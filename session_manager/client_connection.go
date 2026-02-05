package main

import (
	"encoding/json"
	"fmt"
	"sync"
)

const NameClient = "ClientConnection"

const (
	ClientStatusCreated        uint = 0
	ClientStatusConfigReceived uint = 1
	ClientStatusConnected      uint = 2
	ClientStatusReady          uint = 3
	ClientStatusDisconnected   uint = 4
)

type PositionMatrix struct {
	Position            [3]float32    `json:"position"`
	WorldToCameraMatrix [4][4]float32 `json:"worldToCameraMatrix"`
	ProjectionMatrix    [4][4]float32 `json:"projectionMatrix"`
}

type ClientConnection struct {
	parent            *SessionManager
	ClientID          uint
	AuthKey           string
	CodecMode         string
	PipelineType      string
	websocket         *ThreadSafeWebsocket
	config            map[string]interface{}
	IsReady           bool
	Status            uint
	SenderVideoTracks map[string]*ClientTrackInfo
	SenderAudioTracks map[string]*ClientTrackInfo
	RemoteClients     map[uint]*RemoteClient
	PositionMatrix
	ConnectedProviders map[string]*ProviderConnection

	mut sync.Mutex
}

type JoinSessionMessage struct {
	CodecMode string                      `json:"codecMode"`
	Providers []ConnectionProviderMessage `json:"providers"`
}

type ConnectionProviderMessage struct {
	ProviderType     string            `json:"providerType"`
	ProviderKey      string            `json:"providerKey"`
	ProviderSettings json.RawMessage   `json:"providerSettings"`
	VideoTracks      []ClientTrackInfo `json:"videoTracks"`
	AudioTracks      []ClientTrackInfo `json:"audioTracks"`
	ConnectedTo      []string          `json:"connectedTo"` // ProviderKeys of other providers this provider should connect to
}

type ClientTrackInfo struct {
	ClientID              uint            `json:"clientID"`
	ProviderKey           string          `json:"providerKey"`
	TrackID               string          `json:"trackID"`
	CapturerType          string          `json:"capturerType"`
	TrackType             string          `json:"trackType"` // Make it so there is a defaultForTrackType thingy in sessionmanager
	TrackSettings         json.RawMessage `json:"trackSettings"`
	IsConnectedToProvider bool            `json:"isConnectedToProvider"`
}

type ProviderMessage struct {
	ProviderType     string          `json:"providerType"`
	ProviderKey      string          `json:"providerKey"`
	ProviderSettings json.RawMessage `json:"providerSettings"`
}

type ClientTracksAdded struct {
	Providers     []ProviderMessage     `json:"providers"`
	VideoTracks   []ClientTrackInfo     `json:"videoTracks"`
	AudioTracks   []ClientTrackInfo     `json:"audioTracks"`
	RemoteClients []RemoteClientMessage `json:"remoteClients"`
}

func NewClientConnection(parent *SessionManager, clientID uint, authKey string) *ClientConnection {
	LogWithMessage(NameClient, Creating, true, true, fmt.Sprintf("clientID=%d", clientID))
	cl := &ClientConnection{
		parent:            parent,
		ClientID:          clientID,
		AuthKey:           authKey,
		config:            map[string]interface{}{},
		Status:            ClientStatusCreated,
		RemoteClients:     map[uint]*RemoteClient{},
		SenderVideoTracks: map[string]*ClientTrackInfo{}, // TrackID here has to be global unique i.e., clientID + trackID from client
		SenderAudioTracks: map[string]*ClientTrackInfo{},
		mut:               sync.Mutex{},
	}
	Log(NameClient, Created, true, true)
	return cl
}

type ClientFullyConnectedMessage struct {
	ClientID uint   `json:"clientID"`
	AuthKey  string `json:"authKey"`
}

type RemoteClient struct {
	ClientID    uint
	VideoTracks map[string]*ClientTrackInfo
	AudioTracks map[string]*ClientTrackInfo
}

