package main

import (
	"encoding/json"
	"fmt"
	"sync"
)

const NameProvider = "ProviderConnection"

type ProviderTrackSimple struct {
	TrackID     string
	IsConnected bool
}

type ProviderClient struct {
	IsConnected bool
	ClientID    uint
	VideoTracks map[string]*ProviderTrackSimple
	AudioTracks map[string]*ProviderTrackSimple
}

func (pc *ProviderClient) GetConnectedVideoTracks() []TrackSimple {
	var connectedTracks []TrackSimple
	for _, track := range pc.VideoTracks {
		if track.IsConnected {
			connectedTracks = append(connectedTracks, TrackSimple{TrackID: track.TrackID, IsConnected: true})
		}
	}
	return connectedTracks
}

func (pc *ProviderClient) GetConnectedAudioTracks() []TrackSimple {
	var connectedTracks []TrackSimple
	for _, track := range pc.AudioTracks {
		if track.IsConnected {
			connectedTracks = append(connectedTracks, TrackSimple{TrackID: track.TrackID, IsConnected: true})
		}
	}
	return connectedTracks
}

type ProviderNewClient struct {
	ClientID    uint
	AuthKey     string
	VideoTracks []*ClientTrackInfo
	AudioTracks []*ClientTrackInfo
}

type ProviderNewRemoteProvider struct {
	ProviderType        string `json:"providerType"`
	ProviderKey         string `json:"providerKey"`
	Address             string `json:"address"`
	Port                uint   `json:"port"`
	MakeProviderConnect bool   `json:"makeProviderConnect"`
}

func (pc *ProviderRemoteProviderClient) GetConnectedVideoTracks() []TrackSimple {
	var connectedTracks []TrackSimple
	for _, track := range pc.VideoTracks {
		if track.IsConnected {
			connectedTracks = append(connectedTracks, TrackSimple{TrackID: track.TrackID, IsConnected: true})
		}
	}
	return connectedTracks
}

func (pc *ProviderRemoteProviderClient) GetConnectedAudioTracks() []TrackSimple {
	var connectedTracks []TrackSimple
	for _, track := range pc.AudioTracks {
		if track.IsConnected {
			connectedTracks = append(connectedTracks, TrackSimple{TrackID: track.TrackID, IsConnected: true})
		}
	}
	return connectedTracks
}

type ProviderRemoteProviderClient struct {
	ClientID    uint
	VideoTracks map[string]*ProviderTrackSimple
	AudioTracks map[string]*ProviderTrackSimple
}

type ProviderRemoteProvider struct {
	ProviderType         string
	ProviderKey          string
	Address              string
	Port                 uint
	ForwardedVideoTracks map[string]*ProviderTrackSimple
	ForwardedAudioTracks map[string]*ProviderTrackSimple
	MakeProviderConnect  bool
}

type ProviderConnection struct {
	parent                  *SessionManager
	ProviderType            string
	ProviderKey             string
	Address                 string
	Port                    uint
	AuthKey                 string
	websocket               *ThreadSafeWebsocket
	config                  map[string]interface{}
	Clients                 map[uint]*ProviderClient
	VirtualClients          map[uint]*ProviderRemoteProviderClient
	RemoteProviders         map[string]*ProviderRemoteProvider
	IsReady                 bool
	newClientBuffer         []ProviderNewClient
	newRemoteProviderBuffer []ProviderNewRemoteProvider
	mut                     sync.Mutex
}

func NewProviderConnection(parent *SessionManager, providerType string, providerKey string, address string, port uint, authKey string, config map[string]interface{}) *ProviderConnection {
	LogWithMessage(NameProvider, Creating, true, true, fmt.Sprintf("providerKey=%s", providerKey))
	pro := &ProviderConnection{
		parent:                  parent,
		ProviderType:            providerType,
		ProviderKey:             providerKey,
		Address:                 address,
		Port:                    port,
		AuthKey:                 authKey,
		config:                  config,
		Clients:                 map[uint]*ProviderClient{},
		VirtualClients:          map[uint]*ProviderRemoteProviderClient{},
		RemoteProviders:         map[string]*ProviderRemoteProvider{},
		newClientBuffer:         []ProviderNewClient{},
		newRemoteProviderBuffer: []ProviderNewRemoteProvider{},
		mut:                     sync.Mutex{},
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
	// TODO Send buffer providers
	for _, newProvider := range pc.newRemoteProviderBuffer {
		pc._addNewRemoteProvider(newProvider.ProviderType, newProvider.ProviderKey, newProvider.Address, newProvider.Port, newProvider.MakeProviderConnect)
	}
	for _, newClient := range pc.newClientBuffer {
		pc._addNewClient(newClient.ClientID, newClient.AuthKey, newClient.VideoTracks, newClient.AudioTracks)
	}
	pc.newRemoteProviderBuffer = pc.newRemoteProviderBuffer[:0]
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
	TrackID     string `json:"trackID"`
	IsConnected bool   `json:"isConnected"`
}

type ProviderClientAddedMessage struct {
	ClientID            uint          `json:"clientID"`
	SenderVideoTracks   []TrackSimple `json:"senderVideoTracks"`
	ReceiverVideoTracks []TrackSimple `json:"receiverVideoTracks"`
	SenderAudioTracks   []TrackSimple `json:"senderAudioTracks"`
	ReceiverAudioTracks []TrackSimple `json:"receiverAudioTracks"`
}

type ClientAddedToProviderMessage struct {
	ProviderType      string                 `json:"providerType"`
	ProviderKey       string                 `json:"providerKey"`
	Address           string                 `json:"address"`
	Port              uint                   `json:"port"`
	Config            map[string]interface{} `json:"config"`
	SenderVideoTracks []TrackSimple          `json:"senderVideoTracks"`
	SenderAudioTracks []TrackSimple          `json:"senderAudioTracks"`
	RemoteClients     []RemoteClientSimple   `json:"remoteClients"`
}

func (clc *ProviderConnection) AddNewClient(clientID uint, authKey string, videoTracks []*ClientTrackInfo, audioTracks []*ClientTrackInfo) { // TODO Maybe add auth key
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

func (pc *ProviderConnection) _addNewClient(clientID uint, authKey string, videoTracks []*ClientTrackInfo, audioTracks []*ClientTrackInfo) {
	LogWithMessage(NameProvider, AddingClientToProvider, true, true, fmt.Sprintf("providerKey=%s clientID=%d", pc.ProviderKey, clientID))
	msgContent := ProviderNewClientMessage{
		ClientID:          clientID,
		AuthKey:           authKey,
		SenderVideoTracks: []ClientTrackInfo{},
		SenderAudioTracks: []ClientTrackInfo{},
	}
	client := &ProviderClient{
		ClientID:    clientID,
		VideoTracks: map[string]*ProviderTrackSimple{},
		AudioTracks: map[string]*ProviderTrackSimple{},
	}
	pc.Clients[clientID] = client
	for _, track := range videoTracks {
		client.VideoTracks[track.TrackID] = &ProviderTrackSimple{
			TrackID:     track.TrackID,
			IsConnected: false,
		}
		msgContent.SenderVideoTracks = append(msgContent.SenderVideoTracks, *track)
	}
	for _, track := range audioTracks {
		client.AudioTracks[track.TrackID] = &ProviderTrackSimple{
			TrackID:     track.TrackID,
			IsConnected: false,
		}
		msgContent.SenderAudioTracks = append(msgContent.SenderAudioTracks, *track)
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
			case "VirtualClientAdded":
				clc.handleVirtualClientAdded(msg.Message)
			}
		}
	}()
}

