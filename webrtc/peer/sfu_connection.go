// Hold tracks per SFU / send and receive
// Create when sending to session manager
// Set status to ready when receiving AddedToProvider message
package main

import (
	"encoding/json"
	"fmt"
	"net/url"
	"sync"

	"github.com/gorilla/websocket"
	"github.com/pion/interceptor"
	"github.com/pion/interceptor/pkg/nack"
	"github.com/pion/sdp/v3"
	"github.com/pion/webrtc/v3"
)

const NameSFUConnection = "SFUConnection"

type SFUConnection struct {
	providerKey string
	IsReady     bool
	Address     string
	Port        uint

	senderVideoTracks       map[string]*WebRTCVideoTrack
	senderAudioTracks       map[string]*WebRTCAudioTrack
	receiverVideoTracks     map[string]*WebRTCVideoTrack
	receiverAudioTracks     map[string]*WebRTCAudioTrack
	peerConnection          *webrtc.PeerConnection
	pendingCandidates       []*webrtc.ICECandidate
	pendingCandidatesString []string

	websocket *ThreadSafeWebsocket
	mut       sync.Mutex
}

type WebRTCAudioTrack struct {
	track *TrackLocalAudioRTP
}

func NewWebRTCAudioTrack(track *TrackLocalAudioRTP) *WebRTCAudioTrack {
	t := &WebRTCAudioTrack{
		track: track,
	}
	return t
}

type WebRTCVideoTrack struct {
	track *TrackLocalCloudRTP
}

func NewWebRTCVideoTrack(track *TrackLocalCloudRTP) *WebRTCVideoTrack {
	t := &WebRTCVideoTrack{
		track: track,
	}
	return t
}

func NewSFUConnection(providerKey string) *SFUConnection {
	LogWithMessage(NameSFUConnection, CreatingProvider, true, true, fmt.Sprintf("providerKey=%s", providerKey))
	sfu := &SFUConnection{
		providerKey:             providerKey,
		senderVideoTracks:       map[string]*WebRTCVideoTrack{},
		pendingCandidates:       []*webrtc.ICECandidate{},
		pendingCandidatesString: []string{},
		mut:                     sync.Mutex{},
	}
	LogWithMessage(NameSFUConnection, CreatedProvider, true, true, fmt.Sprintf("providerKey=%s", providerKey))
	return sfu
}

func (s *SFUConnection) OnFullyConnected(clientID uint, authKey string, address string, port uint) {
	s.mut.Lock()
	defer s.mut.Unlock()
	LogWithMessage(NameSFUConnection, ClientAddedToProvider, true, true,
		fmt.Sprintf("providerKey=%s", s.providerKey),
	)
	s.IsReady = true
	s.Address = address
	s.Port = port
	s.preparePeerConnection()
	s.connectToSFU(clientID, authKey)
}

func (s *SFUConnection) connectToSFU(clientID uint, authKey string) {
	u := url.URL{Scheme: "ws", Host: fmt.Sprintf("%s:%d", s.Address, s.Port), Path: "websocket_client"}
	query := url.Values{}
	query.Set("clientID", fmt.Sprintf("%d", clientID))
	query.Set("authKey", authKey)

	u.RawQuery = query.Encode()
	conn, _, err := websocket.DefaultDialer.Dial(u.String(), nil)

	if err != nil {
		fmt.Printf("WebRTCPeer: NewWSHandler: ERROR: %s\n", err)
		panic(err)
	}
	s.websocket = &ThreadSafeWebsocket{
		conn, sync.Mutex{},
	}
	s.startListening()
}

