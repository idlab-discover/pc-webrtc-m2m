// Hold tracks per SFU / send and receive
// Create when sending to session manager
// Set status to ready when receiving AddedToProvider message
package sfu

import (
	"bytes"
	"encoding/binary"
	"encoding/json"
	"fmt"
	"net/url"
	"os"
	"strings"
	"sync"
	"time"

	"goweb/peer/src/logger"
	"goweb/peer/src/packet"
	"goweb/peer/src/proxy"
	"goweb/peer/src/session_manager"
	"goweb/peer/src/tracks/audio"
	"goweb/peer/src/tracks/point_cloud"
	"goweb/peer/src/transcoder"
	"goweb/peer/src/utils"

	"github.com/gorilla/websocket"
	"github.com/pion/interceptor"
	"github.com/pion/interceptor/pkg/nack"
	"github.com/pion/sdp/v3"
	"github.com/pion/webrtc/v4"
)

const NameSFUConnection = "SFUConnection"

type SFUConnection struct {
	ProxyConn   *proxy.ProxyConnection
	providerKey string
	IsReady     bool
	Address     string
	Port        uint

	senderVideoTracks       map[string]*WebRTCVideoTrack
	senderAudioTracks       map[string]*WebRTCAudioTrack
	receiverVideoTracks     map[string]bool
	receiverAudioTracks     map[string]bool
	peerConnection          *webrtc.PeerConnection
	pendingCandidates       []*webrtc.ICECandidate
	pendingCandidatesString []string
	transcoder              transcoder.Transcoder

	websocket *utils.ThreadSafeWebsocket
	mut       sync.Mutex
}

type WebRTCAudioTrack struct {
	track *audio.TrackLocalAudioRTP
}

func NewWebRTCAudioTrack(track *audio.TrackLocalAudioRTP) *WebRTCAudioTrack {
	t := &WebRTCAudioTrack{
		track: track,
	}
	return t
}

func (t *WebRTCAudioTrack) StartSending() {
	go func() {
		// TODO
	}()
}

type WebRTCVideoTrack struct {
	track      *point_cloud.TrackLocalCloudRTP
	transcoder transcoder.Transcoder
}

func NewWebRTCVideoTrack(track *point_cloud.TrackLocalCloudRTP, transcoder transcoder.Transcoder) *WebRTCVideoTrack {
	t := &WebRTCVideoTrack{
		track:      track,
		transcoder: transcoder,
	}
	return t
}

func (t *WebRTCVideoTrack) StartSending() {

	go func() {
		println("START SENDING")
		frameNr := 0
		for {
			//	println("START", time.Now().UnixMilli(), frameNr)
			if err := t.track.WriteFrame(t.transcoder, frameNr); err != nil {
				panic(err)
			}
			//	println("END", time.Now().UnixMilli(), frameNr)
			// TODO Log the sending
			frameNr++
		}
	}()
}

func NewSFUConnection(providerKey string, videoTracks []session_manager.TrackSimple, audioTracks []session_manager.TrackSimple, transcoder transcoder.Transcoder) *SFUConnection {
	logger.LogWithMessage(NameSFUConnection, logger.CreatingProvider, true, true, fmt.Sprintf("providerKey=%s", providerKey))
	sfu := &SFUConnection{
		providerKey:             providerKey,
		senderVideoTracks:       map[string]*WebRTCVideoTrack{},
		senderAudioTracks:       map[string]*WebRTCAudioTrack{},
		pendingCandidates:       []*webrtc.ICECandidate{},
		pendingCandidatesString: []string{},
		transcoder:              transcoder,
		mut:                     sync.Mutex{},
	}
	for _, track := range videoTracks {
		sfu.AddVideoTrack(track.TrackID)
	}
	for _, track := range audioTracks {
		sfu.AddAudioTrack(track.TrackID)
	}
	logger.LogWithMessage(NameSFUConnection, logger.CreatedProvider, true, true, fmt.Sprintf("providerKey=%s", providerKey))
	return sfu
}

func (s *SFUConnection) OnFullyConnected(clientID uint, authKey string, address string, port uint) {
	s.mut.Lock()
	defer s.mut.Unlock()
	logger.LogWithMessage(NameSFUConnection, logger.ClientAddedToProvider, true, true,
		fmt.Sprintf("providerKey=%s", s.providerKey),
	)
	s.IsReady = true
	s.Address = address
	s.Port = port
	s.preparePeerConnection()
	s.connectToSFU(clientID, authKey)
}

type SubscribeToRemoteClientsMessage struct {
	Clients []session_manager.RemoteClientSimple `json:"clients"`
}

