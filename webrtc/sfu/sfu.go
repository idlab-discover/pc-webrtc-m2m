package main

import (
	"encoding/json"
	"fmt"
	"goweb/shared/src/logger"
	"goweb/shared/src/metrics"
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
	settings            SFUSettings
	address             string
	port                uint
	clients             map[uint]*ClientConnection
	virtualClients      map[uint]string
	remoteProviders     map[string]ProviderConnection
	overallTrackMetrics *metrics.OverallTrackMetrics
	qualityAdaptation   QualityAdaptation
	websocket           *threadSafeWriter
	ipFilter            string
	mut                 sync.Mutex
	providerKey string
}

func NewSFU(address string, port uint, ipFilter string, providerKey string) *SFU {
	logger.Log(NameSFU, logger.Creating, true, true)
	sfu := &SFU{
		address:             address,
		port:                port,
		clients:             map[uint]*ClientConnection{},
		virtualClients:      map[uint]string{},
		remoteProviders:     map[string]ProviderConnection{},
		overallTrackMetrics: metrics.NewOverallTrackMetrics(),
		ipFilter:            ipFilter,
		providerKey:         providerKey,
		mut:                 sync.Mutex{},
	}
	sfu.overallTrackMetrics.StartMeasuring()
	logger.Log(NameSFU, logger.Created, true, true)
	return sfu
}

func (sfu *SFU) AddClient(msg NewClientMessage) {
	sfu.mut.Lock()
	logger.LogWithMessage(NameSFU, logger.MutLock, true, true, "func=AddClient")
	defer sfu.mut.Unlock()
	defer logger.LogWithMessage(NameSFU, logger.MutUnlock, true, true, "func=AddClient")
	client := NewClientConnection(sfu, msg.ClientID, msg.AuthKey, msg.SenderVideoTracks, msg.SenderAudioTracks)
	client.SetupPeerConnection(&sfu.settings)
	// This will need to be changed to do it based on ReceiverVideoTracksInstead
	/*for _, otherC := range sfu.clients {
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
	}*/
	sfu.clients[msg.ClientID] = client
}

func (sfu *SFU) AddRemoteProvider(msg RemoteProviderAddedMessage, selfProviderKey string, selfAuthKey string) {
	sfu.mut.Lock()
	logger.LogWithMessage(NameSFU, logger.MutLock, true, true, "func=AddRemoteProvider")
	defer sfu.mut.Unlock()
	defer logger.LogWithMessage(NameSFU, logger.MutUnlock, true, true, "func=AddRemoteProvider")
	// TODO Check if provider already exists
	provider := CreateRemoteProvider(sfu, msg.ProviderType, msg.ProviderKey, msg.Address, msg.Port, msg.AuthKey)
	// Call connect
	if provider == nil {
		fmt.Printf("SFU: AddRemoteProvider: Unknown provider type %s\n", msg.ProviderType) // TOOD Proper logging
		return
	}
	sfu.remoteProviders[msg.ProviderKey] = provider
	provider.SetupForwarding()
	provider.ConnectWebSocket("webrtc_sfu", selfProviderKey, selfAuthKey)
}

