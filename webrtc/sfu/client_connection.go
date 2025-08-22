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

type SenderTrack struct {
	TrackID      string `json:"trackID"`
	trackBitrate *TrackBitrate
	WebRTCTrack  webrtc.TrackLocal
}

type ReceiverTrack struct {
	TrackID                  string `json:"trackID"`
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
	clientID       uint
	authKey        string
	peerConnection *webrtc.PeerConnection
	websocket      *threadSafeWriter

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

type ClientMessage struct {
	MessageType uint32          `json:"messageType"`
	Message     json.RawMessage `json:"message"`
}

type ClientCandidate struct {
	Message string `json:"candidate"`
}

func NewClientConnection(clientID uint, authKey string) *ClientConnection {
	return &ClientConnection{
		clientID:            clientID,
		authKey:             authKey,
		SenderVideoTracks:   make(map[string]*SenderTrack),
		SenderAudioTracks:   make(map[string]*SenderTrack),
		ReceiverVideoTracks: make(map[string]*ReceiverTrack),
		ReceiverAudioTracks: make(map[string]*ReceiverTrack),
		mut:                 sync.Mutex{},
	}
}

func (clc *ClientConnection) SetupPeerConnection(sfuSettings *SFUSettings, ws *threadSafeWriter) {
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

}

func (clc *ClientConnection) AddPeerConnectionCallbacks() {
	clc.peerConnection.OnICECandidate(func(i *webrtc.ICECandidate) {
		if i == nil {
			return
		}
		payload := i.ToJSON().Candidate
		candidate := ClientCandidate{
			Message: payload,
		}
		msgBytes, err := json.Marshal(candidate)
		if err != nil {
			fmt.Printf("WebRTCSFU: webSocketHandler: OnICECandidate: ERROR: %s\n", err)
			return
		}
		msg := ClientMessage{
			MessageType: 4,
			Message:     json.RawMessage(msgBytes),
		}
		fmt.Println("WebRTCSFU: webSocketHandler: OnICECandidate: Found a candidate")

		if err := clc.websocket.WriteJSONSafe(msg); err != nil {
			//panic(err)
			fmt.Println("WebRTCSFU: webSocketHandler: ERROR: ", err)
		}
	})

	// If PeerConnection is closed remove it from global list
	clc.peerConnection.OnConnectionStateChange(func(p webrtc.PeerConnectionState) {
		// TODO Maybe inform sfu/session manager
		fmt.Printf("WebRTCSFU: webSocketHandler: OnConnectionStateChange: Peer connection state has changed to %s\n", p.String())
		switch p {
		case webrtc.PeerConnectionStateFailed:
			if err := clc.peerConnection.Close(); err != nil {
				fmt.Printf("WebRTCSFU: webSocketHandler: ERROR: %s\n", err)
			}
		case webrtc.PeerConnectionStateClosed:
			fmt.Println("WebRTCSFU: webSocketHandler: OnConnectionStateChange: Closed")
			signalPeerConnections()
		case webrtc.PeerConnectionStateConnected:
			fmt.Println("WebRTCSFU: webSocketHandler: OnConnectionStateChange: Connected")

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

func (clc *ClientConnection) AddTrack(trackID string, track webrtc.TrackLocal) {
	clc.mut.Lock()
	defer clc.mut.Unlock()
	// TODO Check if trackID appears from session manager track
	receiverTrack := &ReceiverTrack{
		TrackID:                  trackID,
		CorrespondingSenderTrack: track, // TODO Maybe change this to SenderTrack type
	}
	if track.Kind() == webrtc.RTPCodecTypeVideo {
		clc.ReceiverVideoTracks[trackID] = receiverTrack
	} else if track.Kind() == webrtc.RTPCodecTypeAudio {
		clc.ReceiverAudioTracks[trackID] = receiverTrack
	}
	rtpSender, err := clc.peerConnection.AddTrack(trackLocals[trackID])
	if err != nil {
		// TODO error handling
		return
	}
	receiverTrack.RTPSender = rtpSender
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

func (clc *ClientConnection) startListening() {
	go func() {
		for {
			var msg ClientMessage
			if err := clc.websocket.ReadJSON(&msg); err != nil {
				fmt.Printf("WebRTCSFU: webSocketHandler: ReadMessage: error %w\n", err)
				break
			}

			switch msg.MessageType {
			// answer
			case 3:
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
				clc.updateCamInfo(candidate.Message)
			}
		}
	}()
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
