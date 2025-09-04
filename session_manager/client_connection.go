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

type ClientConnection struct {
	parent            *SessionManager
	ClientID          uint
	AuthKey           string
	websocket         *ThreadSafeWebsocket
	config            map[string]interface{}
	IsReady           bool
	Status            uint
	SenderVideoTracks map[string]ClientTrackInfo
	SenderAudioTracks map[string]ClientTrackInfo
	RemoteClients     map[uint]RemoteClient

	mut sync.Mutex
}

type JoinSessionMessage struct {
	Providers []ConnectionProviderMessage `json:"providers"`
}

type ConnectionProviderMessage struct {
	ProviderType     string            `json:"providerType"`
	ProviderKey      string            `json:"providerKey"`
	ProviderSettings json.RawMessage   `json:"providerSettings"`
	VideoTracks      []ClientTrackInfo `json:"videoTracks"`
	AudioTracks      []ClientTrackInfo `json:"audioTracks"`
}

type ClientTrackInfo struct {
	ClientID      uint            `json:"clientID"`
	ProviderKey   string          `json:"providerKey"`
	TrackID       string          `json:"trackID"`
	CapturerType  string          `json:"capturerType"`
	TrackType     string          `json:"trackType"` // Make it so there is a defaultForTrackType thingy in sessionmanager
	TrackSettings json.RawMessage `json:"trackSettings"`
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
		RemoteClients:     map[uint]RemoteClient{},
		SenderVideoTracks: map[string]ClientTrackInfo{}, // TrackID here has to be global unique i.e., clientID + trackID from client
		SenderAudioTracks: map[string]ClientTrackInfo{},
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
	VideoTracks map[string]ClientTrackInfo
	AudioTracks map[string]ClientTrackInfo
}

type RemoteClientSimple struct {
	ClientID    uint              `json:"clientID"`
	VideoTracks []ClientTrackInfo `json:"videoTracks"`
	AudioTracks []ClientTrackInfo `json:"audioTracks"`
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
	ClientID    uint              `json:"clientID"`
	VideoTracks []ClientTrackInfo `json:"videoTracks"`
	AudioTracks []ClientTrackInfo `json:"audioTracks"`
}

func (clc *ClientConnection) AddRemoteClient(remoteClient *ClientConnection) {
	clc.mut.Lock()
	defer clc.mut.Unlock()
	// TODO Maybe filter out some tracks based on preferences?
	clc.RemoteClients[remoteClient.ClientID] = RemoteClient{
		ClientID:    remoteClient.ClientID,
		VideoTracks: remoteClient.SenderVideoTracks,
		AudioTracks: remoteClient.SenderAudioTracks,
	}
	// Inform client of this new remote client
	videoTracks := make([]ClientTrackInfo, 0, len(remoteClient.SenderVideoTracks))
	for _, t := range remoteClient.SenderVideoTracks {
		videoTracks = append(videoTracks, t)
	}
	audioTracks := make([]ClientTrackInfo, 0, len(remoteClient.SenderAudioTracks))
	for _, t := range remoteClient.SenderAudioTracks {
		audioTracks = append(audioTracks, t)
	}
	rmMsg := RemoteClientMessage{
		ClientID:    remoteClient.ClientID,
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
				fmt.Printf("SessionManager: webSocketHandler: ReadMessage: error %s\n", err.Error())
				clc.onClose()
				break
			}

			switch msg.MessageType {
			case "JoinMessage":
				clc.handleJoinMessage(msg.Message)
			}
		}
	}()
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
	clVideoTracks := []ClientTrackInfo{}
	clAudioTracks := []ClientTrackInfo{}
	clProviders := []ProviderMessage{}
	providersAddedToMessage := map[string]bool{}
	for i := range msg.Providers {
		provider := &msg.Providers[i]
		providersAddedToMessage[provider.ProviderKey] = true
		clProviders = append(clProviders, ProviderMessage{
			ProviderType:     provider.ProviderType,
			ProviderKey:      provider.ProviderKey,
			ProviderSettings: provider.ProviderSettings,
		})
		pc := clc.parent.CreateProvider(provider.ProviderType, provider.ProviderKey, "", 0) // TODO Fix port + address
		if pc == nil {
			continue
			// TODO Log invalid provider
		}
		processTracks := func(tracks []ClientTrackInfo, providerKey string, senderTracks map[string]ClientTrackInfo, collectedTracks *[]ClientTrackInfo) {
			for j := range tracks {
				track := &tracks[j]
				track.ProviderKey = providerKey
				track.TrackID = fmt.Sprintf("cl%d_%s", clc.ClientID, track.TrackID)
				senderTracks[track.TrackID] = *track
				*collectedTracks = append(*collectedTracks, *track)
			}
		}
		processTracks(provider.VideoTracks, provider.ProviderKey, clc.SenderVideoTracks, &clVideoTracks)
		processTracks(provider.AudioTracks, provider.ProviderKey, clc.SenderAudioTracks, &clAudioTracks)
		pc.AddNewClient(clc.ClientID, clc.AuthKey, provider.VideoTracks, provider.AudioTracks)
	}
	// Alert other clients of this client

	// Add providers and remote clients to message

	// Send updated tracks to client
	ClientTracksAdded := ClientTracksAdded{
		Providers:   clProviders,
		VideoTracks: clVideoTracks,
		AudioTracks: clAudioTracks,
	}
	fmt.Printf("Sending ClientTracksAdded: %+v\n", ClientTracksAdded)
	clc.websocket.WriteJSONMessageSafe("ClientTracksAdded", ClientTracksAdded)
}

func (clc *ClientConnection) onClose() {
	clc.parent.OnClientClose(clc)
}
