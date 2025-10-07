package main

import (
	"encoding/json"
	"fmt"

	"github.com/pion/interceptor"
	"github.com/pion/webrtc/v4"
)

const NameRemoteSFUConnection = "RemoteSFUConnection"

type RemoteSFUConnection struct {
	ProviderConnectionBase
	peerConnection          *webrtc.PeerConnection
	IsNegotiating           bool
	NeedsUpdate             bool
	SenderVideoTracks       map[string]*SenderTrack
	SenderAudioTracks       map[string]*SenderTrack
	ReceiverVideoTracks     map[string]*ReceiverTrack
	ReceiverAudioTracks     map[string]*ReceiverTrack
	pendingCandidatesString []string
}

func NewRemoteSFUConnection(providerKey string, address string, port uint, authKey string) *RemoteSFUConnection {
	return &RemoteSFUConnection{
		ProviderConnectionBase: NewProviderConnectionBase("sfu", providerKey, address, port, authKey),
		SenderVideoTracks:      make(map[string]*SenderTrack),
		SenderAudioTracks:      make(map[string]*SenderTrack),
		ReceiverVideoTracks:    make(map[string]*ReceiverTrack),
		ReceiverAudioTracks:    make(map[string]*ReceiverTrack),
		IsNegotiating:          false,
		NeedsUpdate:            false,
	}
}

func (rsfu *RemoteSFUConnection) SetupForwarding() error {
	LogWithMessage(NameRemoteSFUConnection, RemoteProviderConnectForwardingStarted, true, true, fmt.Sprintf("providerKey=%s", rsfu.providerKey))
	rsfu.SetupPeerConnection()
	return nil
}

func (rsfu *RemoteSFUConnection) SetupPeerConnection() {
	LogWithMessage(NameRemoteSFUConnection, ClientAddingTransceivers, true, true,
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
	peerConnection, err := webrtc.NewAPI(webrtc.WithSettingEngine(settingEngine2), webrtc.WithMediaEngine(mediaEngine), webrtc.WithInterceptorRegistry(interceptorRegistry)).NewPeerConnection(webrtc.Configuration{})
	rsfu.peerConnection = peerConnection
	rsfu.AddPeerConnectionCallbacks()
	if err != nil {
		panic(err)
	}
	LogWithMessage(NameRemoteSFUConnection, ClientAddedTransceivers, true, true, fmt.Sprintf("providerKey=%s", rsfu.providerKey))
}

func (rsfu *RemoteSFUConnection) SignalRenegotiation() {
	rsfu.mut.Lock()
	defer rsfu.mut.Unlock()
	rsfu.SignalRenegotiationUnsafe()
}

func (rsfu *RemoteSFUConnection) SignalRenegotiationUnsafe() {
	LogWithMessage(NameRemoteSFUConnection, ClientSignalRenegotiation, true, true, fmt.Sprintf("providerKey=%s", rsfu.providerKey))

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
		LogWithMessage(NameRemoteSFUConnection, SFUClientConnectionChange, true, true,
			fmt.Sprintf("providerKey=%s state=%s", rsfu.providerKey, p.String()))
		switch p {
		case webrtc.PeerConnectionStateFailed:
			if err := rsfu.peerConnection.Close(); err != nil {
				fmt.Printf("WebRTCSFU: webSocketHandler: ERROR: %s\n", err)
			}
			LogWithMessage(NameRemoteSFUConnection, RemoteProviderConnectForwardingFailed, true, true,
				fmt.Sprintf("providerKey=%s", rsfu.providerKey))
		case webrtc.PeerConnectionStateClosed:
			// Alert other clients that this client is gone
		case webrtc.PeerConnectionStateConnected:
			LogWithMessage(NameRemoteSFUConnection, RemoteProviderConnectForwardingSuccess, true, true,
				fmt.Sprintf("providerKey=%s", rsfu.providerKey))
		}
	})

	rsfu.peerConnection.OnTrack(func(t *webrtc.TrackRemote, trackReceiver *webrtc.RTPReceiver) {
		// Create a track to fan out our incoming video to all peers
		//if t.Kind() == webrtc.RTPCodecTypeAudio {
		//	return
		//}

		LogWithMessage(NameRemoteSFUConnection, ClientOnTrackCalled, true, true,
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

		fmt.Printf("WebRTCSFU: OnTrack: Adding track %v\n", trackLocal.ID())
		for {

			buf := make([]byte, 15000)
			i, _, err := t.Read(buf)
			if err != nil {
				fmt.Printf("WebRTCSFU: OnTrack: error during read: %s\n", err)
				break
			}

			//go func() {
			if _, err = trackLocal.Write(buf[:i]); err != nil {
				fmt.Printf("WebRTCSFU: OnTrack: error during write: %s\n", err)
			}
			//}()
		}
	})
}

func (rsfu *RemoteSFUConnection) handleAnswerMessage(payload json.RawMessage) {
	answer := webrtc.SessionDescription{}

	if err := json.Unmarshal(payload, &answer); err != nil {
		fmt.Printf("failed to unmarshal payload: %v\n", err)
		return
	}
	rsfu.mut.Lock()
	defer rsfu.mut.Unlock()
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
		println("NEED RENEGGGGG")
		rsfu.SignalRenegotiationUnsafe()
	} else {
		println("DONT NEED NEEG")
	}
}

func (rsfu *RemoteSFUConnection) handleCandidateMessage(payload json.RawMessage) {
	rsfu.mut.Lock()
	defer rsfu.mut.Unlock()
	desc := rsfu.peerConnection.RemoteDescription()
	var candidate string
	if err := json.Unmarshal(payload, &candidate); err != nil {
		panic(err)
	}

	if desc == nil {
		rsfu.pendingCandidatesString = append(rsfu.pendingCandidatesString, candidate)
	} else {
		if candidateErr := rsfu.peerConnection.AddICECandidate(webrtc.ICECandidateInit{Candidate: candidate}); candidateErr != nil {
			panic(candidateErr)
		}
	}
}
