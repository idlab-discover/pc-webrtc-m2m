package main

import (
	"encoding/json"
	"flag"
	"fmt"
	"io/ioutil"
	"log"
	"net/http"
	"reflect"
	"strconv"
	"strings"
	"sync"
	"text/template"
	"time"
	"unsafe"

	"golang.org/x/exp/slices"

	"github.com/gorilla/websocket"
	"github.com/pion/interceptor"
	"github.com/pion/sdp/v3"
	"github.com/pion/webrtc/v3"

	"github.com/pion/interceptor/pkg/cc"
	"github.com/pion/interceptor/pkg/gcc"
)

var (
	addr       = flag.String("addr", ":8080", "http service address")
	disableGCC = flag.Bool("d", false, "Disables GCC based bandwidth estimation and instead uses the value from the dashboard")
	upgrader   = websocket.Upgrader{
		CheckOrigin: func(r *http.Request) bool { return true },
	}
	indexTemplate = &template.Template{}

	// lock for peerConnections and trackLocals
	listLock           sync.RWMutex
	peerConnections    []peerConnectionState
	trackLocals        map[string]*webrtc.TrackLocalStaticRTP
	settingEngine      webrtc.SettingEngine
	wsLock             sync.RWMutex
	maxNumberOfTiles   *int
	undesireableTracks map[int][]string
	pcID               = 0

	// Dashboard stuff
	allowedRates         = []int{12500000, 12500000, 12500000, 12500000}
	dashboardConnections = map[int]dashboardConnection{}
	dashboardID          = 0
	dashboardListLock    sync.RWMutex
)

type WebsocketPacket struct {
	ClientID    uint64
	MessageType uint64
	Message     string
}

type bwEstimator struct {
	estimator cc.BandwidthEstimator
}

type peerConnectionState struct {
	peerConnection *webrtc.PeerConnection
	websocket      *threadSafeWriter
	ID             int
	clientID       *int
	nActiveTracks  *int
	bwEstimator    *bwEstimator
	trackBitrates  map[int]*trackBitrate

	camInfo *cameraInfo
}

type trackBitrate struct {
	trackID                 string
	trackNR                 int
	avgRate                 uint64
	counters                []uint32
	currentCounter          uint32
	currentCounterMax       uint32
	currentCounterCompleted uint32
	tempCounter             uint32
}

type cameraInfo struct {
	init             bool
	camMatrix        [4][4]float32
	projectionMatrix [4][4]float32
	position         [3]float32
}

type bitrateAssignment struct {
	pcState         *peerConnectionState
	startCategory   uint
	currentCategory uint
	currentCombo    uint
	usedBitrate     uint
}

type dashboardConnection struct {
	id     int
	writer *threadSafeWriter
}

type DashboardClientBandwidthMessage struct {
	Type      int     `json:"type"`
	Clients   []bool  `json:"clients"`
	Bandwidth []int   `json:"bw"`
	Fov       [][]int `json:"fov"`
	Qual      [][]int `json:"qual"`
}

//					* Start category
//					* Current category
//					* Current combo
//					* P_visibility = start category
//					* pcState pointer

