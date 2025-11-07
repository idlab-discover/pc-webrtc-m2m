package main

import (
	"encoding/json"
	"fmt"
	"goweb/shared/src/logger"
	"goweb/shared/src/metrics"
	"goweb/shared/src/packet"
	"net"
	"strings"
	"sync"
	"sync/atomic"

	"github.com/pion/interceptor"
	"github.com/pion/interceptor/pkg/cc"
	"github.com/pion/sdp/v3"
	"github.com/pion/webrtc/v4"
)

const NameClientConnection = "ClientConnection"

type SenderTrack struct {
	TrackID      string `json:"trackID"`
	trackBitrate *TrackBitrate
	WebRTCTrack  *webrtc.TrackLocalStaticRTP
	trackMeter   *metrics.TrackMeter
}

type ReceiverTrack struct {
	IsPaused				 bool   
	TrackID                  string `json:"trackID"`
	OriginType               string `json:"originType"` // User, SFU etc...
	OriginID                 uint   `json:"originID"`   // userID, SFU_ID etc...
	SenderTrackID            string `json:"senderTrackID"`
	CorrespondingSenderTrack *SenderTrack
	RTPSender                *webrtc.RTPSender
	PauseTrack                *webrtc.TrackLocalStaticRTP
}

// TODO Maybe better error handling is needed here
func (rt *ReceiverTrack) Pause() error {
	if rt.IsPaused {
		return nil
	}
	rt.IsPaused = true
	return rt.RTPSender.ReplaceTrack(rt.PauseTrack)
}

func (rt *ReceiverTrack) Play() error {
	if !rt.IsPaused {
		return nil
	}
	rt.IsPaused = false
	return rt.RTPSender.ReplaceTrack(rt.CorrespondingSenderTrack.WebRTCTrack)
}

type TrackBitrate struct {
	avgRate                 uint64
	counters                []uint32
	currentCounter          uint32
	currentCounterMax       uint32
	currentCounterCompleted uint32
	tempCounter             uint32
}

type ClientConnection struct {
	parent         *SFU
	NeedsUpdate    bool
	IsNegotiating  bool
	clientID       uint
	authKey        string
	peerConnection *webrtc.PeerConnection

	websocket *ThreadSafeWebsocket

	nActiveTracks       int
	BandwidthEstimator  cc.BandwidthEstimator
	SenderVideoTracks   map[string]*SenderTrack
	SenderAudioTracks   map[string]*SenderTrack
	ReceiverVideoTracks map[string]*ReceiverTrack
	ReceiverAudioTracks map[string]*ReceiverTrack

	camInfo *cameraInfo // TODO Provider requests camera info => callback invocation for localClient (need to ensure that this is async though!) maybe also add a function to send data async?

	pendingCandidatesString []string

	gatherTrackStats bool

	mut sync.Mutex
}

type ClientCandidate struct {
	Message string `json:"candidate"`
}