type ProviderTracksConnectedMessage struct {
	ProviderKey string        `json:"providerKey"`
	ClientID    uint          `json:"clientID"`
	VideoTracks []TrackSimple `json:"videoTracks"`
	AudioTracks []TrackSimple `json:"audioTracks"`
}

type ProviderRemoteProviderClientMessage struct {
	ProviderKey string        `json:"providerKey"`
	ClientID    uint          `json:"clientID"`
	VideoTracks []TrackSimple `json:"videoTracks"`
	AudioTracks []TrackSimple `json:"audioTracks"`
}

func (pc *ProviderConnection) handleClientAdded(payload json.RawMessage) {
	var msg ProviderClientAddedMessage
	if err := json.Unmarshal(payload, &msg); err != nil {
		fmt.Printf("failed to unmarshal payload: %v\n", err)
		return
	}
	fmt.Printf("Received ClientAddedMessage: %+v\n", msg)
	// Set ProviderClient Tracks to connected
	pc.parent.OnClientAddedToProvider(pc, msg)
}

func (pc *ProviderConnection) handleVirtualClientAdded(payload json.RawMessage) {
	var msg ProviderRemoteProviderClientMessage
	if err := json.Unmarshal(payload, &msg); err != nil {
		fmt.Printf("failed to unmarshal payload: %v\n", err)
		return
	}
	fmt.Printf("Received VirtualClientAddedMessage: %+v\n", msg)
	pc.parent.OnVirtualClientAddedToProvider(pc, msg)
}

type ProviderRemoteProviderAddedMessagage struct {
	ProviderType string `json:"providerType"`
	ProviderKey  string `json:"providerKey"`
	Address      string `json:"address"`
	Port         uint   `json:"port"`
}

func (pc *ProviderConnection) AddRemoteProvider(providerType string, providerKey string, address string, port uint, makeProviderConnect bool) {
	pc.mut.Lock()
	defer pc.mut.Unlock()
	// TODO Send websocket providerAdded message
	if !pc.IsReady {
		// Add to buffer
		pc.newRemoteProviderBuffer = append(pc.newRemoteProviderBuffer, ProviderNewRemoteProvider{
			ProviderType:        providerType,
			ProviderKey:         providerKey,
			Address:             address,
			Port:                port,
			MakeProviderConnect: makeProviderConnect,
		})
		return
	}
	pc._addNewRemoteProvider(providerType, providerKey, address, port, makeProviderConnect)
}

func (pc *ProviderConnection) _addNewRemoteProvider(providerType string, providerKey string, address string, port uint, makeProviderConnect bool) {
	pc.RemoteProviders[providerKey] = &ProviderRemoteProvider{
		ProviderType:         providerType,
		ProviderKey:          providerKey,
		Address:              address,
		Port:                 port,
		ForwardedVideoTracks: map[string]*ProviderTrackSimple{},
		ForwardedAudioTracks: map[string]*ProviderTrackSimple{},
	}
	// Only force one of the two providers to initate the connection
	println("test")
	if makeProviderConnect {
		msg := ProviderRemoteProviderAddedMessagage{
			ProviderType: providerType,
			ProviderKey:  providerKey,
			Address:      address,
			Port:         port,
		}
		println("adding remote provider")
		pc.websocket.WriteJSONMessageSafe("RemoteProviderConnected", msg)
	}

}

func (clc *ProviderConnection) onClose() {
	clc.parent.OnProviderClose(clc)
}
