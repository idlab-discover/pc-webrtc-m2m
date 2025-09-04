package main

import (
	"encoding/json"
	"fmt"
	"sync"
)

const NameProvider = "ProviderConnection"

type ProviderNewClient struct {
	ClientID    uint
	AuthKey     string
	VideoTracks []ClientTrackInfo
	AudioTracks []ClientTrackInfo
}

type ProviderConnection struct {
	parent          *SessionManager
	ProviderKey     string
	Address         string
	Port            uint
	AuthKey         string
	websocket       *ThreadSafeWebsocket
	config          map[string]interface{}
	videoTracks     map[string]ClientTrackInfo
	audioTracks     map[string]ClientTrackInfo
	IsReady         bool
	newClientBuffer []ProviderNewClient
	mut             sync.Mutex
}

func NewProviderConnection(parent *SessionManager, providerKey string, address string, port uint, authKey string, config map[string]interface{}) *ProviderConnection {
	Log(NameProvider, Creating, true, true)
	pro := &ProviderConnection{
		parent:          parent,
		ProviderKey:     providerKey,
		Address:         address,
		Port:            port,
		AuthKey:         authKey,
		config:          config,
		videoTracks:     map[string]ClientTrackInfo{},
		audioTracks:     map[string]ClientTrackInfo{},
		newClientBuffer: []ProviderNewClient{},
		mut:             sync.Mutex{},
	}
	Log(NameProvider, Created, true, true)
	return pro
}

func (pc *ProviderConnection) SetupProvider(ws *ThreadSafeWebsocket) {
	pc.mut.Lock()
	defer pc.mut.Unlock()
	pc.websocket = ws
	pc.startListening()
	msgBytes, err := json.Marshal(pc.config)
	if err != nil {
		fmt.Printf("WebRTCSFU: webSocketHandler: OnICECandidate: ERROR: %s\n", err)
		return
	}
	m := ClientMessage{
		MessageType: "FullyConnected",
		Message:     json.RawMessage(msgBytes),
	}
	println("ready", len(pc.newClientBuffer))
	pc.IsReady = true
	for _, newClient := range pc.newClientBuffer {
		pc._addNewClient(newClient.ClientID, newClient.AuthKey, newClient.VideoTracks, newClient.AudioTracks)
	}
	pc.newClientBuffer = pc.newClientBuffer[:0]
	pc.websocket.WriteJSONSafe(m)
}

type ProviderNewClientMessage struct {
	ClientID            uint              `json:"clientID"`
	AuthKey             string            `json:"authKey"`
	SenderVideoTracks   []ClientTrackInfo `json:"senderVideoTracks"`
	ReceiverVideoTracks []ClientTrackInfo `json:"receiverVideoTracks"`
	SenderAudioTracks   []ClientTrackInfo `json:"senderAudioTracks"`
	ReceiverAudioTracks []ClientTrackInfo `json:"receiverAudioTracks"`
}

type TrackSimple struct {
	TrackID string `json:"trackID"`
}

type ProviderClientAddedMessage struct {
	ClientID            uint          `json:"clientID"`
	SenderVideoTracks   []TrackSimple `json:"senderVideoTracks"`
	ReceiverVideoTracks []TrackSimple `json:"receiverVideoTracks"`
	SenderAudioTracks   []TrackSimple `json:"senderAudioTracks"`
	ReceiverAudioTracks []TrackSimple `json:"receiverAudioTracks"`
}

type ClientAddedToProviderMessage struct {
	ProviderKey string `json:"providerKey"`
	Address     string `json:"address"`
	Port        uint   `json:"port"`
}

func (clc *ProviderConnection) AddNewClient(clientID uint, authKey string, videoTracks []ClientTrackInfo, audioTracks []ClientTrackInfo) { // TODO Maybe add auth key
	clc.mut.Lock()
	defer clc.mut.Unlock()

	if !clc.IsReady {
		LogWithMessage(NameProvider, AddingClientToProviderBuffer, true, true, fmt.Sprintf("providerKey=%s clientID=%d", clc.ProviderKey, clientID))
		clc.newClientBuffer = append(clc.newClientBuffer, ProviderNewClient{
			ClientID:    clientID,
			AuthKey:     authKey,
			VideoTracks: videoTracks,
			AudioTracks: audioTracks,
		})
		return
	}
	clc._addNewClient(clientID, authKey, videoTracks, audioTracks)
}

func (pc *ProviderConnection) _addNewClient(clientID uint, authKey string, videoTracks []ClientTrackInfo, audioTracks []ClientTrackInfo) {
	LogWithMessage(NameProvider, AddingClientToProvider, true, true, fmt.Sprintf("providerKey=%s clientID=%d", pc.ProviderKey, clientID))
	msgContent := ProviderNewClientMessage{
		ClientID:          clientID,
		AuthKey:           authKey,
		SenderVideoTracks: []ClientTrackInfo{},
		SenderAudioTracks: []ClientTrackInfo{},
	}
	for _, track := range videoTracks {
		pc.videoTracks[track.TrackID] = track
		msgContent.SenderVideoTracks = append(msgContent.SenderVideoTracks, track)
	}
	for _, track := range audioTracks {
		pc.audioTracks[track.TrackID] = track
		msgContent.SenderAudioTracks = append(msgContent.SenderAudioTracks, track)
	}
	msgBytes, err := json.Marshal(msgContent)
	if err != nil {
		fmt.Printf("ProviderConnection: AddNewClient: ERROR: %s\n", err)
		return
	}
	msg := ClientMessage{
		MessageType: "ClientConnected",
		Message:     json.RawMessage(msgBytes),
	}

	pc.websocket.WriteJSONSafe(msg)
}

func (clc *ProviderConnection) startListening() {
	go func() {
		for {
			var msg ClientMessage
			if err := clc.websocket.ReadJSON(&msg); err != nil {
				fmt.Printf("SessionManager: webSocketHandler: ReadMessage: error %s\n", err.Error())
				clc.onClose()
				break
			}

			switch msg.MessageType {
			case "ClientAdded":
				clc.handleClientAdded(msg.Message)
			}
		}
	}()
}

func (clc *ProviderConnection) handleClientAdded(payload json.RawMessage) {
	var msg ProviderClientAddedMessage
	if err := json.Unmarshal(payload, &msg); err != nil {
		fmt.Printf("failed to unmarshal payload: %v\n", err)
		return
	}
	fmt.Printf("Received ClientAddedMessage: %+v\n", msg)
	clc.parent.OnClientAddedToProvider(clc, msg)
}

func (clc *ProviderConnection) onClose() {
	clc.parent.OnProviderClose(clc)
}