func main() {
	maxNumberOfTiles = flag.Int("t", 1, "Number of tiles")
	flag.Parse()

	fmt.Printf("WebRTCSFU: Starting SFU with at most %d tiles per client\n", *maxNumberOfTiles)

	settingEngine := webrtc.SettingEngine{}
	settingEngine.SetSCTPMaxReceiveBufferSize(16 * 1024 * 1024)

	// Init other state
	log.SetFlags(0)
	trackLocals = map[string]*webrtc.TrackLocalStaticRTP{}
	undesireableTracks = map[int][]string{}

	//ticker := time.NewTicker(1 * time.Second)
	//quit := make(chan struct{})
	go func() {
		// Do bitrate calculationsfor {
		for _ = range time.Tick(1 * time.Second) {
			fmt.Printf("WebRTCSFU: rateCalc: Starting rate calculation for tracks\n")
			for i := 0; i < len(allowedRates); i++ {
				fmt.Printf("WebRTCSFU: rateCalc: Estimated rate for client %d = %d\n", i, allowedRates[i])
			}
			clientState := []bool{false, false, false, false}
			usedBandwidth := []int{0, 0, 0, 0}
			visibility := [][]int{
				{10, 10, 10, 10},
				{10, 10, 10, 10},
				{10, 10, 10, 10},
				{10, 10, 10, 10},
			}
			quality := [][]int{
				{0, 0, 0, 0},
				{0, 0, 0, 0},
				{0, 0, 0, 0},
				{0, 0, 0, 0},
			}
			trackBitrates := [][]int{
				{0, 0, 0},
				{0, 0, 0},
				{0, 0, 0},
				{0, 0, 0},
			}
			for _, pc := range peerConnections {
				for _, tr := range pc.trackBitrates {
					if tr.currentCounterMax == 0 {
						tr.avgRate = 0
					} else {
						upperCounter := int(min(tr.currentCounterMax, 20))

						totalCount := uint32(0)
						for c := 0; c < upperCounter; c++ {
							totalCount += tr.counters[c]
						}
						tr.avgRate = uint64(float32(totalCount) * (20 / float32(upperCounter)))
						if *pc.clientID < 4 {
							trackBitrates[*pc.clientID][tr.trackNR] = int(tr.avgRate)
						}
						fmt.Printf("WebRTCSFU: rateCalc: Client %d, Track #%d: %d\n", pc.ID, tr.trackNR, tr.avgRate)
					}
				}
			}
			cs_score := [][]int{
				[]int{
					60,
					75,
					85,
					100,
				},
				[]int{
					25,
					40,
				},
				[]int{
					15,
				},
			}
			//nSenders := len(peerConnections)
			// Global
			// TODO MOVE OUTSIDE OF THIS LOOP
			cs := [][][]int{
				[][]int{
					[]int{0},
					[]int{0, 2},
					[]int{0, 1},
					[]int{0, 1, 2},
				},
				[][]int{
					[]int{1},
					[]int{1, 2},
				},
				[][]int{
					[]int{2},
				},
			}

			ratesPerClient := make(map[int][][]int, 0)
			for _, pc := range peerConnections {
				if *pc.nActiveTracks != *maxNumberOfTiles {
					continue
				}
				ratesPerClient[pc.ID] = [][]int{
					{},
					{},
					{},
				}
				for i, v := range cs {
					for _, vv := range v {
						rateSum := 0
						for _, vvv := range vv {
							rateSum += int(pc.trackBitrates[vvv].avgRate)
						}
						ratesPerClient[pc.ID][i] = append(ratesPerClient[pc.ID][i], rateSum)
						//fmt.Printf("WebRTCSFU: rateCalc: total rate for client %d for cat %d and combo %d = %d\n", pc.ID, i, j, rateSum)
					}
				}
			}
			hasChanges := false

			for _, pc := range peerConnections {
				if pc.camInfo == nil || !pc.camInfo.init {
					fmt.Printf("WebRTCSFU: rateCalc: cam info for client %d not inited\n", pc.ID)
					continue
				}

				assigments := [][][]*bitrateAssignment{
					{{}, {}, {}, {}},
					{{}, {}},
					{{}},
				}
				dropAssignments := []*bitrateAssignment{}
				baseAssignments := [][]*bitrateAssignment{
					{},
					{},
					{},
				}
				for p, pc2 := range peerConnections {
					if pc2.ID == pc.ID || *pc2.nActiveTracks != *maxNumberOfTiles {
						fmt.Printf("skipping\n")
						continue
					}
					if pc2.camInfo == nil || !pc2.camInfo.init {
						fmt.Printf("WebRTCSFU: rateCalc: inner cam info for client %d not inited\n", pc.ID)
						continue
					}
					pVisibility := calculatePointVisibility(pc, pc2.camInfo.position, 3)
					fmt.Printf("WebRTCSFU: rateCalc: adding base of client %d to %d\n", pc2.ID, pc.ID)
					bitrateA := &bitrateAssignment{
						&peerConnections[p],
						pVisibility,
						pVisibility,
						0,
						0,
					}
					if pVisibility == 3 {
						dropAssignments = append(dropAssignments, bitrateA)
						continue
					}
					baseAssignments[pVisibility] = append(baseAssignments[pVisibility], bitrateA)
					// go from bottom to top => set max possible quality based on category and available bitrate
					// go from top to bottom => reduce own ones first (top to bottom)
					//						 => if not enough steal from below
					//						 => if still not enough drop frames to below (max quality)
					//
				}
				startRate := 100000000
				if *pc.clientID < len(allowedRates) {
					startRate = allowedRates[*pc.clientID]
				}
				if pc.bwEstimator != nil {
					startRate = (*pc.bwEstimator).estimator.GetTargetBitrate() / 8
					if *pc.clientID < len(allowedRates) {
						allowedRates[*pc.clientID] = startRate
					}
				}
				tempRate := startRate // in bytes
				// Assign max allowed value to track
				for i := len(baseAssignments) - 1; i >= 0; i-- {
					asC := baseAssignments[i]
					for j := 0; j < len(asC); j++ {
						as := asC[j]
						for k := len(cs[i]) - 1; k >= 0; k-- {

							if tempRate-ratesPerClient[as.pcState.ID][i][k] >= 0 || k == 0 {
								as.currentCombo = uint(k)
								as.usedBitrate = uint(ratesPerClient[as.pcState.ID][i][k])
								tempRate -= ratesPerClient[as.pcState.ID][i][k]
								assigments[i][k] = append(assigments[i][k], as)
								fmt.Printf("WebRTCSFU: selecting base: %d %d for client %d\n", as.currentCategory, as.currentCombo, as.pcState.ID)
								break
							}
						}

					}
				}
				// Downgrade tracks until enough rate
				for i := 0; i < len(assigments); i++ {
					if tempRate >= 0 {
						break
					}
					// Downscale others DO NOT DROP
					if i+1 < len(assigments) {
						for j := len(assigments[i+1]) - 1; j > 0; j-- {
							asC := assigments[i+1][j]
							downscaleCounter := 0
							for k := 0; k < len(asC); k++ {
								as := asC[k]
								as.currentCombo--
								tempRate += int(as.usedBitrate) - ratesPerClient[as.pcState.ID][as.currentCategory][as.currentCombo]
								assigments[as.currentCategory][as.currentCombo] = append(assigments[as.currentCategory][as.currentCombo], as)
								as.usedBitrate = uint(ratesPerClient[as.pcState.ID][as.currentCategory][as.currentCombo])
								downscaleCounter++
								if tempRate >= 0 {
									break
								}
							}
							assigments[i+1][j] = assigments[i+1][j][downscaleCounter:]
							if tempRate >= 0 {
								break
							}
						}
					}

					if tempRate >= 0 {
						break
					}
					// Downscale self DO NOT DROP
					for j := len(assigments[i]) - 1; j > 0; j-- {
						asC := assigments[i][j]
						downscaleCounter := 0
						for k := 0; k < len(asC); k++ {
							as := asC[k]
							as.currentCombo--
							tempRate += int(as.usedBitrate) - ratesPerClient[as.pcState.ID][as.currentCategory][as.currentCombo]
							as.usedBitrate = uint(ratesPerClient[as.pcState.ID][as.currentCategory][as.currentCombo])
							assigments[as.currentCategory][as.currentCombo] = append(assigments[as.currentCategory][as.currentCombo], as)
							downscaleCounter++
							if tempRate >= 0 {
								break
							}
						}
						assigments[i][j] = assigments[i][j][downscaleCounter:]
						if tempRate >= 0 {
							break
						}
					}
					if tempRate >= 0 {
						break
					}
					// Downscale other DROP
					if i+1 < len(assigments) {
						downscaleCounter := 0
						for j := 0; j < len(assigments[i+1][0]); j++ {
							as := assigments[i+1][0][j]
							tempRate += int(as.usedBitrate)
							if as.currentCategory == uint(len(assigments)-1) {
								dropAssignments = append(dropAssignments, as)
							} else {
								as.currentCategory += 1
								as.currentCombo = uint(len(cs[as.currentCategory]) - 1)
								as.usedBitrate = uint(ratesPerClient[as.pcState.ID][as.currentCategory][as.currentCombo])
								tempRate -= int(as.usedBitrate)
								assigments[as.currentCategory][as.currentCombo] = append(assigments[as.currentCategory][as.currentCombo], as)
							}
							downscaleCounter++
							if tempRate >= 0 {
								break
							}
						}
						assigments[i+1][0] = assigments[i+1][0][downscaleCounter:]
						if tempRate >= 0 {
							break
						}
					}

					// Downscale self DROP
					downscaleCounter := 0
					for j := 0; j < len(assigments[i][0]); j++ {
						as := assigments[i][0][j]
						tempRate += int(as.usedBitrate)
						if as.currentCategory == uint(len(assigments)-1) {
							dropAssignments = append(dropAssignments, as)
						} else {
							as.currentCategory += 1
							as.currentCombo = uint(len(cs[as.currentCategory]) - 1)
							fmt.Printf("WebRTCSFU: selecting base: %d %d for client %d\n", as.currentCategory, as.currentCombo, as.pcState.ID)
							as.usedBitrate = uint(ratesPerClient[as.pcState.ID][as.currentCategory][as.currentCombo])
							tempRate -= int(as.usedBitrate)
							assigments[as.currentCategory][as.currentCombo] = append(assigments[as.currentCategory][as.currentCombo], as)
						}
						downscaleCounter++
						if tempRate >= 0 {
							break
						}
					}
					assigments[i][0] = assigments[i][0][downscaleCounter:]
					if tempRate >= 0 {
						break
					}
					//

				}
				for _, as := range dropAssignments {
					fmt.Printf("WebRTCSFU: rateCalc: dropping tracks")
					if *pc.clientID < 4 && *as.pcState.clientID < 4 {
						visibility[*pc.clientID][*as.pcState.clientID] = int(as.startCategory)
						quality[*pc.clientID][*as.pcState.clientID] = 0
					}
					for _, tr := range as.pcState.trackBitrates {
						v := undesireableTracks[pc.ID]
						foundUn := false
						for _, t := range v {
							if t == tr.trackID {
								foundUn = true
							}
						}
						if !foundUn {
							removeTrackforPeer(pc, tr.trackID)
							fmt.Printf("WebRTCSFU: rateCalc: Client %d, removing track %d of client #%d\n", pc.ID, tr.trackNR, as.pcState.ID)
							hasChanges = true
						}
					}
				}
				for _, asCC := range assigments {
					for _, asC := range asCC {
						for _, as := range asC {
							if *pc.clientID < 4 && *as.pcState.clientID < 4 {
								visibility[*pc.clientID][*as.pcState.clientID] = int(as.startCategory)
								quality[*pc.clientID][*as.pcState.clientID] = cs_score[as.currentCategory][as.currentCombo]
							}
							tracksToEnable := cs[as.currentCategory][as.currentCombo]
							v := undesireableTracks[pc.ID]
							for _, tr := range as.pcState.trackBitrates {
								found := false
								foundUn := false
								for _, t := range tracksToEnable {
									if t == tr.trackNR {
										found = true
									}
								}
								for _, t := range v {
									if t == tr.trackID {
										foundUn = true
									}
								}
								if found && foundUn {
									addTrackforPeer(pc, tr.trackID)
									fmt.Printf("WebRTCSFU: rateCalc: Client %d, adding track %d of client #%d\n", pc.ID, tr.trackNR, as.pcState.ID)
									hasChanges = true
								} else if !found && !foundUn {
									removeTrackforPeer(pc, tr.trackID)
									fmt.Printf("WebRTCSFU: rateCalc: Client %d, removing track %d of client #%d\n", pc.ID, tr.trackNR, as.pcState.ID)
									hasChanges = true
								}
							}
						}
					}
				}
				if *pc.clientID < 4 {
					clientState[*pc.clientID] = true
					usedBandwidth[*pc.clientID] = startRate - tempRate
				}
			}
			dashboardListLock.Lock()
			for _, v := range dashboardConnections {
				v.writer.Lock()
				v.writer.Conn.WriteJSON(DashboardClientBandwidthMessage{
					Type:      1,
					Clients:   clientState,
					Bandwidth: usedBandwidth,
					Fov:       visibility,
					Qual:      quality,
				})
				v.writer.Unlock()
			}
			dashboardListLock.Unlock()

			if hasChanges {
				signalPeerConnections()
			}
		}

	}()

	// Read index.html from disk into memory, serve whenever anyone requests /
	indexHTML, err := ioutil.ReadFile("index.html")
	if err != nil {
		indexHTML = []byte("<p>WebRTCSFU, Nothing to see here, please pass along</p>")
	}
	indexTemplate = template.Must(template.New("").Parse(string(indexHTML)))

	dashboardHTML, err := ioutil.ReadFile("dashboard/index.html")
	if err != nil {
		dashboardHTML = []byte("<p>WebRTCSFU, Nothing to see here, please pass along</p>")
	}
	dashboardTemplate := template.Must(template.New("").Parse(string(dashboardHTML)))

	// WebSocket handler
	http.HandleFunc("/websocket", websocketHandler)
	http.HandleFunc("/dashboardws", websocketHandlerDashboard)
	http.HandleFunc("/dashboard", func(w http.ResponseWriter, r *http.Request) {
		if err := dashboardTemplate.Execute(w, "ws://"+r.Host+"/dashboardws"); err != nil {
			log.Fatal(err)
		}
	})
	http.Handle("/static/", http.StripPrefix("/static/", http.FileServer(http.Dir("dashboard/static"))))
	// index.html handler
	http.HandleFunc("/", func(w http.ResponseWriter, r *http.Request) {
		if err := indexTemplate.Execute(w, "ws://"+r.Host+"/websocket"); err != nil {
			log.Fatal(err)
		}
	})

	// start HTTP server
	log.Fatal(http.ListenAndServe(*addr, nil))
}