type RemoteClientSimple struct {
	ProviderKey string        `json:"providerKey"`
	ClientID    uint          `json:"clientID"`
	VideoTracks []TrackSimple `json:"videoTracks"`
	AudioTracks []TrackSimple `json:"audioTracks"`
}

func (clc *ClientConnection) SetupClient(ws *ThreadSafeWebsocket) {
	clc.mut.Lock()
	defer clc.mut.Unlock()
	clc.websocket = ws
	clc.startListening()
	// Send clientID + authKey to client
	msg := ClientFullyConnectedMessage{
		ClientID: clc.ClientID,
		AuthKey:  clc.AuthKey,
	}

	clc.websocket.WriteJSONMessageSafe("FullyConnected", msg)
}

type RemoteClientMessage struct {
	ClientID       uint              `json:"clientID"`
	CodecMode      string            `json:"codecMode"`
	ClientSettings string            `json:"clientSettings"`
	PipelineType   string            `json:"pipelineType"`
	VideoTracks    []ClientTrackInfo `json:"videoTracks"`
	AudioTracks    []ClientTrackInfo `json:"audioTracks"`
}

func (clc *ClientConnection) AddRemoteClient(remoteClient *ClientConnection) {
	clc.mut.Lock()
	defer clc.mut.Unlock()
	clc.AddRemoteClientUnsafe(remoteClient)
}

func (clc *ClientConnection) AddRemoteClientUnsafe(remoteClient *ClientConnection) {
	// TODO Maybe filter out some tracks based on preferences?
	clc.RemoteClients[remoteClient.ClientID] = &RemoteClient{
		ClientID:    remoteClient.ClientID,
		VideoTracks: remoteClient.SenderVideoTracks,
		AudioTracks: remoteClient.SenderAudioTracks,
	}
	// Inform client of this new remote client
	videoTracks := make([]ClientTrackInfo, 0, len(remoteClient.SenderVideoTracks))
	for _, t := range remoteClient.SenderVideoTracks {
		videoTracks = append(videoTracks, *t)
	}
	audioTracks := make([]ClientTrackInfo, 0, len(remoteClient.SenderAudioTracks))
	for _, t := range remoteClient.SenderAudioTracks {
		audioTracks = append(audioTracks, *t)
	}
	rmMsg := RemoteClientMessage{
		ClientID:    remoteClient.ClientID,
		CodecMode:   remoteClient.CodecMode,
		VideoTracks: videoTracks,
		AudioTracks: audioTracks,
	}
	clc.websocket.WriteJSONMessageSafe("RemoteClientAdded", rmMsg)
}

func (clc *ClientConnection) startListening() {
	go func() {
		for {
			var msg ClientMessage
			if err := clc.websocket.ReadJSON(&msg); err != nil {
				fmt.Printf("SessionManager: webSocketHandler: ReadMessage for client %d: error %s\n", clc.ClientID, err.Error())
				clc.onClose()
				break
			}
			LogWithMessage(NameClient, ReceivedWSMessage, true, true, fmt.Sprintf("messageType=%s", msg.MessageType))
			switch msg.MessageType {
			case "JoinMessage":
				{
					clc.handleJoinMessage(msg.Message)
				}
			case "ClientPositionUpdate":
				{
					clc.handlePositionMatrixUpdate(msg.Message)
				}
			}

		}
	}()
}

type SessionJoinedMessage struct {
	DefaultProvider string                      `json:"defaultProvider"`
	CodecMode       string                      `json:"codecMode"`
	Providers       []ConnectionProviderMessage `json:"providers"`
	// TODO Add clients to this
}