func (s *SFUConnection) preparePeerConnection() {
	settingEngine := webrtc.SettingEngine{}
	settingEngine.SetSCTPMaxReceiveBufferSize(16 * 1024 * 1024)

	i := &interceptor.Registry{}
	m := &webrtc.MediaEngine{}
	if err := m.RegisterDefaultCodecs(); err != nil {
		panic(err)
	}
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
	// TODO: Which enable support for multiple audio codecs + custom ones
	// TODO: also fix these settings although it probably doesnt matter that much
	audioCodecCapability := webrtc.RTPCodecCapability{
		MimeType:     "audio/pcm",
		ClockRate:    90000,
		Channels:     0,
		SDPFmtpLine:  "",
		RTCPFeedback: nil,
	}

	if err := m.RegisterCodec(webrtc.RTPCodecParameters{
		RTPCodecCapability: codecCapability,
		PayloadType:        5,
	}, webrtc.RTPCodecTypeVideo); err != nil {
		panic(err)
	}

	if err := m.RegisterCodec(webrtc.RTPCodecParameters{
		RTPCodecCapability: audioCodecCapability,
		PayloadType:        6,
	}, webrtc.RTPCodecTypeAudio); err != nil {
		panic(err)
	}

	m.RegisterFeedback(webrtc.RTCPFeedback{Type: "nack"}, webrtc.RTPCodecTypeVideo)
	m.RegisterFeedback(webrtc.RTCPFeedback{Type: "nack", Parameter: "pli"}, webrtc.RTPCodecTypeVideo)

	if err := webrtc.ConfigureTWCCHeaderExtensionSender(m, i); err != nil {
		panic(err)
	}

	responder, _ := nack.NewResponderInterceptor()
	i.Add(responder)

	m.RegisterFeedback(webrtc.RTCPFeedback{Type: webrtc.TypeRTCPFBTransportCC}, webrtc.RTPCodecTypeVideo)
	if err := m.RegisterHeaderExtension(webrtc.RTPHeaderExtensionCapability{URI: sdp.TransportCCURI}, webrtc.RTPCodecTypeVideo); err != nil {
		panic(err)
	}

	// TODO add provider settings to addclienttoprovider message so we can know if we need to do this or not
	/*if !isDebug || !*disableGCC {
		generator, err := twcc.NewSenderInterceptor(twcc.SendInterval(10 * time.Millisecond))
		if err != nil {
			panic(err)
		}
		i.Add(generator)
	}*/

	nackGenerator, _ := nack.NewGeneratorInterceptor()
	i.Add(nackGenerator)

	peerConnection, err := webrtc.NewAPI(webrtc.WithSettingEngine(settingEngine), webrtc.WithInterceptorRegistry(i), webrtc.WithMediaEngine(m)).NewPeerConnection(webrtc.Configuration{
		ICEServers: []webrtc.ICEServer{
			{
				URLs: []string{"stun:stun.l.google.com:19302"},
			},
		},
	})
	if err != nil {
		panic(err)
	}
	s.peerConnection = peerConnection
	s.AddPeerConnectionCallbacks()
}

func (s *SFUConnection) startListening() {
	go func() {
		for {
			var msg ClientMessage
			if err := s.websocket.ReadJSON(&msg); err != nil {
				fmt.Printf("SessionManager: webSocketHandler: ReadMessage: error %s\n", err.Error())
				s.onClose()
				break
			}
			LogWithMessage(NameManagerConnection, ReceivedWSMessage, true, true,
				fmt.Sprintf("origin=sfu providerKey=%s type=%s", s.providerKey, msg.MessageType),
			)
			switch msg.MessageType {
			case "OfferMessage":
				s.handleOfferMessage(msg.Message)
			case "CandidateMessage":
				s.handleCandidateMessage(msg.Message)

			}
		}
	}()
}

func (s *SFUConnection) AddPeerConnectionCallbacks() {
	s.addOnICECandidateCallback()
}

func (s *SFUConnection) addOnICECandidateCallback() {
	s.peerConnection.OnICECandidate(func(c *webrtc.ICECandidate) {
		if c == nil {
			return
		}
		s.mut.Lock()
		desc := s.peerConnection.RemoteDescription()
		if desc == nil {
			s.pendingCandidates = append(s.pendingCandidates, c)
		} else {
			s.websocket.WriteJSONMessageSafe("CandidateMessage", c.ToJSON().Candidate)
		}
		s.mut.Unlock()
	})
}

