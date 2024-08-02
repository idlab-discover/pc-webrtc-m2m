package main

import (
	"bytes"
	"encoding/binary"
	"encoding/json"
	"flag"
	"fmt"
	"os"
	"strconv"
	"strings"
	"sync"
	"time"

	"github.com/pion/interceptor"
	"github.com/pion/interceptor/pkg/nack"
	"github.com/pion/interceptor/pkg/twcc"
	"github.com/pion/rtp"
	"github.com/pion/sdp/v3"
	"github.com/pion/webrtc/v3"
)

const (
	Idle     int = 0
	Hello    int = 1
	Offer    int = 2
	Answer   int = 3
	Ready    int = 4
	Finished int = 5
)

var proxyConn *ProxyConnection
var clientID *int

func main() {
	sfuAddress := flag.String("sfu", "localhost:8080", "SFU address")
	proxyPort := flag.String("p", ":0", "Port through which the DLL is connected")
	useProxyInput := flag.Bool("i", false, "Receive content from the DLL to forward over WebRTC")
	useProxyOutput := flag.Bool("o", false, "Forward content received over WebRTC to the DLL")
	clientID = flag.Int("c", 0, "Client ID")
	numberOfTiles := flag.Int("t", 1, "Number of tiles")
	flag.Parse()

	if *proxyPort == ":0" {
		println("WebRTCPeer: ERROR: port cannot equal :0")
		os.Exit(1)
	}

	fmt.Printf("WebRTCPeer: Starting client %d\n", *clientID)

	proxyConn = NewProxyConnection()
	proxyConn.SetupConnection(*proxyPort)
	var transcoder Transcoder
	if *useProxyInput {
		proxyConn.StartListening()
		transcoder = NewTranscoderRemote(proxyConn)
	} else {
		transcoder = NewTranscoderDummy(proxyConn)
	}

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
	//congestionController, err := cc.NewInterceptor(func() (cc.BandwidthEstimator, error) {
	//	return gcc.NewSendSideBWE(gcc.SendSideBWEMinBitrate(75_000*8), gcc.SendSideBWEInitialBitrate(75_000_000), gcc.SendSideBWEMaxBitrate(262_744_320))
	//})
	//if err != nil {
	//	panic(err)
	//}

	m.RegisterFeedback(webrtc.RTCPFeedback{Type: "nack"}, webrtc.RTPCodecTypeVideo)
	m.RegisterFeedback(webrtc.RTCPFeedback{Type: "nack", Parameter: "pli"}, webrtc.RTPCodecTypeVideo)

	//estimatorChan := make(chan cc.BandwidthEstimator, 1)
	//congestionController.OnNewPeerConnection(func(id string, estimator cc.BandwidthEstimator) {
	//	estimatorChan <- estimator
	//})

	//i.Add(congestionController)
	//if err = webrtc.ConfigureTWCCHeaderExtensionSender(m, i); err != nil {
	//	panic(err)
	//}

	responder, _ := nack.NewResponderInterceptor()
	i.Add(responder)

	m.RegisterFeedback(webrtc.RTCPFeedback{Type: webrtc.TypeRTCPFBTransportCC}, webrtc.RTPCodecTypeVideo)
	if err := m.RegisterHeaderExtension(webrtc.RTPHeaderExtensionCapability{URI: sdp.TransportCCURI}, webrtc.RTPCodecTypeVideo); err != nil {
		panic(err)
	}

	//m.RegisterFeedback(webrtc.RTCPFeedback{Type: webrtc.TypeRTCPFBTransportCC}, webrtc.RTPCodecTypeAudio)
	//if err := m.RegisterHeaderExtension(webrtc.RTPHeaderExtensionCapability{URI: sdp.TransportCCURI}, webrtc.RTPCodecTypeAudio); err != nil {
	//	panic(err)
	//}

	generator, err := twcc.NewSenderInterceptor(twcc.SendInterval(10 * time.Millisecond))
	if err != nil {
		panic(err)
	}
	i.Add(generator)

	nackGenerator, _ := nack.NewGeneratorInterceptor()
	i.Add(nackGenerator)

	var candidatesMux sync.Mutex
	pendingCandidates := make([]*webrtc.ICECandidate, 0)
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

	// TODO: probably remove this? This is the same as before but without feedback aka TWCC / NACK
	//
	codecCapability = webrtc.RTPCodecCapability{
		MimeType:     "video/pcm",
		ClockRate:    90000,
		Channels:     0,
		SDPFmtpLine:  "",
		RTCPFeedback: nil,
	}

	videoTracks := map[int]*TrackLocalCloudRTP{}
	// TODO: give audio custom id?
	audioTrack, err := NewTrackLocalAudioRTP(audioCodecCapability, fmt.Sprintf("audio_%d", *clientID), fmt.Sprintf("%d", 99))
	if err != nil {
		panic(err)
	}
	for i := 0; i < *numberOfTiles; i++ {
		videoTrack, err := NewTrackLocalCloudRTP(codecCapability, fmt.Sprintf("video_%d_%d", *clientID, i), fmt.Sprintf("%d", i))
		if err != nil {
			panic(err)
		}
		videoTracks[i] = videoTrack
	}

	if _, err = peerConnection.AddTrack(audioTrack); err != nil {
		panic(err)
	}
	for i := 0; i < *numberOfTiles; i++ {
		if _, err = peerConnection.AddTrack(videoTracks[i]); err != nil {
			panic(err)
		}
	}

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

	defer func() {
		if cErr := peerConnection.Close(); cErr != nil {
			fmt.Printf("WebRTCPeer: Cannot close peer connection: %v\n", cErr)
		}
	}()

	//estimator := <-estimatorChan

	// Create custom websocket handler on SFU address
	wsHandler := NewWSHandler(*sfuAddress, "/websocket")
	proxyConn.wsHandler = wsHandler

	peerConnection.OnICECandidate(func(c *webrtc.ICECandidate) {
		if c == nil {
			return
		}
		candidatesMux.Lock()
		desc := peerConnection.RemoteDescription()
		if desc == nil {
			pendingCandidates = append(pendingCandidates, c)
		} else {
			payload := []byte(c.ToJSON().Candidate)
			wsHandler.SendMessage(WebsocketPacket{uint64(*clientID), 4, string(payload)})
		}
		candidatesMux.Unlock()
	})

	peerConnection.OnConnectionStateChange(func(s webrtc.PeerConnectionState) {
		fmt.Printf("WebRTCPeer: Peer connection state has changed: %s\n", s.String())
		if s == webrtc.PeerConnectionStateFailed {
			fmt.Println("WebRTCPeer: Peer connection failed, exiting")
			os.Exit(0)
		} else if s == webrtc.PeerConnectionStateConnected {
			// TODO Combine both write operations into one loop
			// Idk if the underlying socket is thread safe or not but having is an extra thread is probably unwarrented anyway

			go func() {
				// Potentially combine audio frames into single packet?
				// Would add small delay but might be more optimal?
				// Atm audio frame is around 126 bytes so WebRTC overhead might be too much
				for {
					audioTrack.WriteAudioFrame(proxyConn.NextAudioFrame())
				}

			}()
			//targetBitrate := uint32(estimator.GetTargetBitrate())
			transcoder.UpdateBitrate(0)
			for i := 0; i < *numberOfTiles; i++ {
				// TODO maybe change writeframe into goroutines?
				go func(tileNr int) {
					for {
						if err = videoTracks[tileNr].WriteFrame(transcoder, uint32(tileNr)); err != nil {
							//panic(err)
						}
					}
				}(i)
			}
			//for {

			//}
		}
	})

	peerConnection.OnTrack(func(track *webrtc.TrackRemote, receiver *webrtc.RTPReceiver) {
		fmt.Printf("WebRTCPeer: MIME type %s\n", track.Codec().MimeType)
		fmt.Printf("WebRTCPeer: Payload type %d\n", track.PayloadType())
		fmt.Printf("WebRTCPeer: Track SSRC %d\n", track.SSRC())
		// TODO: check the puprose of this code fragment
		// Currently this removes stuff like audio_1 (audio track second client) and video_X_1 (second tile for user X)?
		//if track.ID()[len(track.ID())-1] == '1' && track.Kind() == webrtc.RTPCodecTypeVideo {
		//		wsHandler.SendMessage(WebsocketPacket{1, 5, track.ID()})
		//	}
		trackIdTokens := strings.Split(track.ID(), "_")
		isVideo := false
		clientID, _ := strconv.ParseUint(trackIdTokens[1], 10, 32)
		trackID := uint64(0)
		if trackIdTokens[0] == "video" {
			isVideo = true
			trackID, _ = strconv.ParseUint(trackIdTokens[2], 10, 32)
		}
		proxyConn.SendTrackStatusPacket(uint32(clientID), 0, uint32(trackID), isVideo, true)
		codecName := strings.Split(track.Codec().RTPCodecCapability.MimeType, "/")
		fmt.Printf("WebRTCPeer: Track of type %d has started: %s\n", track.PayloadType(), codecName)

		// Create buffer to receive incoming track data, using 1300 bytes - header bytes
		// TODO is different for audio
		buf := make([]byte, 1220)

		// Allows to check if frames are received completely
		frames := make(map[uint32]uint32)
		//prevFrame := -1
		// TODO: make clean seperated function for audio / video so we dont constantly need to do the kind check
		lastFrameNr := 0
		for {
			_, _, readErr := track.Read(buf)
			if readErr != nil {
				fmt.Printf("WebRTCPeer: Can no longer read from track %s, terminating %s\n", track.ID(), readErr.Error())
				proxyConn.SendTrackStatusPacket(uint32(clientID), uint32(lastFrameNr), uint32(trackID), isVideo, false)
				break
			}
			if *useProxyOutput {
				if track.Kind() == webrtc.RTPCodecTypeVideo {
					proxyConn.SendTilePacket(buf, 20)
				} else {
					proxyConn.SendAudioPacket(buf, 20)
				}
			}
			// Create a buffer from the byte array, skipping the first 20 WebRTC bytes
			if track.Kind() == webrtc.RTPCodecTypeVideo {

				bufBinary := bytes.NewBuffer(buf[20:])

				// Read the fields from the buffer into a struct
				var p VideoFramePacket
				err := binary.Read(bufBinary, binary.LittleEndian, &p)
				if err != nil {
					panic(err)
				}

				frames[p.FrameNr] += p.SeqLen
				/*	if prevFrame != int(p.FrameNr) {
					fmt.Printf("WebRTCPeer: [VIDEO] Received video frame %d from client %d and tile %d with length %d\n",
						p.FrameNr, p.ClientNr, frames[uint32(prevFrame)], p.FrameLen)
					prevFrame = int(p.FrameNr)
				}*/
				if frames[p.FrameNr] == p.FrameLen {
					lastFrameNr = int(p.FrameNr)
				}

				if frames[p.FrameNr] == p.FrameLen && p.FrameNr%10 == 0 {
					fmt.Printf("WebRTCPeer: [VIDEO] Received video frame %d from client %d and tile %d with length %d\n",
						p.FrameNr, p.ClientNr, p.TileNr, p.FrameLen)
				}

			} else {
				bufBinary := bytes.NewBuffer(buf[20:])

				// Read the fields from the buffer into a struct
				var p AudioFramePacket
				err := binary.Read(bufBinary, binary.LittleEndian, &p)
				if err != nil {
					panic(err)
				}
				frames[p.FrameNr] += p.SeqLen
				if frames[p.FrameNr] == p.FrameLen && p.FrameNr%100 == 0 {
					fmt.Printf("WebRTCPeer: [AUDIO] Received audio frame %d from client %d with length %d\n",
						p.FrameNr, p.ClientNr, p.FrameLen)
				}

			}

		}
	})

	var state = Idle
	fmt.Printf("WebRTCPeer: Current state: %d\n", state)

	var handleMessageCallback = func(wsPacket WebsocketPacket) {
		switch wsPacket.MessageType {
		case 1: // hello
			fmt.Println("WebRTCPeer: Received hello")
			offer, err := peerConnection.CreateOffer(nil)
			if err != nil {
				panic(err)
			}
			if err = peerConnection.SetLocalDescription(offer); err != nil {
				panic(err)
			}
			payload, err := json.Marshal(offer)
			if err != nil {
				panic(err)
			}
			wsHandler.SendMessage(WebsocketPacket{uint64(*clientID), 2, string(payload)})
			state = Hello
			fmt.Printf("WebRTCPeer: Current state: %d\n", state)
		case 2: // offer
			fmt.Println("WebRTCPeer: Received offer")
			offer := webrtc.SessionDescription{}
			err := json.Unmarshal([]byte(wsPacket.Message), &offer)
			if err != nil {
				panic(err)
			}
			err = peerConnection.SetRemoteDescription(offer)
			if err != nil {
				panic(err)
			}
			answer, err := peerConnection.CreateAnswer(nil)
			if err != nil {
				panic(err)
			}
			if err = peerConnection.SetLocalDescription(answer); err != nil {
				panic(err)
			}
			payload, err := json.Marshal(answer)
			if err != nil {
				panic(err)
			}
			wsHandler.SendMessage(WebsocketPacket{uint64(*clientID), 3, string(payload)})
			state = Offer
			fmt.Printf("WebRTCPeer: Current state: %d\n", state)
		case 3: // answer
			fmt.Println("WebRTCPeer: Received answer")
			answer := webrtc.SessionDescription{}
			err := json.Unmarshal([]byte(wsPacket.Message), &answer)
			if err != nil {
				panic(err)
			}
			if err := peerConnection.SetRemoteDescription(answer); err != nil {
				panic(err)
			}
			candidatesMux.Lock()
			for _, c := range pendingCandidates {
				payload := []byte(c.ToJSON().Candidate)
				wsHandler.SendMessage(WebsocketPacket{uint64(*clientID), 4, string(payload)})
			}
			candidatesMux.Unlock()
			state = Answer
			fmt.Printf("WebRTCPeer: Current state: %d\n", state)
		case 4: // candidate
			fmt.Println("WebRTCPeer: Received candidate")
			candidate := wsPacket.Message
			if candidateErr := peerConnection.AddICECandidate(webrtc.ICECandidateInit{Candidate: candidate}); candidateErr != nil {
				panic(candidateErr)
			}
		default:
			fmt.Printf("WebRTCPeer: Received non-compliant message type %d\n", wsPacket.MessageType)
		}
	}

	wsHandler.StartListening(handleMessageCallback)

	// Block forever
	select {}
}