// Add to list of tracks and fire renegotation for all PeerConnections
func addTrack(t *webrtc.TrackRemote) *webrtc.TrackLocalStaticRTP {
	listLock.Lock()
	defer func() {
		listLock.Unlock()
		fmt.Println("WebRTCSFU: addTrack: Calling signalPeerConnections")
		signalPeerConnections()
	}()

	fmt.Printf("WebRTCSFU: addTrack: t.ID %s, t.StreamID %s\n", t.ID(), t.StreamID())

	// Create a new TrackLocal with the same codec as our incoming
	trackLocal, err := webrtc.NewTrackLocalStaticRTP(t.Codec().RTPCodecCapability, t.ID(), t.StreamID())
	if err != nil {
		panic(err)
	}

	trackLocals[t.ID()] = trackLocal
	return trackLocal
}

// Remove from list of tracks and fire renegotation for all PeerConnections
func removeTrack(t *webrtc.TrackLocalStaticRTP) {
	listLock.Lock()
	defer func() {
		listLock.Unlock()
		fmt.Println("WebRTCSFU: removeTrack: Calling signalPeerConnections")
		signalPeerConnections()
	}()

	fmt.Printf("WebRTCSFU: removeTrack: t.ID %s\n", t.ID())
	delete(trackLocals, t.ID())
}