func (s *SFUConnection) SubscribeToRemoteClientTracks(clients []session_manager.RemoteClientSimple) {
	s.mut.Lock()
	defer s.mut.Unlock()
	if len(clients) == 0 {
		return
	}
	s.websocket.WriteJSONMessageSafe("SubscribeToRemoteClientsMessage", SubscribeToRemoteClientsMessage{
		Clients: clients,
	})
}

func (s *SFUConnection) connectToSFU(clientID uint, authKey string) {
	u := url.URL{Scheme: "ws", Host: fmt.Sprintf("%s:%d", s.Address, s.Port), Path: "websocket_client"}
	query := url.Values{}
	query.Set("clientID", fmt.Sprintf("%d", clientID))
	query.Set("authKey", authKey)

	u.RawQuery = query.Encode()
	conn, _, err := websocket.DefaultDialer.Dial(u.String(), nil)

	if err != nil {
		fmt.Printf("WebRTCPeer: NewWSHandler: using %s ERROR: %s\n", u.String(), err)
		panic(err)
	}
	s.websocket = &utils.ThreadSafeWebsocket{
		Conn:  conn,
		Mutex: sync.Mutex{},
	}
	if s.ProxyConn != nil {
		s.ProxyConn.WsHandler = s.websocket
	}
	s.startListening()
}

func (s *SFUConnection) preparePeerConnection() {
	settingEngine := webrtc.SettingEngine{}
	settingEngine.SetSCTPMaxReceiveBufferSize(16 * 1024 * 1024)
	settingEngine.SetReceiveMTU(1500)
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

	// Add sender tracks
	for _, track := range s.senderVideoTracks {
		s.addTrackToPeerConnection(track.track)
	}
	for _, track := range s.senderAudioTracks {
		s.addTrackToPeerConnection(track.track)
	}
	s.AddPeerConnectionCallbacks()
}