func (clc *ClientConnection) handleJoinMessage(payload json.RawMessage) {
	var msg JoinSessionMessage
	if err := json.Unmarshal(payload, &msg); err != nil {
		fmt.Printf("failed to unmarshal payload: %v\n", err)
		return
	}
	fmt.Printf("Received JoinSessionMessage: %+v\n", msg)
	clc.mut.Lock()
	defer clc.mut.Unlock()
	clc.CodecMode = msg.CodecMode

	// Validate providers
	validProviders := []ConnectionProviderMessage{}
	validProviderConnections := map[string]*ProviderConnection{}
	for i := range msg.Providers {
		provider := &msg.Providers[i]
		pc := clc.parent.CreateProvider(provider.ProviderType, provider.ProviderKey, "", 0, provider.ConnectedTo) // TODO Fix port + address
		if pc == nil {

			continue
			// TODO Log invalid provider
		}
		for i := range provider.VideoTracks {
			track := provider.VideoTracks[i]
			track.ProviderKey = provider.ProviderKey
			track.TrackID = fmt.Sprintf("cl%d_%s", clc.ClientID, track.TrackID) // Make trackID globally unique
			provider.VideoTracks[i] = track
			clc.SenderVideoTracks[track.TrackID] = &track
		}
		validProviderConnections[provider.ProviderKey] = pc
		validProviders = append(validProviders, *provider)
	}

	clientMsg := SessionJoinedMessage{
		DefaultProvider: "",
		CodecMode:       msg.CodecMode,  // TODO Validate codec mode based on tracks
		Providers:       validProviders, // TODO Filter this out
	}
	clc.websocket.WriteJSONMessageSafe("SessionJoined", clientMsg)
	// Add remote clients to new client
	println("sddsdssddsdsds")
	for _, clOther := range clc.parent.clients {
		if clOther.ClientID == clc.ClientID {
			continue
		}
		clOther.AddRemoteClient(clc)
		clc.AddRemoteClientUnsafe(clOther)
	}
	println("sddsdssdds")
	for i := range validProviders {
		validProviderMessage := &validProviders[i]
		pc := validProviderConnections[validProviderMessage.ProviderKey]

		videoTrackPtrs := make([]*ClientTrackInfo, len(validProviderMessage.VideoTracks))
		for j := range validProviderMessage.VideoTracks {
			videoTrackPtrs[j] = &validProviderMessage.VideoTracks[j]
		}
		audioTrackPtrs := make([]*ClientTrackInfo, len(validProviderMessage.AudioTracks))
		for j := range validProviderMessage.AudioTracks {
			audioTrackPtrs[j] = &validProviderMessage.AudioTracks[j]
		}

		pc.AddNewClient(clc.ClientID, clc.AuthKey, videoTrackPtrs, audioTrackPtrs)
	}

}

func (clc *ClientConnection) handlePositionMatrixUpdate(payload json.RawMessage) {
	var posMatrix PositionMatrix
	if err := json.Unmarshal(payload, &posMatrix); err != nil {
		fmt.Printf("failed to unmarshal position matrix payload: %v\n", err)
		return
	}
	clc.mut.Lock()
	defer clc.mut.Unlock()
	clc.PositionMatrix = posMatrix
	println("Updated position matrix for client", clc.ClientID, "to", posMatrix.Position[0], posMatrix.Position[1], posMatrix.Position[2])
	for _, pc := range clc.ConnectedProviders {
		pc.UpdateClientPositionMatrix(clc.ClientID, posMatrix)
	}
}

func (clc *ClientConnection) SetTracksToConnected(clientID uint, videoTracks []TrackSimple, audioTracks []TrackSimple) {
	clc.mut.Lock()
	defer clc.mut.Unlock()

	for _, t := range videoTracks {
		track, exists := clc.SenderVideoTracks[t.TrackID]
		if !exists {
			continue
		}
		track.IsConnectedToProvider = true
	}

	for _, t := range audioTracks {
		track, exists := clc.SenderAudioTracks[t.TrackID]
		if !exists {
			continue
		}
		track.IsConnectedToProvider = true
	}

}

func (clc *ClientConnection) onClose() {
	clc.parent.OnClientClose(clc)
}