func addTrackforPeer(pcState peerConnectionState, trackID string) {
	trackLocal := trackLocals[trackID]
	fmt.Printf("WebRTCSFU: addTrackforPeer: t.ID %s\n", trackLocal.ID())
	rtpSender, err := pcState.peerConnection.AddTrack(trackLocal)
	if err != nil {
		panic(err)
	}
	go func() {
		rtcpBuf := make([]byte, 1500)
		for {
			if _, _, err := rtpSender.Read(rtcpBuf); err != nil {
				panic(err)
			}
		}
	}()
	v := undesireableTracks[pcState.ID]
	for i, t := range v {
		if t == trackID {
			v = append(v[:i], v[i+1:]...)
			break
		}
	}
	undesireableTracks[pcState.ID] = v
}

func removeTrackforPeer(pcState peerConnectionState, trackID string) {
	for _, sender := range pcState.peerConnection.GetSenders() {
		if sender.Track().ID() == trackID {
			pcState.peerConnection.RemoveTrack(sender)
			undesireableTracks[pcState.ID] = append(undesireableTracks[pcState.ID], trackID)
			break
		}
	}
}

// TODO does this work with multiple tiles / audio?
// signalPeerConnections updates each PeerConnection so that it is getting all the expected media tracks
func signalPeerConnections() {
	fmt.Println("WebRTCSFU: signalPeerConnections")

	listLock.Lock()
	defer func() {
		listLock.Unlock()
	}()

	attemptSync := func() (tryAgain bool) {
		for i := range peerConnections {
			if peerConnections[i].peerConnection.ConnectionState() == webrtc.PeerConnectionStateClosed {
				peerConnections = append(peerConnections[:i], peerConnections[i+1:]...)
				return true // We modified the slice, start from the beginning
			}

			// map of sender we already are seanding, so we don't double send
			existingSenders := map[string]bool{}

			for _, sender := range peerConnections[i].peerConnection.GetSenders() {
				if sender.Track() == nil {
					continue
				}

				existingSenders[sender.Track().ID()] = true

				// If we have a RTPSender that doesn't map to a existing track remove and signal
				if _, ok := trackLocals[sender.Track().ID()]; !ok {
					if err := peerConnections[i].peerConnection.RemoveTrack(sender); err != nil {
						return true
					}
				}
			}

			// Don't receive videos we are sending, make sure we don't have loopback
			for _, receiver := range peerConnections[i].peerConnection.GetReceivers() {
				if receiver.Track() == nil {
					continue
				}

				existingSenders[receiver.Track().ID()] = true
			}

			// Add all track we aren't sending yet to the PeerConnection
			for trackID := range trackLocals {
				if _, ok := existingSenders[trackID]; !ok {
					if !slices.Contains(undesireableTracks[peerConnections[i].ID], trackID) {
						rtpSender, err := peerConnections[i].peerConnection.AddTrack(trackLocals[trackID])
						if err != nil {
							return true
						}
						go func() {
							rtcpBuf := make([]byte, 1500)
							for {
								if _, _, err := rtpSender.Read(rtcpBuf); err != nil {
									return
								}
							}
						}()
					}
				}
			}

			offer, err := peerConnections[i].peerConnection.CreateOffer(nil)
			if err != nil {
				return true
			}

			if err = peerConnections[i].peerConnection.SetLocalDescription(offer); err != nil {
				return true
			}

			payload, err := json.Marshal(offer)
			if err != nil {
				return true
			}

			fmt.Printf("WebRTCSFU: attemptSync: Sending offer to peerConnection #%d\n", i)

			s := fmt.Sprintf("%d@%d@%s", 0, 2, string(payload))
			wsLock.Lock()
			peerConnections[i].websocket.WriteMessage(websocket.TextMessage, []byte(s))
			wsLock.Unlock()

			/*offerString, err := json.Marshal(offer)
			if err != nil {
				return true
			}

			if err = peerConnections[i].websocket.WriteJSON(&websocketMessage{
				Event: "offer",
				Data:  string(offerString),
			}); err != nil {
				return true
			}*/
		}

		return
	}

	fmt.Println("WebRTCSFU: signalPeerConnections: attempting sync")

	for syncAttempt := 0; ; syncAttempt++ {
		if syncAttempt == 1 {
			// Release the lock and attempt a sync in 5 seconds
			// We might be blocking a RemoveTrack or AddTrack
			go func() {
				time.Sleep(time.Second * 5)
				signalPeerConnections()
			}()
			return
		}

		if !attemptSync() {
			break
		}
	}
}

