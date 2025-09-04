package main

import (
	"encoding/json"
	"fmt"
	"io/ioutil"
	"log"
	"net/http"
	"strconv"
	"sync"
	"text/template"
	"time"

	"github.com/gorilla/websocket"
	"golang.org/x/exp/slices"
)

const NameSFU = "SFU"

type SFU struct {
	settings  SFUSettings
	address   string
	port      uint
	clients   map[uint]*ClientConnection
	websocket *threadSafeWriter
	mut       sync.Mutex
}

func NewSFU(address string, port uint) *SFU {
	Log(NameSFU, Creating, true, true)
	sfu := &SFU{
		address: address,
		port:    port,
		clients: map[uint]*ClientConnection{},
		mut:     sync.Mutex{},
	}
	Log(NameSFU, Created, true, true)
	return sfu
}

func (sfu *SFU) AddClient(msg NewClientMessage) {
	sfu.mut.Lock()
	defer sfu.mut.Unlock()
	client := NewClientConnection(msg.ClientID, msg.AuthKey, msg.SenderVideoTracks, msg.SenderAudioTracks)
	client.SetupPeerConnection(&sfu.settings)
	// This will need to be changed to do it based on ReceiverVideoTracksInstead
	for _, otherC := range sfu.clients {
		hasChanged := false
		for _, t := range client.SenderVideoTracks {
			otherC.AddTrackFromOther("client", client.clientID, t.TrackID, t.WebRTCTrack)
			hasChanged = true
		}
		for _, t := range client.SenderAudioTracks {
			otherC.AddTrackFromOther("client", client.clientID, t.TrackID, t.WebRTCTrack)
			hasChanged = true
		}
		if hasChanged {
			otherC.SignalRenegotiation()
		}
		for _, t := range otherC.SenderVideoTracks {
			client.AddTrackFromOther("client", otherC.clientID, t.TrackID, t.WebRTCTrack)
		}
		for _, t := range otherC.SenderAudioTracks {
			client.AddTrackFromOther("client", otherC.clientID, t.TrackID, t.WebRTCTrack)
		}
	}
	sfu.clients[msg.ClientID] = client
}

func (sfu *SFU) SetupSFU(settings SFUSettings) {
	sfu.settings = settings
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
	http.HandleFunc("/websocket_client", sfu.websocketHandler)
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
	log.Fatal(http.ListenAndServe(fmt.Sprintf("%s:%d", sfu.address, sfu.port), nil))
}

// Handle incoming websockets
func (sfu *SFU) websocketHandler(w http.ResponseWriter, r *http.Request) {

	fmt.Println("WebRTCSFU: webSocketHandler: Websocket handler started")
	clientIDS := r.URL.Query().Get("clientID")
	if clientIDS == "" {
		fmt.Println("WebRTCSFU: webSocketHandler: No clientID provided, returning 400")
		http.Error(w, "No clientID provided", http.StatusBadRequest)
		return
	}
	clientID, err := strconv.ParseUint(clientIDS, 10, 64)
	if err != nil {
		fmt.Printf("WebRTCSFU: webSocketHandler: Error parsing clientID: %s\n", err)
		http.Error(w, "Invalid clientID", http.StatusBadRequest)
		return
	}
	sfu.mut.Lock()
	client := sfu.clients[uint(clientID)]
	sfu.mut.Unlock()
	if sfu.settings.VerifyAuthKey {
		authKeyS := r.URL.Query().Get("authKey")
		if authKeyS == "" {
			fmt.Println("WebRTCSFU: webSocketHandler: No authKey provided, returning 400")
			http.Error(w, "No authKey provided", http.StatusBadRequest)
			sfu.mut.Unlock()
			return
		}
		if client.authKey != authKeyS {
			fmt.Println("WebRTCSFU: webSocketHandler: Invalid authKey provided, returning 403")
			http.Error(w, "Invalid authKey", http.StatusForbidden)
			sfu.mut.Unlock()
			return
		}
	}

	// Upgrade HTTP request to Websocket
	unsafeWebSocketConn, err := upgrader.Upgrade(w, r, nil)
	if err != nil {
		fmt.Printf("WebRTCSFU: webSocketHandler: ERROR: %s\n", err)
		return
	}

	fmt.Println("WebRTCSFU: webSocketHandler: Websocket handler upgraded")

	client.SetupWebsocket(
		&ThreadSafeWebsocket{
			unsafeWebSocketConn, sync.Mutex{},
		},
	)
	sfu.mut.Lock()
	// TODO Check if this is even needed, atm we already add all tracks when new client connects
	// Add all tracks from other clients to client
	// If quality adaptation is enabled => unpause if needed
	// For the first time tracks need to be added with AddTrack to get the RTPSender
	client.SignalRenegotiation()
	sfu.mut.Unlock()

	fmt.Println("WebRTCSFU: webSocketHandler: Will now call signalpeerconnections again")

}

// If someone connects => signal all PeerConnections again
// If someone disconnects => signal all PeerConnections again
// When quality changes => send new qualities to clients => "pause" tracks by replacing it with the nil track
func (sfu *SFU) signalClients() {
	fmt.Println("WebRTCSFU: signalPeerConnections")

	sfu.mut.Lock()
	defer func() {
		sfu.mut.Unlock()
	}()

	attemptSync := func() (tryAgain bool) {
		for i := range sfu.clients {
			// TODO Handle client connection state changes
			//if peerConnections[i].peerConnection.ConnectionState() == webrtc.PeerConnectionStateClosed {
			//	peerConnections = append(peerConnections[:i], peerConnections[i+1:]...)
			//	return true // We modified the slice, start from the beginning
			//}

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