func (s *SFUConnection) startListening() {
	go func() {
		for {
			var msg utils.ClientMessage
			if err := s.websocket.ReadJSON(&msg); err != nil {
				fmt.Printf("SessionManager: webSocketHandler: ReadMessage: error %s\n", err.Error())
				s.onClose()
				break
			}
			logger.LogWithMessage(NameSFUConnection, logger.ReceivedWSMessage, true, true,
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
	s.addOnTrackCallback()
	s.addOnConnectionStatusChanged()
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

func (s *SFUConnection) addOnTrackCallback() {
	s.peerConnection.OnTrack(func(track *webrtc.TrackRemote, receiver *webrtc.RTPReceiver) {
		fmt.Printf("WebRTCPeer: MIME type %s\n", track.Codec().MimeType)
		fmt.Printf("WebRTCPeer: Payload type %d\n", track.PayloadType())
		fmt.Printf("WebRTCPeer: Track SSRC %d\n", track.SSRC())

		/*if *useProxyInput {
			proxyConn.SendTrackStatusPacket(uint32(clientID), 0, uint32(capturerID), uint32(trackID), isVideo, true)
		}*/

		codecName := strings.Split(track.Codec().RTPCodecCapability.MimeType, "/")
		fmt.Printf("WebRTCPeer: Track of type %d has started: %s\n", track.PayloadType(), codecName)

		// Create buffer to receive incoming track data, using 1300 bytes - header bytes
		// TODO is different for audio
		buf := make([]byte, 1220)

		// Allows to check if frames are received completely
		frames := make(map[uint32]uint32)
		//prevFrame := -1
		// TODO: make clean seperated function for audio / video so we dont constantly need to do the kind check
		// ---------------------------------------
		// Keep reading until error or until client request to listen to track
		// If client decides to listen, keep reading until the frame is completed
		// Start forwarding frames at this point
		// ---------------------------------------
		var internalTrackID uint32
		if s.ProxyConn != nil {
			for {
				_, _, readErr := track.Read(buf)
				if readErr != nil {
					return
				}
				var p packet.FramePacket
				bufBinary := bytes.NewBuffer(buf[20:])
				err := binary.Read(bufBinary, binary.LittleEndian, &p)
				if err != nil {
					panic(err)
				}
				// Read the fields from the buffer into a struct

				frames[p.FrameNr] += p.SeqLen
				if frames[p.FrameNr] == p.FrameLen {
					var exists bool
					internalTrackID, exists = s.ProxyConn.GetInternalTrackID(track.ID())
					if exists {
						break
					}
				}
			}
		}
		for {
			_, _, readErr := track.Read(buf)
			// TODO Implement pausing unpausing of track
			if readErr != nil {
				return
			}
			bufBinary := bytes.NewBuffer(buf[20:])

			// Read the fields from the buffer into a struct
			var p packet.FramePacket
			err := binary.Read(bufBinary, binary.LittleEndian, &p)
			if err != nil {
				panic(err)
			}
			if s.ProxyConn != nil {
				// TODO Add internal track ID mapping here
				s.ProxyConn.SendFramePacket(internalTrackID, buf, 20)
			}
			frames[p.FrameNr] += p.SeqLen
			if frames[p.FrameNr] == p.FrameLen && p.FrameNr%100 == 0 {
				// Frame complete
				fmt.Printf("WebRTCPeer: [VIDEO] %s %d Received video frame %d from client %d with internalTrackID %d with length %d\n",
					track.ID(), time.Now().UnixMilli(), p.FrameNr, p.ClientNr, internalTrackID, p.FrameLen)
			}

		}
	})
}

func (s *SFUConnection) addOnConnectionStatusChanged() {
	s.peerConnection.OnConnectionStateChange(func(state webrtc.PeerConnectionState) {
		logger.LogWithMessage(NameSFUConnection, logger.SFUClientConnectionChange, true, true,
			fmt.Sprintf("providerKey=%s state=%s", s.providerKey, state.String()))
		if state == webrtc.PeerConnectionStateFailed {
			fmt.Println("WebRTCPeer: Peer connection failed, exiting")
			os.Exit(0)
		} else if state == webrtc.PeerConnectionStateConnected {
			// TODO Combine both write operations into one loop
			// Idk if the underlying socket is thread safe or not but having is an extra thread is probably unwarrented anyway
			s.mut.Lock()
			defer s.mut.Unlock()
			for _, t := range s.senderVideoTracks {
				t.StartSending()
			}
			for _, t := range s.senderAudioTracks {
				t.StartSending()
			}

		}
	})
}

func (s *SFUConnection) handleOfferMessage(payload json.RawMessage) {
	offer := webrtc.SessionDescription{}
	err := json.Unmarshal(payload, &offer)
	if err != nil {
		panic(err)
	}
	fmt.Printf("%+v\n", offer)
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

func (s *SFUConnection) StartPollingVideoTrack(trackID string) {
	s.StartPollingVideoTracks([]string{trackID})
	// Tracks are automatically added but with nil, this replace the track with the actual track
	// Potentially buffer these tracks until real track is received????
}

func (s *SFUConnection) StartPollingVideoTracks(trackIDs []string) {
	s.mut.Lock()
	defer s.mut.Unlock()
	for _, trackID := range trackIDs {
		s.receiverVideoTracks[trackID] = true
	}
	s.websocket.WriteJSONMessageSafe("StartPollingVideoTracks", trackIDs)
}

func (s *SFUConnection) StartPollingAudioTrack(trackID string) {
	s.StartPollingAudioTracks([]string{trackID})
}

func (s *SFUConnection) StartPollingAudioTracks(trackIDs []string) {
	s.mut.Lock()
	defer s.mut.Unlock()
	for _, trackID := range trackIDs {
		s.receiverAudioTracks[trackID] = true
	}
	s.websocket.WriteJSONMessageSafe("StartPollingAudioTracks", trackIDs)
}

func (s *SFUConnection) AddAudioTrack(trackID string) {
	s.mut.Lock()
	defer s.mut.Unlock()
	// TODO Add client id to track ID here, maybe
	audioCodecCapability := webrtc.RTPCodecCapability{
		MimeType:     "audio/pcm",
		ClockRate:    90000,
		Channels:     0,
		SDPFmtpLine:  "",
		RTCPFeedback: nil,
	}
	audioTrack, err := audio.NewTrackLocalAudioRTP(audioCodecCapability, trackID, trackID)
	if err != nil {
		panic(err)
	}
	s.senderAudioTracks[trackID] = NewWebRTCAudioTrack(audioTrack)

}

func (s *SFUConnection) AddVideoTrack(trackID string) {
	s.mut.Lock()
	defer s.mut.Unlock()
	videoRTCPFeedback := []webrtc.RTCPFeedback{
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
	videoTrack, err := point_cloud.NewTrackLocalCloudRTP(videoCodecCapability, trackID, trackID)
	if err != nil {
		panic(err)
	}
	s.senderVideoTracks[trackID] = NewWebRTCVideoTrack(videoTrack, s.transcoder)

}

func (s *SFUConnection) addTrackToPeerConnection(track webrtc.TrackLocal) {

	var rtpSender *webrtc.RTPSender
	var err error
	if rtpSender, err = s.peerConnection.AddTrack(track); err != nil {
		panic(err)
	}

	go func() {
		rtcpBuf := make([]byte, 1500)
		for {
			if _, _, err := rtpSender.Read(rtcpBuf); err != nil {
				panic(err)
				return
			}
		}
	}()
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