// Handle incoming websockets
func websocketHandlerDashboard(w http.ResponseWriter, r *http.Request) {

	fmt.Println("WebRTCSFU: websocketHandlerDashboard: Websocket handler started")

	// Upgrade HTTP request to Websocket
	unsafeWebSocketConn, err := upgrader.Upgrade(w, r, nil)
	if err != nil {
		fmt.Printf("WebRTCSFU: websocketHandlerDashboard: ERROR: %s\n", err)
		return
	}

	fmt.Println("WebRTCSFU: websocketHandlerDashboard: Websocket handler upgraded")

	webSocketConnection := &threadSafeWriter{unsafeWebSocketConn, sync.Mutex{}}
	dashboardListLock.Lock()
	dashboardCon := &dashboardConnection{dashboardID, webSocketConnection}
	dashboardConnections[dashboardID] = *dashboardCon
	dashboardID++
	dashboardListLock.Unlock()
	// When this frame returns close the Websocket
	defer func() {
		fmt.Println("WebRTCSFU: websocketHandlerDashboard: Closing a ThreadSafeWriter")
		dashboardListLock.Lock()
		delete(dashboardConnections, dashboardCon.id)
		dashboardListLock.Unlock()
		webSocketConnection.Close()
	}()

	for {
		_, raw, err := webSocketConnection.ReadMessage()
		if err != nil {
			fmt.Printf("WebRTCSFU: webSocketHandler: ReadMessage: error %w\n", err)
			break
		}
		v := strings.Split(string(raw), "@")
		messageType, _ := strconv.ParseUint(v[0], 10, 64)
		//	message := v[2]
		if messageType != 7 {
			fmt.Printf("WebRTCSFU: webSocketHandler: Message type: %d\n", messageType)
		}

		switch messageType {
		// answer
		case 11:
			fmt.Printf("WebRTCSFU: webSocketHandler: Message : %s\n", string(raw))
			for i := 0; i < len(allowedRates); i++ {
				rate, _ := strconv.ParseUint(v[i+1], 10, 64)
				allowedRates[i] = int(float32(rate) / 8.0 * 1000000)
			}
		}
	}
}

