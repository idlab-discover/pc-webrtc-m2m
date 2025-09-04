package main

import (
	"encoding/json"
	"fmt"
	"log"
	"net/url"
	"os"
	"sync"

	"github.com/gorilla/websocket"
)

const NameManagerConnection = "SessionManagerConnection"

const (
	FullyConnected     uint32 = 0
	ClientConnected    uint32 = 1
	ClientDisconnected uint32 = 2
	AddTrack           uint32 = 3
	RemoveTrack        uint32 = 4
)

type SessionManagerConnection struct {
	managerIP         string
	clientID          uint
	authKey           string
	providersPath     string
	providers         map[string]*SFUConnection
	localClient       *ConnectedClient
	remoteClient      map[uint]*ConnectedClient
	bufferedProviders map[string]AddedToProviderMessage
	conn              *ThreadSafeWebsocket
	mut               sync.Mutex
}

type SessionManagerMessage struct {
	MessageType string          `json:"messageType"`
	Message     json.RawMessage `json:"message"`
}

type AddedToProviderMessage struct {
	ProviderKey string `json:"providerKey"`
	Address     string `json:"address"`
	Port        uint   `json:"port"`
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

func NewSessionManagerConnection(managerIP string, preferredClientID uint, providersPath string) (*SessionManagerConnection, error) {
	LogWithMessage(NameManagerConnection, Creating, true, true, fmt.Sprintf("managerIP=%s preferredclientID=%d", managerIP, preferredClientID))
	u := url.URL{
		Scheme: "ws",
		Host:   managerIP,
		Path:   "/websocket_client",
	}
	query := url.Values{}
	query.Set("preferredClientID", fmt.Sprintf("%d", preferredClientID))
	u.RawQuery = query.Encode()

	conn, _, err := websocket.DefaultDialer.Dial(u.String(), nil)
	if err != nil {
		Log(NameManagerConnection, Failed, true, true)
		return nil, fmt.Errorf("failed to connect to manager: %w", err)
	}
	smc := &SessionManagerConnection{
		managerIP:         managerIP,
		providersPath:     providersPath,
		providers:         map[string]*SFUConnection{},
		remoteClient:      make(map[uint]*ConnectedClient),
		bufferedProviders: map[string]AddedToProviderMessage{},
		conn: &ThreadSafeWebsocket{
			conn, sync.Mutex{},
		},
		mut: sync.Mutex{},
	}
	Log(NameManagerConnection, Created, true, true)
	return smc, nil
}

func (smc *SessionManagerConnection) StartListening() {
	go func() {
		for {
			var msg SessionManagerMessage
			if err := smc.conn.ReadJSON(&msg); err != nil {
				// fmt.Errorf("error reading message: %w", err)
				continue // TODO Handle errors
			}
			LogWithMessage(NameManagerConnection, ReceivedWSMessage, true, true,
				fmt.Sprintf("origin=manager type=%s", msg.MessageType),
			)
			switch msg.MessageType {
			case "FullyConnected":
				smc.handleFullyConnected(msg.Message)
			case "ClientAddedToProvider":
				smc.handleClientAddedToProvider(msg.Message)
			case "RemoteClientAdded":
				smc.handleRemoteClientAdded(msg.Message)
			case "ClientTracksAdded":
				smc.handleClientTracksAdded(msg.Message)
			default:
				// Unknown message type, ignore or log
			}
		}
	}()
}

type ClientConnectedSettings struct {
	ClientID uint   `json:"clientID"`
	AuthKey  string `json:"authKey"`
}

type ClientTracksAdded struct {
	Providers     []ProviderMessage     `json:"providers"`
	VideoTracks   []ClientTrackInfo     `json:"videoTracks"`
	AudioTracks   []ClientTrackInfo     `json:"audioTracks"`
	RemoteClients []RemoteClientMessage `json:"remoteClients"`
}
type ProviderMessage struct {
	ProviderType     string          `json:"providerType"`
	ProviderKey      string          `json:"providerKey"`
	ProviderSettings json.RawMessage `json:"providerSettings"`
}
type RemoteClientMessage struct {
	ClientID    uint              `json:"clientID"`
	VideoTracks []ClientTrackInfo `json:"videoTracks"`
	AudioTracks []ClientTrackInfo `json:"audioTracks"`
}

func (smc *SessionManagerConnection) handleFullyConnected(payload json.RawMessage) {
	var msg ClientConnectedSettings
	if err := json.Unmarshal(payload, &msg); err != nil {
		fmt.Printf("failed to unmarshal payload: %v\n", err)
		return
	}
	fmt.Printf("Received FullyConnected: %+v\n", msg)

	smc.clientID = msg.ClientID
	smc.authKey = msg.AuthKey
	smc.localClient = NewConnectedClient(smc.clientID)

	LogWithMessage(NameManagerConnection, ClientConnectionComplete, true, true,
		fmt.Sprintf("clientID=%d authKey=%s", smc.clientID, smc.authKey),
	)
	// Send All tracks with requested providers
	smc.sendProviders()
}

func (smc *SessionManagerConnection) handleClientAddedToProvider(payload json.RawMessage) {
	var msg AddedToProviderMessage
	if err := json.Unmarshal(payload, &msg); err != nil {
		fmt.Printf("failed to unmarshal payload: %v\n", err)
		return
	}
	fmt.Printf("Received ClientAddedToProvider: %+v\n", msg)
	smc.mut.Lock()
	defer smc.mut.Unlock()
	if conn, ok := smc.providers[msg.ProviderKey]; ok {
		conn.OnFullyConnected(smc.clientID, smc.authKey, msg.Address, msg.Port)
	} else {
		// In case we get added to the provider first before the message was received
		LogWithMessage(NameManagerConnection, ClientAddedToBufferedProvider, true, true,
			fmt.Sprintf("providerKey=%s", msg.ProviderKey),
		)
		smc.bufferedProviders[msg.ProviderKey] = msg
	}
}

func (smc *SessionManagerConnection) handleRemoteClientAdded(payload json.RawMessage) {
	var msg RemoteClientMessage
	if err := json.Unmarshal(payload, &msg); err != nil {
		fmt.Printf("failed to unmarshal payload: %v\n", err)
		return
	}
	fmt.Printf("Received RemoteClientAdded: %+v\n", msg)
	remoteClient := NewConnectedClient(msg.ClientID)
	remoteClient.AddVideoTracks(msg.VideoTracks)
	remoteClient.AddAudioTracks(msg.AudioTracks)
	smc.remoteClient[msg.ClientID] = remoteClient
	LogWithMessage(NameManagerConnection, RemoteClientAdded, true, true,
		fmt.Sprintf("clientID=%d", msg.ClientID),
	)
}

func (smc *SessionManagerConnection) handleClientTracksAdded(payload json.RawMessage) {
	var msg ClientTracksAdded
	if err := json.Unmarshal(payload, &msg); err != nil {
		fmt.Printf("failed to unmarshal payload: %v\n", err)
		return
	}
	fmt.Printf("Received ClientTracksAdded: %+v\n", msg)
	smc.AddProviders(msg.Providers)
	smc.localClient.AddVideoTracks(msg.VideoTracks)
	smc.localClient.AddAudioTracks(msg.AudioTracks)
}

func (smc *SessionManagerConnection) sendProviders() {
	Log(NameManagerConnection, ClientSendingProviders, true, true)
	var providers JoinSessionMessage
	file, err := os.Open(smc.providersPath)
	if err != nil {
		log.Fatalf("Failed to open config file: %v", err)
	}
	defer file.Close()
	decoder := json.NewDecoder(file)
	if err := decoder.Decode(&providers); err != nil {
		log.Fatalf("Failed to decode config file: %v", err)
	}

	fmt.Printf("Loaded providers: %+v\n", providers)
	jsonE, _ := json.Marshal(providers)
	m := ClientMessage{
		MessageType: "JoinMessage",
		Message:     json.RawMessage(jsonE),
	}
	smc.conn.WriteJSONSafe(m)
	fmt.Printf("Sent providers: %+v\n", providers)
}

func (smc *SessionManagerConnection) AddProviders(providers []ProviderMessage) {
	smc.mut.Lock()
	defer smc.mut.Unlock()
	for _, provider := range providers {
		newProvider := NewSFUConnection(provider.ProviderKey)
		smc.providers[provider.ProviderKey] = newProvider
		bufferedProvider, exists := smc.bufferedProviders[provider.ProviderKey]
		if exists {
			newProvider.OnFullyConnected(smc.clientID, smc.authKey, bufferedProvider.Address, bufferedProvider.Port)
			delete(smc.bufferedProviders, provider.ProviderKey)
		}
	}
}