// TrackLocalStaticRTP  is a TrackLocal that has a pre-set codec and accepts RTP Packets.
// If you wish to send a media.Sample use TrackLocalStaticSample
type TrackLocalCloudRTP struct {
	packetizer rtp.Packetizer
	sequencer  rtp.Sequencer
	rtpTrack   *webrtc.TrackLocalStaticRTP
	clockRate  float64
}

// NewTrackLocalStaticSample returns a TrackLocalStaticSample
func NewTrackLocalCloudRTP(c webrtc.RTPCodecCapability, id, streamID string, options ...func(*webrtc.TrackLocalStaticRTP)) (*TrackLocalCloudRTP, error) {
	rtpTrack, err := webrtc.NewTrackLocalStaticRTP(c, id, streamID, options...)
	if err != nil {
		return nil, err
	}
	return &TrackLocalCloudRTP{
		rtpTrack: rtpTrack,
	}, nil
}

// Bind is called by the PeerConnection after negotiation is complete
// This asserts that the code requested is supported by the remote peer.
// If so it setups all the state (SSRC and PayloadType) to have a call
func (s *TrackLocalCloudRTP) Bind(t webrtc.TrackLocalContext) (webrtc.RTPCodecParameters, error) {
	codec, err := s.rtpTrack.Bind(t)
	if err != nil {
		return codec, err
	}
	// We only need one packetizer
	if s.packetizer != nil {
		return codec, nil
	}
	s.sequencer = rtp.NewRandomSequencer()
	ui64, err := strconv.ParseUint(s.StreamID(), 10, 64)
	if err != nil {
		panic(err)
	}
	println("Tile", ui64)
	s.packetizer = rtp.NewPacketizer(
		1200, // Not MTU but ok
		0,    // Value is handled when writing
		0,    // Value is handled when writing
		NewPointCloudPayloader(uint32(ui64)),
		s.sequencer,
		codec.ClockRate,
	)
	s.clockRate = float64(codec.RTPCodecCapability.ClockRate)
	return codec, err
}