// Handle incoming websockets
func websocketHandler(w http.ResponseWriter, r *http.Request) {

	fmt.Println("WebRTCSFU: webSocketHandler: Websocket handler started")

	// Upgrade HTTP request to Websocket
	unsafeWebSocketConn, err := upgrader.Upgrade(w, r, nil)
	if err != nil {
		fmt.Printf("WebRTCSFU: webSocketHandler: ERROR: %s\n", err)
		return
	}

	fmt.Println("WebRTCSFU: webSocketHandler: Websocket handler upgraded")

	webSocketConnection := &threadSafeWriter{unsafeWebSocketConn, sync.Mutex{}}

	// When this frame returns close the Websocket
	defer func() {
		fmt.Println("WebRTCSFU: webSocketHandler: Closing a ThreadSafeWriter")
		webSocketConnection.Close()
	}()

	fmt.Println("WebRTCSFU: webSocketHandler: Creating a new peer connection")

	mediaEngine := &webrtc.MediaEngine{}
	interceptorRegistry := &interceptor.Registry{}

	if err := mediaEngine.RegisterDefaultCodecs(); err != nil {
		panic(err)
	}

	videoRTCPFeedback := []webrtc.RTCPFeedback{
		{Type: "goog-remb", Parameter: ""},
		{Type: "ccm", Parameter: "fir"},
		{Type: "nack", Parameter: ""},
		{Type: "nack", Parameter: "pli"},
	}
	// TODO Audio RTP
	videoCodecCapability := webrtc.RTPCodecCapability{
		MimeType:     "video/pcm",
		ClockRate:    90000,
		Channels:     0,
		SDPFmtpLine:  "",
		RTCPFeedback: videoRTCPFeedback,
	}

	audioCodecCapability := webrtc.RTPCodecCapability{
		MimeType:     "audio/pcm",
		ClockRate:    90000,
		Channels:     0,
		SDPFmtpLine:  "",
		RTCPFeedback: nil,
	}

	if err := mediaEngine.RegisterCodec(webrtc.RTPCodecParameters{
		RTPCodecCapability: videoCodecCapability,
		PayloadType:        5,
	}, webrtc.RTPCodecTypeVideo); err != nil {
		panic(err)
	}

	if err := mediaEngine.RegisterCodec(webrtc.RTPCodecParameters{
		RTPCodecCapability: audioCodecCapability,
		PayloadType:        6,
	}, webrtc.RTPCodecTypeAudio); err != nil {
		panic(err)
	}

	mediaEngine.RegisterFeedback(webrtc.RTCPFeedback{Type: "nack"}, webrtc.RTPCodecTypeVideo)
	mediaEngine.RegisterFeedback(webrtc.RTCPFeedback{Type: "nack", Parameter: "pli"}, webrtc.RTPCodecTypeVideo)
	mediaEngine.RegisterFeedback(webrtc.RTCPFeedback{Type: webrtc.TypeRTCPFBTransportCC}, webrtc.RTPCodecTypeVideo)
	if err := mediaEngine.RegisterHeaderExtension(webrtc.RTPHeaderExtensionCapability{URI: sdp.TransportCCURI}, webrtc.RTPCodecTypeVideo); err != nil {
		panic(err)
	}

	bwEstimator := &bwEstimator{}
	if !*disableGCC {

		congestionController, err := cc.NewInterceptor(func() (cc.BandwidthEstimator, error) {
			return gcc.NewSendSideBWE(gcc.SendSideBWEMinBitrate(55000*30*8), gcc.SendSideBWEInitialBitrate(55000*30*8), gcc.SendSideBWEMaxBitrate(262_744_320))
		})
		if err != nil {
			panic(err)
		}
		congestionController.OnNewPeerConnection(func(id string, estimator cc.BandwidthEstimator) {
			pointerVal := reflect.ValueOf(estimator)
			val := reflect.Indirect(pointerVal)

			lossControllerFieldPtr := val.FieldByName("lossController")
			lossControllerField := reflect.Indirect((lossControllerFieldPtr))

			minBitrateField := lossControllerField.FieldByName("minBitrate")
			ptrToMin := unsafe.Pointer(minBitrateField.UnsafeAddr())
			actualMinPtr := (*int)(ptrToMin)
			*actualMinPtr = 55000 * 30 * 8

			maxBitrateField := lossControllerField.FieldByName("maxBitrate")
			ptrToMax := unsafe.Pointer(maxBitrateField.UnsafeAddr())
			actualMaxPtr := (*int)(ptrToMax)
			*actualMaxPtr = 262_744_320

			bwEstimator.estimator = estimator
		})
		interceptorRegistry.Add(congestionController)
	}

	if err = webrtc.ConfigureTWCCHeaderExtensionSender(mediaEngine, interceptorRegistry); err != nil {
		panic(err)
	}
	if err = webrtc.RegisterDefaultInterceptors(mediaEngine, interceptorRegistry); err != nil {
		panic(err)
	}

	peerConnection, err := webrtc.NewAPI(webrtc.WithSettingEngine(settingEngine), webrtc.WithMediaEngine(mediaEngine), webrtc.WithInterceptorRegistry(interceptorRegistry)).NewPeerConnection(webrtc.Configuration{})
	// peerConnection, err := webrtc.NewAPI(webrtc.WithSettingEngine(settingEngine), webrtc.WithInterceptorRegistry(interceptorRegistry), webrtc.WithMediaEngine(mediaEngine)).NewPeerConnection(webrtc.Configuration{})
	if err != nil {
		panic(err)
	}

	fmt.Println("WebRTCSFU: webSocketHandler: Peer connection created")

	// When this frame returns close the PeerConnection
	defer func() {
		fmt.Println("WebRTCSFU: webSocketHandler: Closing a peer connection")
		peerConnection.Close()
	}()

	fmt.Println("WebRTCSFU: webSocketHandler: Iterating over video tracks")

	for i := 0; i < *maxNumberOfTiles; i++ {
		if _, err := peerConnection.AddTransceiverFromKind(webrtc.RTPCodecTypeVideo, webrtc.RTPTransceiverInit{
			Direction: webrtc.RTPTransceiverDirectionRecvonly,
		}); err != nil {
			fmt.Printf("WebRTCSFU: webSocketHandler: ERROR: %s\n", err)
			return
		}
	}

	fmt.Println("WebRTCSFU: webSocketHandler: Adding audio track")

	if _, err := peerConnection.AddTransceiverFromKind(webrtc.RTPCodecTypeAudio, webrtc.RTPTransceiverInit{
		Direction: webrtc.RTPTransceiverDirectionRecvonly,
	}); err != nil {
		fmt.Printf("WebRTCSFU: webSocketHandler: ERROR: %s\n", err)
		return
	}

	fmt.Println("WebRTCSFU: webSocketHandler: Waiting for lock")

	// Add our new PeerConnection to global list
	listLock.Lock()
	start := int(0)
	var pcState = peerConnectionState{peerConnection, webSocketConnection, pcID, &start, new(int), bwEstimator, map[int]*trackBitrate{}, &cameraInfo{}}
	pcID += 1
	peerConnections = append(peerConnections, pcState)
	fmt.Printf("WebRTCSFU: webSocketHandler: peerConnection #%d\n", len(peerConnections))
	undesireableTracks[pcID] = []string{}
	listLock.Unlock()

	fmt.Println("WebRTCSFU: webSocketHandler: Will now call signalpeerconnections again")

	// Signal for the new PeerConnection
	signalPeerConnections()

	// Trickle ICE and emit server candidate to client
	peerConnection.OnICECandidate(func(i *webrtc.ICECandidate) {
		if i == nil {
			return
		}
		fmt.Println("WebRTCSFU: webSocketHandler: OnICECandidate: Found a candidate")
		payload := []byte(i.ToJSON().Candidate)
		s := fmt.Sprintf("%d@%d@%s", 0, 4, string(payload))
		wsLock.Lock()
		err = webSocketConnection.WriteMessage(websocket.TextMessage, []byte(s))
		wsLock.Unlock()
		if err != nil {
			panic(err)
		}
	})

	// If PeerConnection is closed remove it from global list
	peerConnection.OnConnectionStateChange(func(p webrtc.PeerConnectionState) {
		fmt.Printf("WebRTCSFU: webSocketHandler: OnConnectionStateChange: Peer connection state has changed to %s\n", p.String())
		switch p {
		case webrtc.PeerConnectionStateFailed:
			if err := peerConnection.Close(); err != nil {
				fmt.Printf("WebRTCSFU: webSocketHandler: ERROR: %s\n", err)
			}
		case webrtc.PeerConnectionStateClosed:
			fmt.Println("WebRTCSFU: webSocketHandler: OnConnectionStateChange: Closed")
			signalPeerConnections()
		case webrtc.PeerConnectionStateConnected:
			fmt.Println("WebRTCSFU: webSocketHandler: OnConnectionStateChange: Connected")

		}
	})

	peerConnection.OnTrack(func(t *webrtc.TrackRemote, _ *webrtc.RTPReceiver) {
		// Create a track to fan out our incoming video to all peers
		//if t.Kind() == webrtc.RTPCodecTypeAudio {
		//	return
		//}
		trackLocal := addTrack(t)
		defer func() {
			fmt.Printf("WebRTCSFU: OnTrack: removing track %w\n", trackLocal.ID)
			removeTrack(trackLocal)
		}()

		idTokens := strings.Split(t.ID(), "_")
		tileNr := 99
		if t.Kind() == webrtc.RTPCodecTypeVideo {
			tileNr, _ = strconv.Atoi(idTokens[2])
			listLock.Lock()
			pcState.trackBitrates[tileNr] = &trackBitrate{}
			pcState.trackBitrates[tileNr].trackID = t.ID()
			pcState.trackBitrates[tileNr].trackNR = tileNr
			pcState.trackBitrates[tileNr].counters = make([]uint32, 20)
			*pcState.nActiveTracks++
			listLock.Unlock()
		}

		buf := make([]byte, 1500)

		startTime := time.Now().UnixNano() // / int64(time.Millisecond)
		prevBucket := int64(0)
		for {
			i, _, err := t.Read(buf)
			if err != nil {
				fmt.Printf("WebRTCSFU: OnTrack: error during read: %s\n", err)
				break
			}

			nextTime := time.Now().UnixNano() //
			nsDiff := nextTime - startTime
			msBucket := nsDiff / int64(50*time.Millisecond)

			if msBucket != int64(prevBucket) {
				pcState.trackBitrates[tileNr].currentCounterCompleted = pcState.trackBitrates[tileNr].currentCounter
				pcState.trackBitrates[tileNr].counters[pcState.trackBitrates[tileNr].currentCounter] = pcState.trackBitrates[tileNr].tempCounter
				pcState.trackBitrates[tileNr].currentCounter = (pcState.trackBitrates[tileNr].currentCounter + 1) % 20
				pcState.trackBitrates[tileNr].currentCounterMax++
				pcState.trackBitrates[tileNr].tempCounter = 0
			}
			pcState.trackBitrates[tileNr].tempCounter += uint32(i)
			prevBucket = msBucket
			if _, err = trackLocal.Write(buf[:i]); err != nil {
				fmt.Printf("WebRTCSFU: OnTrack: error during write: %s\n", err)
				break
			}
		}
	})

	for {
		_, raw, err := webSocketConnection.ReadMessage()
		if err != nil {
			fmt.Printf("WebRTCSFU: webSocketHandler: ReadMessage: error %w\n", err)
			break
		}
		v := strings.Split(string(raw), "@")
		clientID, _ := strconv.ParseUint(v[0], 10, 64)
		*pcState.clientID = int(clientID)
		//fmt.Printf("WebRTCSFU: webSocketHandler: Message fro;: %d\n", *pcState.clientID)
		messageType, _ := strconv.ParseUint(v[1], 10, 64)
		message := v[2]
		if messageType != 7 {
			fmt.Printf("WebRTCSFU: webSocketHandler: Message type: %d\n", messageType)
		}

		switch messageType {
		// answer
		case 3:
			answer := webrtc.SessionDescription{}
			if err := json.Unmarshal([]byte(message), &answer); err != nil {
				panic(err)
			}
			if err := peerConnection.SetRemoteDescription(answer); err != nil {
				panic(err)
			}
		// candidate
		case 4:
			candidate := webrtc.ICECandidateInit{Candidate: message}
			if err := peerConnection.AddICECandidate(candidate); err != nil {
				panic(err)
			}
		// remove track
		case 5:
			removeTrackforPeer(pcState, message)
		// add track
		case 6:
			addTrackforPeer(pcState, message)
		case 7:
			updateCamInfoforPeer(pcState, message)
		}
	}
}
func updateCamInfoforPeer(pcState peerConnectionState, data string) {
	tokens := strings.Split(data, ";")
	if len(tokens) == 36 {
		pcState.camInfo.init = true
		pcState.camInfo.camMatrix = fillMatrix(tokens, 0)
		pcState.camInfo.projectionMatrix = fillMatrix(tokens, 16)
		pcState.camInfo.position = fillPosition(tokens, 32)
	}
}

