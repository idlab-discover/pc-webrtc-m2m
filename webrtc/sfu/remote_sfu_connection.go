package main

import (
	"encoding/json"
	"fmt"
	"goweb/shared/src/logger"
	"goweb/shared/src/packet"
	"net"
	"strings"
	"sync/atomic"

	"github.com/pion/interceptor"
	"github.com/pion/webrtc/v4"
)

const NameRemoteSFUConnection = "RemoteSFUConnection"

type RemoteSFUConnection struct {
	ProviderConnectionBase
	parent                  *SFU
	peerConnection          *webrtc.PeerConnection
	IsNegotiating           bool
	NeedsUpdate             bool
	SenderVideoTracks       map[string]*SenderTrack
	SenderAudioTracks       map[string]*SenderTrack
	ReceiverVideoTracks     map[string]*ReceiverTrack
	ReceiverAudioTracks     map[string]*ReceiverTrack
	pendingCandidatesString []string
}

func NewRemoteSFUConnection(parent *SFU, providerKey string, address string, port uint, authKey string) *RemoteSFUConnection {
	rsfu := &RemoteSFUConnection{
		parent:                 parent,
		ProviderConnectionBase: NewProviderConnectionBase("sfu", providerKey, address, port, authKey),
		SenderVideoTracks:      make(map[string]*SenderTrack),
		SenderAudioTracks:      make(map[string]*SenderTrack),
		ReceiverVideoTracks:    make(map[string]*ReceiverTrack),
		ReceiverAudioTracks:    make(map[string]*ReceiverTrack),
		IsNegotiating:          false,
		NeedsUpdate:            false,
	}
	rsfu.specialMessageCallback = rsfu.HandleSpecialMessage
	rsfu.subscribeToRemoteClientCallback = rsfu.HandleSubscribeToRemoteClient
	return rsfu
}

func (rsfu *RemoteSFUConnection) SetupForwarding() error {
	logger.LogWithMessage(NameRemoteSFUConnection, logger.RemoteProviderConnectForwardingStarted, true, true, fmt.Sprintf("providerKey=%s", rsfu.providerKey))
	rsfu.SetupPeerConnection()
	return nil
}

func (rsfu *RemoteSFUConnection) SetupPeerConnection() {
	logger.LogWithMessage(NameRemoteSFUConnection, logger.ClientAddingTransceivers, true, true,
		fmt.Sprintf("providerKey=%s nVideoTrack=%d nAudioTracks=%d",
			rsfu.providerKey, len(rsfu.SenderVideoTracks), len(rsfu.SenderAudioTracks)),
	)
	mediaEngine := SetupDefaultMediaEngine()
	interceptorRegistry := &interceptor.Registry{}

	if err := webrtc.ConfigureTWCCHeaderExtensionSender(mediaEngine, interceptorRegistry); err != nil {
		panic(err)
	}
	if err := webrtc.RegisterDefaultInterceptors(mediaEngine, interceptorRegistry); err != nil {
		panic(err)
	}
	settingEngine2 := webrtc.SettingEngine{}
	settingEngine2.SetSCTPMaxReceiveBufferSize(16 * 1024 * 1024)
	settingEngine2.SetReceiveMTU(1500)
	if rsfu.parent.ipFilter != "" {
		settingEngine2.SetIPFilter(rsfu.ipFilterFunc)
	}
	peerConnection, err := webrtc.NewAPI(webrtc.WithSettingEngine(settingEngine2), webrtc.WithMediaEngine(mediaEngine), webrtc.WithInterceptorRegistry(interceptorRegistry)).NewPeerConnection(webrtc.Configuration{})
	rsfu.peerConnection = peerConnection
	rsfu.AddPeerConnectionCallbacks()
	if err != nil {
		panic(err)
	}
	logger.LogWithMessage(NameRemoteSFUConnection, logger.ClientAddedTransceivers, true, true, fmt.Sprintf("providerKey=%s", rsfu.providerKey))
}

func (rsfu *RemoteSFUConnection) SignalRenegotiation() {
	rsfu.mut.Lock()
	logger.LogWithMessage(NameRemoteSFUConnection, logger.MutLock, true, true, "func=SignalRenegotiation")
	defer rsfu.mut.Unlock()
	defer logger.LogWithMessage(NameRemoteSFUConnection, logger.MutUnlock, true, true, "func=SignalRenegotiation")
	rsfu.SignalRenegotiationUnsafe()
}