// Unbind implements the teardown logic when the track is no longer needed. This happens
// because a track has been stopped.
func (s *TrackLocalCloudRTP) Unbind(t webrtc.TrackLocalContext) error {
	return s.rtpTrack.Unbind(t)
}

// ID is the unique identifier for this Track. This should be unique for the
// stream, but doesn't have to globally unique. A common example would be 'audio' or 'video'
// and StreamID would be 'desktop' or 'webcam'
func (s *TrackLocalCloudRTP) ID() string { return s.rtpTrack.ID() }

// StreamID is the group this track belongs too. This must be unique
func (s *TrackLocalCloudRTP) StreamID() string { return s.rtpTrack.StreamID() }

// RID is the RTP stream identifier
func (s *TrackLocalCloudRTP) RID() string { return s.rtpTrack.RID() }

// Kind controls if this TrackLocal is audio or video
func (s *TrackLocalCloudRTP) Kind() webrtc.RTPCodecType { return s.rtpTrack.Kind() }

// Codec gets the Codec of the track
func (s *TrackLocalCloudRTP) Codec() webrtc.RTPCodecCapability {
	return s.rtpTrack.Codec()
}

func (s *TrackLocalCloudRTP) WriteFrame(t Transcoder, tile uint32) error {
	p := s.packetizer
	clockRate := s.clockRate
	if p == nil {
		return nil
	}
	samples := uint32(1 * clockRate)
	data := t.EncodeFrame(tile)
	if data != nil {
		packets := p.Packetize(data, samples)
		counter := 0
		for _, p := range packets {
			if err := s.rtpTrack.WriteRTP(p); err != nil {
				fmt.Printf("WebRTCPeer: ERROR: %s\n", err)
			}
			counter += 1
		}
	}
	return nil
}