func NewClientConnection(parent *SFU, clientID uint, authKey string,
	senderVideoTracks []SenderTrack,
	senderAudioTracks []SenderTrack,
) *ClientConnection {
	logger.LogWithMessage(NameClientConnection, logger.Creating, true, true, fmt.Sprintf("clientID=%d authKey=%s", clientID, authKey))
	videoTracksMap := make(map[string]*SenderTrack)
	audioTracksMap := make(map[string]*SenderTrack)

	videoRTCPFeedback := []webrtc.RTCPFeedback{
		{Type: "goog-remb", Parameter: ""},
		{Type: "ccm", Parameter: "fir"},
		{Type: "nack", Parameter: ""},
		{Type: "nack", Parameter: "pli"},
	}

	codecCapability := webrtc.RTPCodecCapability{
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

	for i := range senderVideoTracks {
		tempTrack := senderVideoTracks[i]
		trackLocal, err := webrtc.NewTrackLocalStaticRTP(codecCapability, tempTrack.TrackID, tempTrack.TrackID)
		if err != nil {
			panic(err)
		}
		meter := parent.overallTrackMetrics.AddTrackMeter(tempTrack.TrackID)
		videoTracksMap[tempTrack.TrackID] = &SenderTrack{
			TrackID:      tempTrack.TrackID,
			trackBitrate: &TrackBitrate{}, /*TODO Make constructor*/
			WebRTCTrack:  trackLocal,
			trackMeter:   meter,
		}

	}
	for i := range senderAudioTracks {
		tempTrack := senderAudioTracks[i]
		trackLocal, err := webrtc.NewTrackLocalStaticRTP(audioCodecCapability, tempTrack.TrackID, tempTrack.TrackID)
		if err != nil {
			panic(err)
		}
		audioTracksMap[tempTrack.TrackID] = &SenderTrack{
			TrackID:      tempTrack.TrackID,
			trackBitrate: &TrackBitrate{}, /*TODO Make constructor*/
			WebRTCTrack:  trackLocal,
		}
	}
	cl := &ClientConnection{
		parent:              parent,
		clientID:            clientID,
		authKey:             authKey,
		SenderVideoTracks:   videoTracksMap,
		SenderAudioTracks:   audioTracksMap,
		ReceiverVideoTracks: make(map[string]*ReceiverTrack),
		ReceiverAudioTracks: make(map[string]*ReceiverTrack),
		mut:                 sync.Mutex{},
	}
	logger.LogWithMessage(NameClientConnection, logger.Created, true, true, fmt.Sprintf("clientID=%d authKey=%s", clientID, authKey))
	return cl
}

func (clc *ClientConnection) SetupPeerConnection(sfuSettings *SFUSettings) {
	logger.LogWithMessage(NameClientConnection, logger.ClientAddingTransceivers, true, true,
		fmt.Sprintf("clientID=%d nVideoTrack=%d nAudioTracks=%d",
			clc.clientID, len(clc.SenderVideoTracks), len(clc.SenderAudioTracks)),
	)
	mediaEngine := SetupDefaultMediaEngine()
	interceptorRegistry := &interceptor.Registry{}

	clc.gatherTrackStats = sfuSettings.UseABR || sfuSettings.GatherTrackStats

	if sfuSettings.UseCC {
		congestionController, err := CreateBandwidthEstimator(clc, sfuSettings)
		if err != nil {
			panic(err)
		}
		interceptorRegistry.Add(congestionController)
	}
	if err := webrtc.ConfigureTWCCHeaderExtensionSender(mediaEngine, interceptorRegistry); err != nil {
		panic(err)
	}
	if err := webrtc.RegisterDefaultInterceptors(mediaEngine, interceptorRegistry); err != nil {
		panic(err)
	}
	settingEngine2 := webrtc.SettingEngine{}
	settingEngine2.SetSCTPMaxReceiveBufferSize(16 * 1024 * 1024)
	settingEngine2.SetReceiveMTU(1500)
	if clc.parent.ipFilter != "" {
		settingEngine2.SetIPFilter(clc.ipFilterFunc)
	}
	peerConnection, err := webrtc.NewAPI(webrtc.WithSettingEngine(settingEngine2), webrtc.WithMediaEngine(mediaEngine), webrtc.WithInterceptorRegistry(interceptorRegistry)).NewPeerConnection(webrtc.Configuration{})
	clc.peerConnection = peerConnection
	clc.AddPeerConnectionCallbacks()
	if err != nil {
		panic(err)
	}

	for i := 0; i < len(clc.SenderVideoTracks); i++ {
		if _, err := peerConnection.AddTransceiverFromKind(webrtc.RTPCodecTypeVideo, webrtc.RTPTransceiverInit{
			Direction: webrtc.RTPTransceiverDirectionRecvonly,
		}); err != nil {
			fmt.Printf("WebRTCSFU: webSocketHandler: ERROR: %s\n", err)
			return
		}
	}

	for i := 0; i < len(clc.SenderAudioTracks); i++ {
		if _, err := peerConnection.AddTransceiverFromKind(webrtc.RTPCodecTypeAudio, webrtc.RTPTransceiverInit{
			Direction: webrtc.RTPTransceiverDirectionRecvonly,
		}); err != nil {
			fmt.Printf("WebRTCSFU: webSocketHandler: ERROR: %s\n", err)
			return
		}
	}
	if len(clc.SenderVideoTracks) == 0 && len(clc.SenderAudioTracks) == 0 {
		if _, err := peerConnection.AddTransceiverFromKind(webrtc.RTPCodecTypeVideo, webrtc.RTPTransceiverInit{
			Direction: webrtc.RTPTransceiverDirectionRecvonly,
		}); err != nil {
			fmt.Printf("WebRTCSFU: webSocketHandler: ERROR: %s\n", err)
			return
		}
	}
	logger.LogWithMessage(NameClientConnection, logger.ClientAddedTransceivers, true, true, fmt.Sprintf("clientID=%d", clc.clientID))
}

func (clc *ClientConnection) SetupWebsocket(ws *ThreadSafeWebsocket) {
	clc.websocket = ws
	clc.startListening()
}

func (clc *ClientConnection) AddPeerConnectionCallbacks() {
	clc.peerConnection.OnICECandidate(func(i *webrtc.ICECandidate) {
		if i == nil {
			return
		}
		fmt.Println("WebRTCSFU: webSocketHandler: OnICECandidate: Found a candidate")
		clc.websocket.WriteJSONMessageSafe("CandidateMessage", i.ToJSON().Candidate)
	})

	// If PeerConnection is closed remove it from global list
	clc.peerConnection.OnConnectionStateChange(func(p webrtc.PeerConnectionState) {
		// TODO Maybe inform sfu/session manager
		logger.LogWithMessage(NameClientConnection, logger.SFUClientConnectionChange, true, true,
			fmt.Sprintf("clientID=%d state=%s", clc.clientID, p.String()))
		switch p {
		case webrtc.PeerConnectionStateFailed:
			if err := clc.peerConnection.Close(); err != nil {
				fmt.Printf("WebRTCSFU: webSocketHandler: ERROR: %s\n", err)
			}
		case webrtc.PeerConnectionStateClosed:
			// Alert other clients that this client is gone
		case webrtc.PeerConnectionStateConnected:

		}
	})

	clc.peerConnection.OnTrack(func(t *webrtc.TrackRemote, trackReceiver *webrtc.RTPReceiver) {
		// Create a track to fan out our incoming video to all peers
		//if t.Kind() == webrtc.RTPCodecTypeAudio {
		//	return
		//}

		logger.LogWithMessage(NameClientConnection, logger.ClientOnTrackCalled, true, true,
			fmt.Sprintf("clientID=%d trackID=%s streamID=%s", clc.clientID, t.ID(), t.StreamID()))
		go func() {
			rtcpBuf := make([]byte, 1500)
			for {
				if _, _, rtcpErr := trackReceiver.Read(rtcpBuf); rtcpErr != nil {
					//panic(rtcpErr)
					// TODO Add some cleanup here
				}
			}
		}()
		var senderTrack *SenderTrack
		exists := false
		clc.mut.Lock()
		if t.Kind() == webrtc.RTPCodecTypeVideo {
			senderTrack, exists = clc.SenderVideoTracks[t.ID()]
		} else if t.Kind() == webrtc.RTPCodecTypeAudio {
			senderTrack, exists = clc.SenderAudioTracks[t.ID()]
		}

		if !exists {
			clc.mut.Unlock()
			fmt.Printf("WebRTCSFU: OnTrack: No sender track found for track ID %s\n", t.ID())
			return
		}
		trackBitrate := &TrackBitrate{
			counters: make([]uint32, 20),
		}
		senderTrack.trackBitrate = trackBitrate
		trackLocal := senderTrack.WebRTCTrack
		clc.mut.Unlock()

		fmt.Printf("WebRTCSFU: OnTrack: Adding track %v\n", trackLocal.ID())

		//startTime := time.Now().UnixNano() // / int64(time.Millisecond)
		//prevBucket := int64(0)
		frames := make(map[uint32]uint32)
		nDroppedFrames := uint32(0)
		completedFrame := false
		for {

			buf := make([]byte, 15000)
			i, _, err := t.Read(buf)
			if err != nil {
				fmt.Printf("WebRTCSFU: OnTrack: error during read: %s\n", err)
				break
			}
			atomic.AddUint64(&senderTrack.trackMeter.Bytes, uint64(i))
			p := packet.BytesToFramePacketHeader(buf[20:])

			if frames[p.FrameNr] == 0 { // Can maybe be optimized more because of the string being created for no reason
				if !completedFrame { // Previous frame was not completed
					nDroppedFrames++
				}
				completedFrame = false
				logger.LogFrameWithMessage(NameClientConnection, logger.FrameFirstPacketRecv, true, true, fmt.Sprintf("clientID=%d trackID=%s", clc.clientID, t.ID()), uint(p.FrameNr))
			}
			frames[p.FrameNr] += p.SeqLen

			/*if clc.gatherTrackStats && t.Kind() == webrtc.RTPCodecTypeVideo {
				nextTime := time.Now().UnixNano() //
				nsDiff := nextTime - startTime
				msBucket := nsDiff / int64(50*time.Millisecond)
				// Todo implement concurrency safety => get pointer to trackbitrates once!
				if msBucket != int64(prevBucket) {
					trackBitrate.currentCounterCompleted = trackBitrate.currentCounter
					trackBitrate.counters[trackBitrate.currentCounter] = trackBitrate.tempCounter
					trackBitrate.currentCounter = (trackBitrate.currentCounter + 1) % 20
					trackBitrate.currentCounterMax++
					trackBitrate.tempCounter = 0
				}
				trackBitrate.tempCounter += uint32(i)
				prevBucket = msBucket
			}*/
			if frames[p.FrameNr] == p.FrameLen { // Can maybe be optimized more because of the string being created for no reason
				completedFrame = true
				logger.LogFrameWithMessage(NameClientConnection, logger.FrameFullyRecv, true, true, fmt.Sprintf("clientID=%d trackID=%s totalDroppedFrames=%d", clc.clientID, t.ID(), nDroppedFrames), uint(p.FrameNr))
			}
			//go func() {
				if _, err = trackLocal.Write(buf[:i]); err != nil {
					fmt.Printf("WebRTCSFU: OnTrack: error during write: %s\n", err)
				}

			//}()

		}
	})
}

func (clc *ClientConnection) AddTrackFromOther(originType string, originID uint, trackID string, track *SenderTrack) {
	clc.mut.Lock()
	defer clc.mut.Unlock()
	clc.AddTrackFromOtherUnsafe(originType, originID, trackID, track)

}

func (clc *ClientConnection) AddTrackFromOtherUnsafe(originType string, originID uint, trackID string, track *SenderTrack) {
	// No locking, must be called with caution
	logger.LogWithMessage(NameClientConnection, logger.ClientAddingTrackFromOther, true, true,
		fmt.Sprintf("clientID=%d originType=%s originID=%d trackID=%s",
			clc.clientID, originType, originID, trackID))
	// TODO Check if trackID appears from session manager track
	recvTrack := &ReceiverTrack{
		TrackID:    trackID,
		OriginType: originType,
		OriginID:   originID,
	}
	if track.WebRTCTrack.Kind() == webrtc.RTPCodecTypeVideo {
		clc.ReceiverVideoTracks[trackID] = recvTrack
	} else if track.WebRTCTrack.Kind() == webrtc.RTPCodecTypeAudio {
		clc.ReceiverAudioTracks[trackID] = recvTrack
	}
	println("ADDING TRACK", track == nil)
	fmt.Printf("TrackID=%s streamID=%s\n", track.WebRTCTrack.ID(), track.WebRTCTrack.StreamID())
	pauseTrack, _ :=webrtc.NewTrackLocalStaticRTP(track.WebRTCTrack.Codec(), track.WebRTCTrack.ID(), track.WebRTCTrack.StreamID())
	rtpSender, err := clc.peerConnection.AddTrack(pauseTrack)

	if err != nil {
		println("OOPSSS")
		// TODO error handling
		return
	}
	recvTrack.CorrespondingSenderTrack = track
	recvTrack.RTPSender = rtpSender
	recvTrack.PauseTrack = pauseTrack
	go func() {
		rtcpBuf := make([]byte, 1500)
		for {
			if _, _, err := rtpSender.Read(rtcpBuf); err != nil {
				panic(err)
				return
			}
		}
		// TODO Handle track closure?
	}()
	println("track added")
}

func (clc *ClientConnection) SignalRenegotiation() {
	clc.mut.Lock()
	defer clc.mut.Unlock()
	clc.SignalRenegotiationUnsafe()
}

func (clc *ClientConnection) SignalRenegotiationUnsafe() {
	logger.LogWithMessage(NameClientConnection, logger.ClientSignalRenegotiation, true, true, fmt.Sprintf("clientID=%d", clc.clientID))

	if clc.websocket == nil {
		return
	}
	if clc.IsNegotiating {
		clc.NeedsUpdate = true
		return
	}
	clc.IsNegotiating = true
	offer, err := clc.peerConnection.CreateOffer(nil)
	if err != nil {
		panic(err)
	}

	if err = clc.peerConnection.SetLocalDescription(offer); err != nil {
		panic(err)
	}
	//fmt.Printf("WebRTCSFU: webSocketHandler: SignalRenegotiation: Sending offer to clientID=%d %v+\n", clc.clientID, offer)
	if err = clc.websocket.WriteJSONMessageSafe("OfferMessage", offer); err != nil {
		panic(err)
	}
	clc.NeedsUpdate = false
}

func (clc *ClientConnection) startListening() {
	go func() {
		for {
			var msg ClientMessage
			if err := clc.websocket.ReadJSON(&msg); err != nil {
				fmt.Printf("WebRTCSFU: webSocketHandler: ReadMessage: error %w\n", err)
				break
			}
			logger.LogWithMessage(NameClientConnection, logger.ReceivedWSMessage, true, true,
				fmt.Sprintf("origin=client clientID=%d type=%s", clc.clientID, msg.MessageType),
			)
			switch msg.MessageType {
			case "AnswerMessage":
				clc.handleAnswerMessage(msg.Message)
			case "CandidateMessage":
				clc.handleCandidateMessage(msg.Message)
			case "SubscribeToTracksMessage":
				clc.handleSubscribeToTracksMessage(msg.Message)
			case "SubscribeToRemoteClientsMessage":
				clc.handleSubscribeToRemoteClientsMessage(msg.Message)
			}
		}
	}()
}

func (clc *ClientConnection) handleAnswerMessage(payload json.RawMessage) {
	answer := webrtc.SessionDescription{}

	if err := json.Unmarshal(payload, &answer); err != nil {
		fmt.Printf("failed to unmarshal payload: %v\n", err)
		return
	}
	clc.mut.Lock()
	defer clc.mut.Unlock()
	if err := clc.peerConnection.SetRemoteDescription(answer); err != nil {
		panic(err)
	}

	for _, c := range clc.pendingCandidatesString {
		if candidateErr := clc.peerConnection.AddICECandidate(webrtc.ICECandidateInit{Candidate: c}); candidateErr != nil {
			panic(candidateErr)
		}
	}
	clc.IsNegotiating = false
	if clc.NeedsUpdate {
		println("NEED RENEGGGGG")
		clc.SignalRenegotiationUnsafe()
	} else {
		println("DONT NEED NEEG")
		for _, t := range clc.ReceiverVideoTracks {
			t.Pause()
		}
	}
}

func (clc *ClientConnection) handleCandidateMessage(payload json.RawMessage) {
	clc.mut.Lock()
	defer clc.mut.Unlock()
	desc := clc.peerConnection.RemoteDescription()
	var candidate string
	if err := json.Unmarshal(payload, &candidate); err != nil {
		panic(err)
	}

	if desc == nil {
		clc.pendingCandidatesString = append(clc.pendingCandidatesString, candidate)
	} else {
		if candidateErr := clc.peerConnection.AddICECandidate(webrtc.ICECandidateInit{Candidate: candidate}); candidateErr != nil {
			panic(candidateErr)
		}
	}
	println("Added candidate")
}

type SubscribeToTracksMessage struct {
	ClientID    uint          `json:"clientID"`
	VideoTracks []TrackSimple `json:"videoTracks"`
	AudioTracks []TrackSimple `json:"audioTracks"`
}

func (clc *ClientConnection) handleSubscribeToTracksMessage(payload json.RawMessage) {
	var msg SubscribeToTracksMessage
	if err := json.Unmarshal(payload, &msg); err != nil {
		fmt.Printf("failed to unmarshal payload: %v\n", err)
		return
	}
	fmt.Printf("Received SubscribeToTracksMessage: %+v\n", msg)
	clc.parent.mut.Lock()
	defer clc.parent.mut.Unlock()
	otherC := clc.parent.clients[msg.ClientID]
	if otherC == nil {
		fmt.Printf("WebRTCSFU: handleSubscribeToTracksMessage: No client with ID %d found\n", msg.ClientID)
		return
	}
	clc.subscribeToTracks(msg, otherC)
	clc.SignalRenegotiation()
	for _, t := range clc.ReceiverVideoTracks {
			t.Pause()
}
	// TODO
	// Renegotiate SDP
}

type SubscribeToRemoteClientsMessage struct {
	Clients []SubscribeToTracksMessage `json:"clients"`
}

func (clc *ClientConnection) handleSubscribeToRemoteClientsMessage(payload json.RawMessage) {
	var msg SubscribeToRemoteClientsMessage
	if err := json.Unmarshal(payload, &msg); err != nil {
		fmt.Printf("failed to unmarshal payload: %v\n", err)
		return
	}
	fmt.Printf("Received SubscribeToRemoteClientsMessage %d: %+v\n", clc.clientID, msg)
	clc.parent.mut.Lock()
	defer clc.parent.mut.Unlock()
	for _, sub := range msg.Clients {
		otherC := clc.parent.clients[sub.ClientID]
		if otherC != nil {
			clc.subscribeToTracks(sub, otherC)
			continue
		}
		otherVirtualC := clc.parent.virtualClients[sub.ClientID]
		if otherVirtualC != "" {
			provider, exists := clc.parent.remoteProviders[otherVirtualC]
			if exists {
				if err := provider.ForwardTracksToClient(clc, sub, sub.ClientID); err != nil {
					fmt.Printf("WebRTCSFU: handleSubscribeToRemoteClientsMessage: Error forwarding tracks from provider %s to clientID=%d: %s\n", otherVirtualC, clc.clientID, err)
				}
			} else {
				fmt.Printf("WebRTCSFU: handleSubscribeToRemoteClientsMessage: No provider with key %s found for virtual clientID=%d\n", otherVirtualC, sub.ClientID)
			}
		}
	}

	fmt.Printf("WebRTCSFU: handleSubscribeToRemoteClientsMessage: Signaling renegotiation for clientID=%d\n", clc.clientID)
	clc.SignalRenegotiation()

	fmt.Printf("WebRTCSFU: handleSubscribeToRemoteClientsMessage: Signaling renegotiation done for clientID=%d\n", clc.clientID)
	// TODO
	// Renegotiate SDP
}

func (clc *ClientConnection) subscribeToTracks(subMessage SubscribeToTracksMessage, otherClient *ClientConnection) {
	// Prevent deadlock CL_A locks CL_B, CL_B locks CL_A, CL_A waits to lock CL_A
	if clc.clientID > otherClient.clientID {
		clc.mut.Lock()
		otherClient.mut.Lock()
	} else {
		otherClient.mut.Lock()
		clc.mut.Lock()
	}
	defer clc.mut.Unlock()
	defer otherClient.mut.Unlock()

	for _, t := range subMessage.VideoTracks {
		track, exists := otherClient.SenderVideoTracks[t.TrackID]
		if !exists {
			continue
		}
		clc.AddTrackFromOtherUnsafe("client", otherClient.clientID, t.TrackID, track)
	}
	for _, t := range subMessage.AudioTracks {
		track, exists := otherClient.SenderAudioTracks[t.TrackID]
		if !exists {
			continue
		}
		clc.AddTrackFromOtherUnsafe("client", otherClient.clientID, t.TrackID, track)
	}

}

func (clc *ClientConnection) updateCamInfo(data string) {
	data = strings.ReplaceAll(data, ",", ".")
	tokens := strings.Split(data, ";")
	if len(tokens) == 36 {
		clc.camInfo.init = true
		clc.camInfo.camMatrix = FillMatrix(tokens, 0)
		clc.camInfo.projectionMatrix = FillMatrix(tokens, 16)
		clc.camInfo.position = FillPosition(tokens, 32)
	}
}

func (clc *ClientConnection) Dispose() {
	clc.peerConnection.Close()
	clc.websocket.Close()
}

func SetupDefaultMediaEngine() *webrtc.MediaEngine {
	mediaEngine := &webrtc.MediaEngine{}

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
	return mediaEngine
}

func (clc *ClientConnection) ipFilterFunc(addr net.IP) bool {
	logger.LogWithMessage(NameClientConnection, logger.IPFilterCheck, true, true, fmt.Sprintf("clientID=%d ip=%s filter=%s", clc.clientID, addr.String(), clc.parent.ipFilter))
	if strings.HasPrefix(addr.String(), clc.parent.ipFilter) {
		return true
	}
	return false
}