func fillMatrix(tokens []string, offset int) [4][4]float32 {
	m := [4][4]float32{}
	for i := 0; i < 4; i++ {
		for j := 0; j < 4; j++ {
			mm, _ := strconv.ParseFloat(tokens[offset+j+(i*4)], 32)
			m[i][j] = float32(mm)
			//		fmt.Printf("%f\t", float32(mm))
		}
		//	fmt.Printf("\n")
	}
	return m
}

func fillPosition(tokens []string, offset int) [3]float32 {
	p := [3]float32{}
	for i := 0; i < 3; i++ {
		pp, _ := strconv.ParseFloat(tokens[offset+i], 32)
		p[i] = float32(pp)
		//fmt.Printf("%s %f %f %f\n", tokens[offset+i], pp, float32(pp), p[i])
	}
	return p
}

func multiplyPoint(m [4][4]float32, p [3]float32) [3]float32 {
	x := m[0][0]*p[0] + m[0][1]*p[1] + m[0][2]*p[2] + m[0][3]
	y := m[1][0]*p[0] + m[1][1]*p[1] + m[1][2]*p[2] + m[1][3]
	z := m[2][0]*p[0] + m[2][1]*p[1] + m[2][2]*p[2] + m[2][3]
	n := m[3][0]*p[0] + m[3][1]*p[1] + m[3][2]*p[2] + m[3][3]
	n = 1 / n
	x *= n
	y *= n
	z *= n
	return [3]float32{x, y, z}
}