// AV1Payloader payloads AV1 packets
type PointCloudPayloader struct {
	frameCounter uint32
	tile         uint32
}

// Payload fragments a AV1 packet across one or more byte arrays
// See AV1Packet for description of AV1 Payload Header
func (p *PointCloudPayloader) Payload(mtu uint16, payload []byte) (payloads [][]byte) {
	payloadDataOffset := uint32(0)
	payloadLen := uint32(len(payload))
	payloadRemaining := payloadLen
	for payloadRemaining > 0 {
		currentFragmentSize := uint32(1148)
		if payloadRemaining < currentFragmentSize {
			currentFragmentSize = payloadRemaining
		}
		buf := make([]byte, currentFragmentSize+24)
		//	binary.LittleEndian.PutUint32(buf[0:], TilePacketType)
		binary.LittleEndian.PutUint32(buf[0:], uint32(*clientID))
		binary.LittleEndian.PutUint32(buf[4:], p.frameCounter)
		binary.LittleEndian.PutUint32(buf[8:], payloadLen)
		binary.LittleEndian.PutUint32(buf[12:], payloadDataOffset)
		binary.LittleEndian.PutUint32(buf[16:], currentFragmentSize)
		binary.LittleEndian.PutUint32(buf[20:], p.tile)

		copy(buf[24:], payload[payloadDataOffset:(payloadDataOffset+currentFragmentSize)])

		payloads = append(payloads, buf)
		payloadDataOffset += currentFragmentSize
		payloadRemaining -= currentFragmentSize
	}
	p.frameCounter++
	return payloads
}

