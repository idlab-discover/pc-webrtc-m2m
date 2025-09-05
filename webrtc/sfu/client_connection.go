package main

import (
	"encoding/json"
	"fmt"
	"strings"
	"sync"
	"time"

	"github.com/pion/interceptor"
	"github.com/pion/interceptor/pkg/cc"
	"github.com/pion/sdp/v3"
	"github.com/pion/webrtc/v3"
)

const NameClientConnection = "ClientConnection"

type SenderTrack struct {
	TrackID      string `json:"trackID"`
	trackBitrate *TrackBitrate
	WebRTCTrack  webrtc.TrackLocal
}

type ReceiverTrack struct {
	TrackID                  string `json:"trackID"`
	OriginType               string `json:"originType"` // User, SFU etc...
	OriginID                 uint   `json:"originID"`   // userID, SFU_ID etc...
	SenderTrackID            string `json:"senderTrackID"`
	CorrespondingSenderTrack webrtc.TrackLocal
	RTPSender                *webrtc.RTPSender
}

// TODO Maybe better error handling is needed here
func (rt *ReceiverTrack) Pause() error {
	if rt.RTPSender.Track() == nil {
		return nil
	}
	return rt.RTPSender.ReplaceTrack(nil)
}

func (rt *ReceiverTrack) Play() error {
	if rt.RTPSender.Track() != nil {
		return nil
	}
	return rt.RTPSender.ReplaceTrack(rt.CorrespondingSenderTrack)
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
	NeedsUpdate    bool
	clientID       uint
	authKey        string
	peerConnection *webrtc.PeerConnection
	websocket      *ThreadSafeWebsocket

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

func NewClientConnection(clientID uint, authKey string,
	senderVideoTracks []SenderTrack,
	senderAudioTracks []SenderTrack,
) *ClientConnection {
	LogWithMessage(NameClientConnection, Creating, true, true, fmt.Sprintf("clientID=%d authKey=%s", clientID, authKey))
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
		videoTracksMap[tempTrack.TrackID] = &SenderTrack{
			TrackID:      tempTrack.TrackID,
			trackBitrate: &TrackBitrate{}, /*TODO Make constructor*/
			WebRTCTrack:  trackLocal,
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
		clientID:            clientID,
		authKey:             authKey,
		SenderVideoTracks:   videoTracksMap,
		SenderAudioTracks:   audioTracksMap,
		ReceiverVideoTracks: make(map[string]*ReceiverTrack),
		ReceiverAudioTracks: make(map[string]*ReceiverTrack),
		mut:                 sync.Mutex{},
	}
	LogWithMessage(NameClientConnection, Created, true, true, fmt.Sprintf("clientID=%d authKey=%s", clientID, authKey))
	return cl
}

func (clc *ClientConnection) SetupPeerConnection(sfuSettings *SFUSettings) {
	LogWithMessage(NameClientConnection, ClientAddingTransceivers, true, true,
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

	peerConnection, err := webrtc.NewAPI(webrtc.WithSettingEngine(settingEngine), webrtc.WithMediaEngine(mediaEngine), webrtc.WithInterceptorRegistry(interceptorRegistry)).NewPeerConnection(webrtc.Configuration{})
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
	LogWithMessage(NameClientConnection, ClientAddedTransceivers, true, true, fmt.Sprintf("clientID=%d", clc.clientID))
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
		LogWithMessage(NameClientConnection, SFUClientConnectionChange, true, true,
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

	clc.peerConnection.OnTrack(func(t *webrtc.TrackRemote, _ *webrtc.RTPReceiver) {
		// Create a track to fan out our incoming video to all peers
		//if t.Kind() == webrtc.RTPCodecTypeAudio {
		//	return
		//}
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
		clc.mut.Unlock()
		trackLocal := addTrack(t)
		fmt.Printf("WebRTCSFU: OnTrack: Adding track %v\n", trackLocal.ID())
		defer func() {
			fmt.Printf("WebRTCSFU: OnTrack: removing track %v\n", trackLocal.ID())
			removeTrack(trackLocal)
		}()

		startTime := time.Now().UnixNano() // / int64(time.Millisecond)
		prevBucket := int64(0)
		for {
			buf := make([]byte, 1500)
			i, _, err := t.Read(buf)
			if err != nil {
				fmt.Printf("WebRTCSFU: OnTrack: error during read: %s\n", err)
				break
			}
			if clc.gatherTrackStats && t.Kind() == webrtc.RTPCodecTypeVideo {
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
			}
			go func() {
				if _, err = trackLocal.Write(buf[:i]); err != nil {
					fmt.Printf("WebRTCSFU: OnTrack: error during write: %s\n", err)
				}
			}()

		}
	})
}

func (clc *ClientConnection) AddTrackFromOther(originType string, originID uint, trackID string, track webrtc.TrackLocal) {
	clc.mut.Lock()
	defer clc.mut.Unlock()
	LogWithMessage(NameClientConnection, ClientAddingTrackFromOther, true, true,
		fmt.Sprintf("clientID=%d originType=%s originID=%d trackID=%s",
			clc.clientID, originType, originID, trackID))
	// TODO Check if trackID appears from session manager track
	var recvTrack *ReceiverTrack
	var exists bool
	if track.Kind() == webrtc.RTPCodecTypeVideo {
		recvTrack, exists = clc.ReceiverVideoTracks[trackID]
	} else if track.Kind() == webrtc.RTPCodecTypeAudio {
		recvTrack, exists = clc.ReceiverAudioTracks[trackID]
	}
	if !exists {
		// TODO Error logging
		return
	}
	if recvTrack.OriginType != originType || recvTrack.OriginID != originID {
		// TODO Error logging
		return
	}

	rtpSender, err := clc.peerConnection.AddTrack(track)
	if err != nil {
		// TODO error handling
		return
	}
	recvTrack.CorrespondingSenderTrack = track
	recvTrack.RTPSender = rtpSender
	go func() {
		rtcpBuf := make([]byte, 1500)
		for {
			if _, _, err := rtpSender.Read(rtcpBuf); err != nil {
				return
			}
		}
		// TODO Handle track closure?
	}()

}

func (clc *ClientConnection) SignalRenegotiation() {
	clc.mut.Lock()
	defer clc.mut.Unlock()
	clc.NeedsUpdate = true
	if clc.websocket == nil {
		return
	}

	offer, err := clc.peerConnection.CreateOffer(nil)
	if err != nil {
		panic(err)
	}

	if err = clc.peerConnection.SetLocalDescription(offer); err != nil {
		panic(err)
	}
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
			LogWithMessage(NameManagerConnection, ReceivedWSMessage, true, true,
				fmt.Sprintf("origin=client clientID=%d type=%s", clc.clientID, msg.MessageType),
			)
			switch msg.MessageType {
			case "AnswerMessage":
				clc.handleAnswerMessage(msg.Message)
			case "CandidateMessage":
				clc.handleCandidateMessage(msg.Message)
				// answer
				/*case 3:
					answer := webrtc.SessionDescription{}
					if err := json.Unmarshal(msg.Message, &answer); err != nil {
						panic(err)
					}
					if err := clc.peerConnection.SetRemoteDescription(answer); err != nil {
						panic(err)
					}

					for _, c := range clc.pendingCandidatesString {
						if candidateErr := clc.peerConnection.AddICECandidate(webrtc.ICECandidateInit{Candidate: c}); candidateErr != nil {
							panic(candidateErr)
						}
					}
				// candidate
				case 4:
					candidate := ClientCandidate{}
					if err := json.Unmarshal(msg.Message, &candidate); err != nil {
						panic(err)
					}
					desc := clc.peerConnection.RemoteDescription()
					if desc == nil {
						clc.pendingCandidatesString = append(clc.pendingCandidatesString, candidate.Message)
					} else {
						if candidateErr := clc.peerConnection.AddICECandidate(webrtc.ICECandidateInit{Candidate: candidate.Message}); candidateErr != nil {
							panic(candidateErr)
						}
					}
				// remove track
				case 5:
					//removeTrackforPeer(pcState, message)
				// add track
				case 6:
					//addTrackforPeer(pcState, message)
				case 7:
					candidate := ClientCandidate{}
					if err := json.Unmarshal(msg.Message, &candidate); err != nil {
						panic(err)
					}
					clc.updateCamInfo(candidate.Message)*/
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

	if err := clc.peerConnection.SetRemoteDescription(answer); err != nil {
		panic(err)
	}

	for _, c := range clc.pendingCandidatesString {
		if candidateErr := clc.peerConnection.AddICECandidate(webrtc.ICECandidateInit{Candidate: c}); candidateErr != nil {
			panic(candidateErr)
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