func convertToClipspace(m [4][4]float32, p [3]float32) [4]float32 {
	x := m[0][0]*p[0] + m[0][1]*p[1] + m[0][2]*p[2] + m[0][3]
	y := m[1][0]*p[0] + m[1][1]*p[1] + m[1][2]*p[2] + m[1][3]
	z := m[2][0]*p[0] + m[2][1]*p[1] + m[2][2]*p[2] + m[2][3]
	w := m[3][0]*p[0] + m[3][1]*p[1] + m[3][2]*p[2] + m[3][3]
	return [4]float32{x, y, z, w}
}

func calculatePointVisibility(pcState peerConnectionState, p [3]float32, nBands uint) uint {
	camSpace := multiplyPoint(pcState.camInfo.camMatrix, p)
	clipSpace := convertToClipspace(pcState.camInfo.projectionMatrix, camSpace)
	ndcSpace := [3]float32{
		clipSpace[0] / clipSpace[3],
		clipSpace[1] / clipSpace[3],
		clipSpace[2] / clipSpace[3],
	}
	fmt.Printf("WebRTCSFU: calculatePointVisibility: Client %d, x %f y %f z %f\n", pcState.ID, p[0], p[1], p[2])
	fmt.Printf("WebRTCSFU: calculatePointVisibility: Pos x %f y %f z %f\n", ndcSpace[0], ndcSpace[1], ndcSpace[2])

	bandSpacing := 1.0 / float32(nBands) * 1.0
	for i := uint(0); i < nBands; i++ {
		if (ndcSpace[0] >= 0-bandSpacing*float32(i+1)) && (ndcSpace[0] <= 0+bandSpacing*float32(i+1)) {
			return i
		}
	}
	if (ndcSpace[0] >= -1.25 && ndcSpace[0] <= 1.25) && (ndcSpace[1] >= -1.25 && ndcSpace[1] <= 1.25) {
		return nBands - 1
	}

	return nBands
}

// Helper to make Gorilla Websockets threadsafe
type threadSafeWriter struct {
	*websocket.Conn
	sync.Mutex
}

/*
func (t *threadSafeWriter) WriteJSON(v interface{}) error {
	t.Lock()
	defer t.Unlock()

	return t.Conn.WriteJSON(v)
}
*/