func NewPointCloudPayloader(tile uint32) *PointCloudPayloader {
	return &PointCloudPayloader{0, tile}
}

// TrackLocalStaticRTP  is a TrackLocal that has a pre-set codec and accepts RTP Packets.
// If you wish to send a media.Sample use TrackLocalStaticSample
type TrackLocalAudioRTP struct {
	packetizer rtp.Packetizer
	sequencer  rtp.Sequencer
	rtpTrack   *webrtc.TrackLocalStaticRTP
	clockRate  float64
}

// NewTrackLocalStaticSample returns a TrackLocalStaticSample
func NewTrackLocalAudioRTP(c webrtc.RTPCodecCapability, id, streamID string, options ...func(*webrtc.TrackLocalStaticRTP)) (*TrackLocalAudioRTP, error) {
	rtpTrack, err := webrtc.NewTrackLocalStaticRTP(c, id, streamID, options...)
	if err != nil {
		return nil, err
	}
	return &TrackLocalAudioRTP{
		rtpTrack: rtpTrack,
	}, nil
}

// Bind is called by the PeerConnection after negotiation is complete
// This asserts that the code requested is supported by the remote peer.
// If so it setups all the state (SSRC and PayloadType) to have a call
func (s *TrackLocalAudioRTP) Bind(t webrtc.TrackLocalContext) (webrtc.RTPCodecParameters, error) {
	codec, err := s.rtpTrack.Bind(t)
	if err != nil {
		return codec, err
	}
	// We only need one packetizer
	if s.packetizer != nil {
		return codec, nil
	}
	s.sequencer = rtp.NewRandomSequencer()

	s.packetizer = rtp.NewPacketizer(
		1200, // Not MTU but ok
		0,    // Value is handled when writing
		0,    // Value is handled when writing
		NewAudioPayloader(),
		s.sequencer,
		codec.ClockRate,
	)
	s.clockRate = float64(codec.RTPCodecCapability.ClockRate)
	return codec, err
}