func (sfu *SFU) SetupSFU(settings SFUSettings) {
	//println("Setting up SFU")
	sfu.settings = settings
	sfu.qualityAdaptation = CreateQualityAdaptation(settings.AdaptationMethod)
	sfu.StartPerformingQualityAdaptation()
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
	http.HandleFunc("/websocket_client", sfu.websocketClientHandler)
	http.HandleFunc("/websocket_provider", sfu.websocketProviderHandler)
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
func (sfu *SFU) websocketClientHandler(w http.ResponseWriter, r *http.Request) {

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
	logger.LogWithMessage(NameSFU, logger.MutLock, true, true, "func=websocketClientHandler")
	// TODO Check if this is even needed, atm we already add all tracks when new client connects
	// Add all tracks from other clients to client
	// If quality adaptation is enabled => unpause if needed
	// For the first time tracks need to be added with AddTrack to get the RTPSender
	client.SignalRenegotiation()
	sfu.mut.Unlock()
	logger.LogWithMessage(NameSFU, logger.MutUnlock, true, true, "func=websocketClientHandler")

	fmt.Println("WebRTCSFU: webSocketHandler: Will now call signalpeerconnections again")

}

func (sfu *SFU) websocketProviderHandler(w http.ResponseWriter, r *http.Request) {
	logger.Log(NameSFU, logger.RemoteProviderConnectionStarted, true, true)
	providerKey := r.URL.Query().Get("providerKey")
	if providerKey == "" {
		fmt.Println("WebRTCSFU: webSocketHandler: No providerKey provided, returning 400")
		http.Error(w, "No providerKey provided", http.StatusBadRequest)
		logger.Log(NameSFU, logger.RemoteProviderConnectionFailed, true, true)
		return
	}
	providerType := r.URL.Query().Get("providerType")
	if providerType == "" {
		fmt.Println("WebRTCSFU: webSocketHandler: No providerType provided, returning 400")
		http.Error(w, "No providerType provided", http.StatusBadRequest)
		logger.Log(NameSFU, logger.RemoteProviderConnectionFailed, true, true)
		return
	}
	sfu.mut.Lock()
	logger.LogWithMessage(NameSFU, logger.MutLock, true, true, "func=websocketProviderHandler")
	if _, exists := sfu.remoteProviders[providerKey]; exists {
		fmt.Println("WebRTCSFU: webSocketHandler: Provider already connected, returning 400")
		http.Error(w, "Provider already connected", http.StatusBadRequest)
		logger.Log(NameSFU, logger.RemoteProviderConnectionFailed, true, true)
		sfu.mut.Unlock()
		logger.LogWithMessage(NameSFU, logger.MutUnlock, true, true, "func=websocketProviderHandler")
		return
	}
	logger.LogWithMessage(NameSFU, logger.RemoteProviderConnectionConnecting, true, true, fmt.Sprintf("providerKey=%s providerType=%s", providerKey, providerType))
	provider := CreateRemoteProvider(sfu, providerType, providerKey, r.RemoteAddr, 0, "") // TODO Fix this port and address
	provider.SetupForwarding()
	sfu.remoteProviders[providerKey] = provider
	sfu.mut.Unlock()
	logger.LogWithMessage(NameSFU, logger.MutUnlock, true, true, "func=websocketProviderHandler")
	// TODO Verify authKey

	// Upgrade HTTP request to Websocket
	unsafeWebSocketConn, err := upgrader.Upgrade(w, r, nil)
	if err != nil {
		fmt.Printf("WebRTCSFU: webSocketHandler: ERROR: %s\n", err)
		return
	}

	provider.SetupWebsocket(
		&ThreadSafeWebsocket{
			unsafeWebSocketConn, sync.Mutex{},
		},
	)
	sfu.mut.Lock()
	//client.SignalRenegotiation()
	sfu.mut.Unlock()
	logger.LogWithMessage(NameSFU, logger.RemoteProviderConnectionSuccess, true, true, fmt.Sprintf("providerKey=%s providerType=%s", providerKey, providerType))
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

func (sfu *SFU) AddVirtualClient(msg ProviderRemoteProviderClientMessage) bool {
	sfu.mut.Lock()
	logger.LogWithMessage(NameSFU, logger.MutLock, true, true, "func=AddVirtualClient")
	defer sfu.mut.Unlock()
	defer logger.LogWithMessage(NameSFU, logger.MutUnlock, true, true, "func=AddVirtualClient")
	provider := sfu.remoteProviders[msg.ProviderKey]
	if provider == nil {
		fmt.Printf("SFU: AddVirtualClient: No such provider %s\n", msg.ProviderKey)
		return false
	}
	sfu.virtualClients[msg.ClientID] = msg.ProviderKey
	provider.AddVirtualClient(msg.ClientID, msg.VideoTracks, msg.AudioTracks)
	// Alert Session Manager Virtual client was added
	return true
}

func (sfu *SFU) SubscribeRemoteProviderToClient(provider ProviderConnection, clientID uint, videoTracks []TrackSimple, audioTracks []TrackSimple) {
	//sfu.mut.Lock()
	//logger.LogWithMessage(NameSFU, logger.MutLock, true, true, "func=SubscribeRemoteProviderToClient")
	//defer sfu.mut.Unlock()
	//defer logger.LogWithMessage(NameSFU, logger.MutUnlock, true, true, "func=SubscribeRemoteProviderToClient")
	client := sfu.clients[clientID]
	if client == nil {
		fmt.Printf("SFU: SubscribeRemoteProviderToClient: No such client %d\n", clientID)
		return
	}
	for _, t := range videoTracks {
		track, exists := client.SenderVideoTracks[t.TrackID]
		if !exists {
			fmt.Printf("SFU: SubscribeRemoteProviderToClient: Client %d has no video track %s\n", clientID, t.TrackID)
			return
		}
		recvTrack := &ReceiverTrack{
			TrackID:                  track.TrackID,
			OriginType:               "sfu",
			OriginID:                 0, //TODO
			SenderTrackID:            track.TrackID,
			CorrespondingSenderTrack: track,
		}
		// TODO Maybe we need to store this mapping somewhere to be able to remove it again
		if err := provider.AddTrackFromOtherUnsafe(recvTrack); err != nil {
			fmt.Printf("SFU: SubscribeRemoteProviderToClient: Failed to add track %s: %v\n", recvTrack.TrackID, err)
		}
	}
	for _, t := range audioTracks {
		track, exists := client.SenderAudioTracks[t.TrackID]
		if !exists {
			fmt.Printf("SFU: SubscribeRemoteProviderToClient: Client %d has no audio track %s\n", clientID, t.TrackID)
			return
		}
		recvTrack := &ReceiverTrack{
			TrackID:                  track.TrackID,
			OriginType:               "sfu",
			OriginID:                 0, //TODO
			SenderTrackID:            track.TrackID,
			CorrespondingSenderTrack: track,
		}
		// TODO Maybe we need to store this mapping somewhere to be able to remove it again
		if err := provider.AddTrackFromOtherUnsafe(recvTrack); err != nil {
			fmt.Printf("SFU: SubscribeRemoteProviderToClient: Failed to add track %s: %v\n", recvTrack.TrackID, err)
		}
	}

}

func (sfu *SFU) StartPerformingQualityAdaptation() {
	go func() {
		for {
			sfu.mut.Lock()
			output := fmt.Sprintf("ts=%d stats=[", time.Now().UnixMilli())
			for _, client := range sfu.clients {
				if client.BandwidthEstimator != nil {
					targetBitrate := client.BandwidthEstimator.GetTargetBitrate()
					avgLoss := client.BandwidthEstimator.GetStats()["averageLoss"]
					delayBitrate := client.BandwidthEstimator.GetStats()["delayTargetBitrate"]
					lossBitrate := client.BandwidthEstimator.GetStats()["lossTargetBitrate"]
					delayMeasurement := client.BandwidthEstimator.GetStats()["delayMeasurement"]
					delayEstimate := client.BandwidthEstimator.GetStats()["delayEstimate"]
					delayThreshold := client.BandwidthEstimator.GetStats()["delayThreshold"]
					usage := client.BandwidthEstimator.GetStats()["usage"]
					state := client.BandwidthEstimator.GetStats()["state"]
					extraOutput := ""
					if sfu.qualityAdaptation != nil {
						extraOutput = "@activeTracks=("
						tracks := sfu.qualityAdaptation.PerformAdaptation(client, targetBitrate)
						for _, trackID := range tracks {
							extraOutput += fmt.Sprintf("%s|", trackID)
						}
						extraOutput += ")"
					}
					output += fmt.Sprintf("client=%d@bitrate=%d@avgLoss=%.5f@delayBitrate=%d@lossBitrate=%d@delayMeasurement=%.2f@delayEstimate=%.2f@delayThreshold=%.2f@usage=%s@state=%s%s;", client.clientID, targetBitrate, avgLoss, delayBitrate, lossBitrate, delayMeasurement, delayEstimate, delayThreshold, usage, state, extraOutput)
				}
			}
			output += "]"
			logger.LogWithMessage(NameSFU, logger.EstimatedBitrate, true, true, output)
			sfu.mut.Unlock()
			time.Sleep(time.Second * 1)
		}
	}()
}