func (rsfu *RemoteSFUConnection) SignalRenegotiationUnsafe() {
	logger.LogWithMessage(NameRemoteSFUConnection, logger.ClientSignalRenegotiation, true, true, fmt.Sprintf("providerKey=%s", rsfu.providerKey))

	if rsfu.websocket == nil {
		return
	}
	if rsfu.IsNegotiating {
		rsfu.NeedsUpdate = true
		return
	}
	rsfu.IsNegotiating = true
	offer, err := rsfu.peerConnection.CreateOffer(nil)
	if err != nil {
		panic(err)
	}

	if err = rsfu.peerConnection.SetLocalDescription(offer); err != nil {
		panic(err)
	}
	//fmt.Printf("WebRTCSFU: webSocketHandler: SignalRenegotiation: Sending offer to clientID=%d %v+\n", clc.clientID, offer)
	if err = rsfu.websocket.WriteJSONMessageSafe("OfferMessage", offer); err != nil {
		panic(err)
	}
	rsfu.NeedsUpdate = false
}

func (rsfu *RemoteSFUConnection) AddPeerConnectionCallbacks() {
	rsfu.peerConnection.OnICECandidate(func(i *webrtc.ICECandidate) {
		if i == nil {
			return
		}
		fmt.Println("WebRTCSFU: webSocketHandler: OnICECandidate: Found a candidate")
		rsfu.websocket.WriteJSONMessageSafe("CandidateMessage", i.ToJSON().Candidate)
	})

	// If PeerConnection is closed remove it from global list
	rsfu.peerConnection.OnConnectionStateChange(func(p webrtc.PeerConnectionState) {
		// TODO Maybe inform sfu/session manager
		logger.LogWithMessage(NameRemoteSFUConnection, logger.SFUClientConnectionChange, true, true,
			fmt.Sprintf("providerKey=%s state=%s", rsfu.providerKey, p.String()))
		switch p {
		case webrtc.PeerConnectionStateFailed:
			if err := rsfu.peerConnection.Close(); err != nil {
				fmt.Printf("WebRTCSFU: webSocketHandler: ERROR: %s\n", err)
			}
			logger.LogWithMessage(NameRemoteSFUConnection, logger.RemoteProviderConnectForwardingFailed, true, true,
				fmt.Sprintf("providerKey=%s", rsfu.providerKey))
		case webrtc.PeerConnectionStateClosed:
			// Alert other clients that this client is gone
		case webrtc.PeerConnectionStateConnected:
			logger.LogWithMessage(NameRemoteSFUConnection, logger.RemoteProviderConnectForwardingSuccess, true, true,
				fmt.Sprintf("providerKey=%s", rsfu.providerKey))
		}
	})

	rsfu.peerConnection.OnTrack(func(t *webrtc.TrackRemote, trackReceiver *webrtc.RTPReceiver) {
		// Create a track to fan out our incoming video to all peers
		//if t.Kind() == webrtc.RTPCodecTypeAudio {
		//	return
		//}

		logger.LogWithMessage(NameRemoteSFUConnection, logger.ClientOnTrackCalled, true, true,
			fmt.Sprintf("providerKey=%s trackID=%s streamID=%s", rsfu.providerKey, t.ID(), t.StreamID()))
		go func() {
			rtcpBuf := make([]byte, 1500)
			for {
				if _, _, rtcpErr := trackReceiver.Read(rtcpBuf); rtcpErr != nil {
					panic(rtcpErr)
				}
			}
		}()
		var senderTrack *SenderTrack
		exists := false
		rsfu.mut.Lock()
		if t.Kind() == webrtc.RTPCodecTypeVideo {
			senderTrack, exists = rsfu.SenderVideoTracks[t.ID()]
		} else if t.Kind() == webrtc.RTPCodecTypeAudio {
			senderTrack, exists = rsfu.SenderAudioTracks[t.ID()]
		}

		if !exists {
			rsfu.mut.Unlock()
			fmt.Printf("WebRTCSFU: OnTrack: No sender track found for track ID %s\n", t.ID())
			return
		}
		trackBitrate := &TrackBitrate{
			counters: make([]uint32, 20),
		}
		senderTrack.trackBitrate = trackBitrate
		trackLocal := senderTrack.WebRTCTrack
		rsfu.mut.Unlock()

		frames := make(map[uint32]uint32)
		completedFrame := false
		nDroppedFrames := uint32(0)
		fmt.Printf("WebRTCSFU: OnTrack: Adding track %v\n", trackLocal.ID())
		for {

			buf := make([]byte, 1500)
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
			}
			frames[p.FrameNr] += p.SeqLen
			if frames[p.FrameNr] == p.FrameLen { // Can maybe be optimized more because of the string being created for no reason
				completedFrame = true
				logger.LogFrameWithMessage(NameRemoteSFUConnection, logger.FrameFullyRecv, true, true, fmt.Sprintf("providerID=%s trackID=%s totalDroppedFrames=%d", rsfu.providerKey, t.ID(), nDroppedFrames), uint(p.FrameNr))
			}
			//go func() {
			if _, err = trackLocal.Write(buf[:i]); err != nil {
				fmt.Printf("WebRTCSFU: OnTrack: error during write: %s\n", err)
			}
			//}()
		}
	})
}