func (s *SFUConnection) handleOfferMessage(payload json.RawMessage) {
	offer := webrtc.SessionDescription{}
	err := json.Unmarshal(payload, &offer)
	if err != nil {
		panic(err)
	}

	err = s.peerConnection.SetRemoteDescription(offer)
	if err != nil {
		panic(err)
	}
	answer, err := s.peerConnection.CreateAnswer(nil)
	if err != nil {
		panic(err)
	}
	if err = s.peerConnection.SetLocalDescription(answer); err != nil {
		panic(err)
	}

	s.websocket.WriteJSONMessageSafe("AnswerMessage", answer)
}

func (s *SFUConnection) handleCandidateMessage(payload json.RawMessage) {
	var candidate string
	if err := json.Unmarshal(payload, &candidate); err != nil {
		panic(err)
	}
	s.mut.Lock()
	defer s.mut.Unlock()
	desc := s.peerConnection.RemoteDescription()
	if desc == nil {
		s.pendingCandidatesString = append(s.pendingCandidatesString, candidate)
	} else {
		if candidateErr := s.peerConnection.AddICECandidate(webrtc.ICECandidateInit{Candidate: candidate}); candidateErr != nil {
			panic(candidateErr)
		}
	}

}

func (s *SFUConnection) AddAudioTrack(trackID string) {
	s.mut.Lock()
	defer s.mut.Unlock()
	// TODO Add client id to track ID here, maybe
	/*audioCodecCapability := webrtc.RTPCodecCapability{
		MimeType:     "audio/pcm",
		ClockRate:    90000,
		Channels:     0,
		SDPFmtpLine:  "",
		RTCPFeedback: nil,
	}
	audioTrack, err := NewTrackLocalAudioRTP(audioCodecCapability, trackID, trackID)
	if err != nil {
		panic(err)
	}
	s.senderAudioTracks[trackID] = NewWebRTCAudioTrack(audioTrack)
	if _, err = s.peerConnection.AddTrack(audioTrack); err != nil {
		panic(err)
	}*/
}

func (s *SFUConnection) AddVideoTrack(trackID string) {
	s.mut.Lock()
	defer s.mut.Unlock()
	//TODO Add client id to track ID here, maybe
	/*videoRTCPFeedback := []webrtc.RTCPFeedback{
		{Type: "goog-remb", Parameter: ""},
		{Type: "ccm", Parameter: "fir"},
		{Type: "nack", Parameter: ""},
		{Type: "nack", Parameter: "pli"},
	}

	videoCodecCapability := webrtc.RTPCodecCapability{
		MimeType:     "video/pcm",
		ClockRate:    90000,
		Channels:     0,
		SDPFmtpLine:  "",
		RTCPFeedback: videoRTCPFeedback,
	}
	videoTrack, err := NewTrackLocalCloudRTP(videoCodecCapability, fmt.Sprintf("video_%d_%d_%d", *clientID, i, j), fmt.Sprintf("%d_%d", i, j))
	if err != nil {
		panic(err)
	}
	s.senderVideoTracks[trackID] = NewWebRTCVideoTrack(videoTrack)
	if _, err = s.peerConnection.AddTrack(videoTrack); err != nil {
		panic(err)
	}*/
}

func (s *SFUConnection) onClose() {
	// TODO
}

// TODO ADD THIS FOR EACH TRACK FOR FEEDBACK ------------------------------------------------
/*
	processRTCP := func(rtpSender *webrtc.RTPSender) {
		rtcpBuf := make([]byte, 1500)
		for {
			if _, _, rtcpErr := rtpSender.Read(rtcpBuf); rtcpErr != nil {
				return
			}
		}
	}

	for _, rtpSender := range peerConnection.GetSenders() {
		go processRTCP(rtpSender)
	}

*/
