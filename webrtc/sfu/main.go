package main

import (
	"encoding/binary"
	"encoding/json"
	"flag"
	"fmt"
	"goweb/shared/src/logger"
	"log"
	"net/http"
	"os"
	"regexp"
	"strconv"
	"strings"
	"sync"
	"text/template"
	"time"

	"golang.org/x/exp/slices"

	"github.com/gorilla/websocket"
	"github.com/pion/webrtc/v4"
	"github.com/shirou/gopsutil/host"
	"github.com/shirou/gopsutil/v4/cpu"
	"github.com/shirou/gopsutil/v4/process"

	"github.com/pion/interceptor/pkg/cc"
)

var (
	addr       = flag.String("addr", ":8080", "http service address")
	disableGCC = flag.Bool("d", false, "Disables GCC based bandwidth estimation and instead uses the value from the dashboard")
	disableABR = flag.Bool("b", false, "Disables adaptive bitrate allocation algorithm")
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

	camInfo            *cameraInfo
	capturerIntrinsics map[int]string

	pendingCandidatesString []string
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

type DashboardSystemResourcesMessage struct {
	Type     int `json:"type"`
	CPUUsage int `json:"cpuUsage"`
	MemUsage int `json:"memUsage"`
	CPUTemp  int `json:"cpuTemp"`
}

//					* Start category
//					* Current category
//					* Current combo
//					* P_visibility = start category
//					* pcState pointer

func main() {
	subDir := flag.String("subDir", "", "Subdirectory for logs")
	managerIP := flag.String("managerIP", "", "IP address of the session manager instance")
	address := flag.String("address", "", "IP address of the session manager instance, without port")
	port := flag.Uint("port", 0, "IP address of the session manager instance, without port")
	providerKey := flag.String("providerKey", "", "ID of this provider, assigned by the session manager")
	authKey := flag.String("authKey", "", "Optional authentication key provider by the session manager")
	enableConsoleOutput := flag.Bool("console", false, "Enable console output for logger")
	ipFilter := flag.String("ipFilter", "", "IP Prefix to filter on (e.g., 192.168.1.)")
	flag.Parse()
	if *managerIP == "" || *address == "" || *port == 0 || *providerKey == "" {
		println("wrongs args", *managerIP, *address, *port, *providerKey)
		return
	}
	logger.LogInit(*providerKey, *providerKey, logger.LogBlue, 10, *subDir, *enableConsoleOutput)
	logger.LogWithMessage(*providerKey, logger.Creating, true, true,
		fmt.Sprintf("managerIP=%s address=%s port=%d authKey=%s",
			*managerIP, *address, *port, *authKey))
	settingEngine := webrtc.SettingEngine{}
	settingEngine.SetSCTPMaxReceiveBufferSize(16 * 1024 * 1024)

	// Init other state
	log.SetFlags(0)
	trackLocals = map[string]*webrtc.TrackLocalStaticRTP{}
	undesireableTracks = map[int][]string{}

	sfu := NewSFU(*address, *port, *ipFilter)
	sm, err := NewSessionManagerConnection(*managerIP, *providerKey, *authKey, sfu)
	if err != nil {
		panic(err)
	}
	sm.StartListening()
	go func() {
		nCPUs, _ := cpu.Counts(true)
		proc, _ := process.NewProcess(int32(os.Getpid()))
		pattern := `coretemp_core(\d+)_input: (\d+(\.\d+)?)°C`
		re := regexp.MustCompile(pattern)
		for _ = range time.Tick(2 * time.Second) {
			memUsage, _ := proc.MemoryInfo()
			cpuUsage, _ := proc.CPUPercent()
			sensors, _ := host.SensorsTemperatures()
			cpuCounter := 0.0
			cpuTotalTemp := 0.0
			for _, sensor := range sensors {
				matches := re.FindStringSubmatch(sensor.SensorKey)
				if matches != nil {
					cpuCounter++
					cpuTotalTemp += sensor.Temperature
				}
			}
			//t, _ := proc.Cmdline()
			avgTempVal := 0
			cpuUsageVal := 0
			memUsageVal := int(memUsage.RSS / (1024 * 1024))
			if cpuCounter != 0 {
				avgTempVal = int(cpuTotalTemp / cpuCounter)
			}
			if nCPUs != 0 {
				cpuUsageVal = int(cpuUsage / float64(nCPUs))
			}
			logger.LogWithMessage("SystemResources", logger.SystemResources, true, true, fmt.Sprintf("ts=%d cpuUsage=%d memUsage=%d cpuTemp=%d", time.Now().UnixMilli(), cpuUsageVal, memUsageVal, avgTempVal))
		}
	}()
	select {}
	//ticker := time.NewTicker(1 * time.Second)
	//quit := make(chan struct{})
	// System metrics loop:
	/*go func() {
		nCPUs, _ := cpu.Counts(true)
		proc, _ := process.NewProcess(int32(os.Getpid()))
		pattern := `coretemp_core(\d+)_input: (\d+(\.\d+)?)°C`
		re := regexp.MustCompile(pattern)
		for _ = range time.Tick(2 * time.Second) {
			memUsage, _ := proc.MemoryInfo()
			cpuUsage, _ := proc.CPUPercent()
			sensors, _ := host.SensorsTemperatures()
			cpuCounter := 0.0
			cpuTotalTemp := 0.0
			for _, sensor := range sensors {
				matches := re.FindStringSubmatch(sensor.SensorKey)
				if matches != nil {
					cpuCounter++
					cpuTotalTemp += sensor.Temperature
				}
			}
			//t, _ := proc.Cmdline()
			avgTemp := 0
			if cpuCounter != 0 {
				avgTemp = int(cpuTotalTemp / cpuCounter)
			}
			dashboardListLock.Lock()
			for _, v := range dashboardConnections {
				v.writer.Lock()
				v.writer.Conn.WriteJSON(DashboardSystemResourcesMessage{
					Type:     2,
					CPUUsage: int(cpuUsage / float64(nCPUs)),
					MemUsage: int(memUsage.RSS / (1024 * 1024)),
					CPUTemp:  avgTemp,
				})
				v.writer.Unlock()
			}
			dashboardListLock.Unlock()
		}
	}()

	go func() {
		// Do bitrate calculationsfor {
		if *disableABR {
			return
		}
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
				if pc.bwEstimator.estimator != nil {
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
	log.Fatal(http.ListenAndServe(*addr, nil))*/
}

// Add to list of tracks and fire renegotation for all PeerConnections
func addTrack(t *webrtc.TrackRemote) *webrtc.TrackLocalStaticRTP {

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
				//panic(err)
				return
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

func updateCapturerIntrinsicsForPeer(pcState peerConnectionState, data string) {
	listLock.Lock()
	wsLock.Lock()
	defer func() {
		listLock.Unlock()
		wsLock.Unlock()
	}()
	capturerID := getCapturerIDFromString(data)
	pcState.capturerIntrinsics[capturerID] = data
	println("INTRSINICS", capturerID)
	for _, pc := range peerConnections {
		if pcState.clientID != pc.clientID {
			s := fmt.Sprintf("%d@%d@%s", *pcState.clientID, 8, data)
			pc.websocket.WriteMessage(websocket.TextMessage, []byte(s))
		}

	}
}

func updateCamInfoforPeer(pcState peerConnectionState, data string) {
	data = strings.ReplaceAll(data, ",", ".")
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

// Parse the first 4 bytes of a string as an unsigned int (uint32, big endian)
func getCapturerIDFromString(s string) int {
	b := []byte(s)
	if len(b) < 4 {
		return 0 // or handle error as needed
	}
	return int(binary.LittleEndian.Uint32(b[:4]))
}

// Helper to make Gorilla Websockets threadsafe
type threadSafeWriter struct {
	*websocket.Conn
	sync.Mutex
}

func (t *threadSafeWriter) WriteJSONSafe(v interface{}) error {
	t.Lock()
	defer t.Unlock()
	return t.WriteJSON(v)
}

func (t *threadSafeWriter) WriteMessageSafe(messageType int, data []byte) error {
	t.Lock()
	defer t.Unlock()
	return t.WriteMessage(messageType, data)
}

/*
func (t *threadSafeWriter) WriteJSON(v interface{}) error {
	t.Lock()
	defer t.Unlock()

	return t.Conn.WriteJSON(v)
}
*/