func (rsfu *RemoteSFUConnection) handleOfferMessage(payload json.RawMessage) {
	offer := webrtc.SessionDescription{}
	err := json.Unmarshal(payload, &offer)
	if err != nil {
		panic(err)
	}
	rsfu.mut.Lock()
	defer rsfu.mut.Unlock()
	if rsfu.providerKey < rsfu.parent.providerKey {
		return
	}
	fmt.Printf("%+v\n", offer)
	err = rsfu.peerConnection.SetRemoteDescription(offer)
	if err != nil {
		panic(err)
	}
	answer, err := rsfu.peerConnection.CreateAnswer(nil)
	if err != nil {
		panic(err)
	}
	if err = rsfu.peerConnection.SetLocalDescription(answer); err != nil {
		panic(err)
	}
	rsfu.IsNegotiating = false
	rsfu.NeedsUpdate = false
	rsfu.websocket.WriteJSONMessageSafe("AnswerMessage", answer)
}

func (rsfu *RemoteSFUConnection) handleAnswerMessage(payload json.RawMessage) {
	answer := webrtc.SessionDescription{}

	if err := json.Unmarshal(payload, &answer); err != nil {
		fmt.Printf("failed to unmarshal payload: %v\n", err)
		return
	}
	rsfu.mut.Lock()
	logger.LogWithMessage(NameRemoteSFUConnection, logger.MutLock, true, true, "func=handleAnswerMessage")
	defer rsfu.mut.Unlock()
	defer logger.LogWithMessage(NameRemoteSFUConnection, logger.MutUnlock, true, true, "func=handleAnswerMessage")
	if err := rsfu.peerConnection.SetRemoteDescription(answer); err != nil {
		panic(err)
	}

	for _, c := range rsfu.pendingCandidatesString {
		if candidateErr := rsfu.peerConnection.AddICECandidate(webrtc.ICECandidateInit{Candidate: c}); candidateErr != nil {
			panic(candidateErr)
		}
	}
	rsfu.IsNegotiating = false
	if rsfu.NeedsUpdate {
		rsfu.SignalRenegotiationUnsafe()
	} else {
	}
}

func (rsfu *RemoteSFUConnection) handleCandidateMessage(payload json.RawMessage) {
	rsfu.mut.Lock()
	logger.LogWithMessage(NameRemoteSFUConnection, logger.MutLock, true, true, "func=handleCandidateMessage")
	defer rsfu.mut.Unlock()
	defer logger.LogWithMessage(NameRemoteSFUConnection, logger.MutUnlock, true, true, "func=handleCandidateMessage")
	desc := rsfu.peerConnection.RemoteDescription()
	var candidate string
	if err := json.Unmarshal(payload, &candidate); err != nil {
		panic(err)
	}

	if desc == nil {
		println("desc in nil")
		rsfu.pendingCandidatesString = append(rsfu.pendingCandidatesString, candidate)
	} else {
		println("adding ice")
		if candidateErr := rsfu.peerConnection.AddICECandidate(webrtc.ICECandidateInit{Candidate: candidate}); candidateErr != nil {
			panic(candidateErr)
		}
	}
}