// Unbind implements the teardown logic when the track is no longer needed. This happens
// because a track has been stopped.
func (s *TrackLocalAudioRTP) Unbind(t webrtc.TrackLocalContext) error {
	return s.rtpTrack.Unbind(t)
}

// ID is the unique identifier for this Track. This should be unique for the
// stream, but doesn't have to globally unique. A common example would be 'audio' or 'video'
// and StreamID would be 'desktop' or 'webcam'
func (s *TrackLocalAudioRTP) ID() string { return s.rtpTrack.ID() }

// StreamID is the group this track belongs too. This must be unique
func (s *TrackLocalAudioRTP) StreamID() string { return s.rtpTrack.StreamID() }

// RID is the RTP stream identifier
func (s *TrackLocalAudioRTP) RID() string { return s.rtpTrack.RID() }

// Kind controls if this TrackLocal is audio or video
func (s *TrackLocalAudioRTP) Kind() webrtc.RTPCodecType { return s.rtpTrack.Kind() }

// Codec gets the Codec of the track
func (s *TrackLocalAudioRTP) Codec() webrtc.RTPCodecCapability {
	return s.rtpTrack.Codec()
}

func (s *TrackLocalAudioRTP) WriteAudioFrame(audio []byte) error {
	p := s.packetizer
	clockRate := s.clockRate
	if p == nil {
		return nil
	}
	samples := uint32(1 * clockRate)

	if audio != nil {
		packets := p.Packetize(audio, samples)
		counter := 0
		for _, p := range packets {
			if err := s.rtpTrack.WriteRTP(p); err != nil {
				fmt.Printf("WebRTCPeer: ERROR: %s\n", err)
			}
			counter += 1
		}
	}
	return nil
}

// AV1Payloader payloads AV1 packets
type AudioPayloader struct {
	frameCounter uint32
}

// Payload fragments a AV1 packet across one or more byte arrays
// See AudioPayloader for description of AV1 Payload Header
func (p *AudioPayloader) Payload(mtu uint16, payload []byte) (payloads [][]byte) {
	payloadDataOffset := uint32(0)
	payloadLen := uint32(len(payload))
	payloadRemaining := payloadLen
	for payloadRemaining > 0 {
		currentFragmentSize := uint32(1152)
		if payloadRemaining < currentFragmentSize {
			currentFragmentSize = payloadRemaining
		}
		buf := make([]byte, currentFragmentSize+20)
		binary.LittleEndian.PutUint32(buf[0:], uint32(*clientID))
		binary.LittleEndian.PutUint32(buf[4:], p.frameCounter)

		binary.LittleEndian.PutUint32(buf[8:], payloadLen)
		binary.LittleEndian.PutUint32(buf[12:], payloadDataOffset)
		binary.LittleEndian.PutUint32(buf[16:], currentFragmentSize)
		copy(buf[20:], payload[payloadDataOffset:(payloadDataOffset+currentFragmentSize)])
		payloads = append(payloads, buf)
		payloadDataOffset += currentFragmentSize
		payloadRemaining -= currentFragmentSize
	}
	p.frameCounter++
	return payloads
}

func NewAudioPayloader() *AudioPayloader {
	return &AudioPayloader{}
}