func (rsfu *RemoteSFUConnection) HandleSpecialMessage(messageType string, payload json.RawMessage) error {
	switch messageType {
	case "OfferMessage":
		rsfu.handleOfferMessage(payload)
	case "AnswerMessage":
		rsfu.handleAnswerMessage(payload)
	case "CandidateMessage":
		rsfu.handleCandidateMessage(payload)
	}
	return nil
}

func (rsfu *RemoteSFUConnection) AddVirtualClient(clientID uint, videoTracks []TrackSimple, audioTracks []TrackSimple) error {
	rsfu.mut.Lock()
	logger.LogWithMessage(NameRemoteSFUConnection, logger.MutLock, true, true, "func=AddVirtualClient")
	defer rsfu.mut.Unlock()
	defer logger.LogWithMessage(NameRemoteSFUConnection, logger.MutUnlock, true, true, "func=AddVirtualClient")
	logger.LogWithMessage(NameRemoteSFUConnection, logger.RemoteProviderAddVirtualClient, true, true, fmt.Sprintf("providerKey=%s clientID=%d nVideoTracks=%d nAudioTracks=%d", rsfu.providerKey, clientID, len(videoTracks), len(audioTracks)))
	if rsfu.websocket == nil {
		return fmt.Errorf("websocket not connected")
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
	audioCodecCapability := webrtc.RTPCodecCapability{
		MimeType:     "audio/pcm",
		ClockRate:    90000,
		Channels:     0,
		SDPFmtpLine:  "",
		RTCPFeedback: nil,
	}
	for _, ts := range videoTracks {
		if _, err := rsfu.peerConnection.AddTransceiverFromKind(webrtc.RTPCodecTypeVideo, webrtc.RTPTransceiverInit{
			Direction: webrtc.RTPTransceiverDirectionRecvonly,
		}); err != nil {
			fmt.Printf("WebRTCSFU: webSocketHandler: ERROR: %s\n", err)
			return err
		}
		trackLocal, err := webrtc.NewTrackLocalStaticRTP(codecCapability, ts.TrackID, ts.TrackID)
		if err != nil {
			panic(err)
		}
		meter := rsfu.parent.overallTrackMetrics.AddTrackMeter(ts.TrackID)
		rsfu.SenderVideoTracks[ts.TrackID] = &SenderTrack{
			TrackID:      ts.TrackID,
			trackBitrate: &TrackBitrate{},
			WebRTCTrack:  trackLocal,
			trackMeter:   meter,
		}
	}
	for _, ts := range audioTracks {
		if _, err := rsfu.peerConnection.AddTransceiverFromKind(webrtc.RTPCodecTypeAudio, webrtc.RTPTransceiverInit{
			Direction: webrtc.RTPTransceiverDirectionRecvonly,
		}); err != nil {
			fmt.Printf("WebRTCSFU: webSocketHandler: ERROR: %s\n", err)
			return err
		}
		trackLocal, err := webrtc.NewTrackLocalStaticRTP(audioCodecCapability, ts.TrackID, ts.TrackID)
		if err != nil {
			panic(err)
		}
		rsfu.SenderAudioTracks[ts.TrackID] = &SenderTrack{
			TrackID:      ts.TrackID,
			trackBitrate: &TrackBitrate{},
			WebRTCTrack:  trackLocal,
		}
	}

	rsfu.websocket.WriteJSONMessageSafe("SubscribeToRemoteClient", SubscribeToTracksMessage{
		ClientID:    clientID,
		VideoTracks: videoTracks,
		AudioTracks: audioTracks,
	})

	return nil
}

func (rsfu *RemoteSFUConnection) HandleSubscribeToRemoteClient(clientID uint, videoTracks []TrackSimple, audioTracks []TrackSimple) error {
	// TODO Maybe rename this function as this should setup the forwarding
	// Call SFU and get client connection
	// Based on clientID and videoTracks/audioTracks
	// Get the senderTrack and attach it
	rsfu.parent.mut.Lock()
	//logger.LogWithMessage(NameRemoteSFUConnection, logger.MutLock, true, true, "func=HandleSubscribeToRemoteClient")
	rsfu.mut.Lock()
	logger.LogWithMessage(NameRemoteSFUConnection, logger.MutLock, true, true, "func=HandleSubscribeToRemoteClient")
	defer rsfu.parent.mut.Unlock()
	defer rsfu.mut.Unlock()
	defer logger.LogWithMessage(NameRemoteSFUConnection, logger.MutUnlock, true, true, "func=HandleSubscribeToRemoteClient")
	rsfu.parent.SubscribeRemoteProviderToClient(rsfu, clientID, videoTracks, audioTracks)
	// Renegotiate
	rsfu.SignalRenegotiationUnsafe()
	return nil
}

func (rsfu *RemoteSFUConnection) AddTrackFromOtherUnsafe(recvTrack *ReceiverTrack) error {
	logger.LogWithMessage(NameRemoteSFUConnection, logger.ClientAddingTrackFromOther, true, true,
		fmt.Sprintf("providerKey=%s trackID=%s",
			rsfu.providerKey, recvTrack.TrackID))
	// TODO Check if trackID appears from session manager track

	if recvTrack.CorrespondingSenderTrack.WebRTCTrack.Kind() == webrtc.RTPCodecTypeVideo {
		rsfu.ReceiverVideoTracks[recvTrack.TrackID] = recvTrack
	} else if recvTrack.CorrespondingSenderTrack.WebRTCTrack.Kind() == webrtc.RTPCodecTypeAudio {
		rsfu.ReceiverAudioTracks[recvTrack.TrackID] = recvTrack
	}
	println("ADDING TRACK", recvTrack.CorrespondingSenderTrack == nil)
	fmt.Printf("TrackID=%s streamID=%s\n", recvTrack.TrackID, recvTrack.CorrespondingSenderTrack.WebRTCTrack.StreamID())
	rtpSender, err := rsfu.peerConnection.AddTrack(recvTrack.CorrespondingSenderTrack.WebRTCTrack)

	if err != nil {
		println("OOPSSS")
		// TODO error handling
		return nil
	}
	recvTrack.RTPSender = rtpSender

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
	return nil
}

func (rsfu *RemoteSFUConnection) ForwardTracksToClient(client *ClientConnection, msg SubscribeToTracksMessage, clientID uint) error {
	logger.LogWithMessage(NameRemoteSFUConnection, logger.MutTryEnter, true, true, "func=ForwardTracksToClient")
	rsfu.mut.Lock()
	logger.LogWithMessage(NameRemoteSFUConnection, logger.MutLock, true, true, "func=ForwardTracksToClient")
	defer rsfu.mut.Unlock()
	defer logger.LogWithMessage(NameRemoteSFUConnection, logger.MutUnlock, true, true, "func=ForwardTracksToClient")
	for _, t := range msg.VideoTracks {
		track, exists := rsfu.SenderVideoTracks[t.TrackID]
		if !exists {
			fmt.Printf("WebRTCSFU: ForwardTracksToClient: No sender video track found for track ID %s\n", t.TrackID)
			continue
		}
		// TODO Change originID to string
		client.AddTrackFromOtherUnsafe("provider", 0, track.TrackID, track)
	}
	for _, t := range msg.AudioTracks {
		track, exists := rsfu.SenderAudioTracks[t.TrackID]
		if !exists {
			fmt.Printf("WebRTCSFU: ForwardTracksToClient: No sender audio track found for track ID %s\n", t.TrackID)
			continue
		}
		client.AddTrackFromOtherUnsafe("provider", 0, track.TrackID, track)
	}
	return nil
}

func (rsfu *RemoteSFUConnection) ipFilterFunc(addr net.IP) bool {
	logger.LogWithMessage(NameRemoteSFUConnection, logger.IPFilterCheck, true, true, fmt.Sprintf("providerKey=%s ip=%s filter=%s", &rsfu.providerKey, addr.String(), rsfu.parent.ipFilter))
	if strings.HasPrefix(addr.String(), rsfu.parent.ipFilter) {
		return true
	}
	return false
}
