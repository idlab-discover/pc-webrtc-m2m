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
	managerIP     string
	clientID      uint
	authKey       string
	providersPath string
	providers     map[string]*SFUConnection
	localClient   *ConnectedClient
	remoteClient  map[uint]*ConnectedClient
	transcoder    Transcoder
	conn          *ThreadSafeWebsocket
	mut           sync.Mutex
}

type SessionManagerMessage struct {
	MessageType string          `json:"messageType"`
	Message     json.RawMessage `json:"message"`
}

type ClientAddedToProviderMessage struct {
	ProviderKey       string                 `json:"providerKey"`
	Address           string                 `json:"address"`
	Port              uint                   `json:"port"`
	Config            map[string]interface{} `json:"config"`
	SenderVideoTracks []ClientTrackInfo      `json:"senderVideoTracks"`
	SenderAudioTracks []ClientTrackInfo      `json:"senderAudioTracks"`
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
}

type ClientTrackInfo struct {
	ClientID      uint            `json:"clientID"`
	ProviderKey   string          `json:"providerKey"`
	TrackID       string          `json:"trackID"`
	CapturerType  string          `json:"capturerType"`
	TrackType     string          `json:"trackType"` // Make it so there is a defaultForTrackType thingy in sessionmanager
	TrackSettings json.RawMessage `json:"trackSettings"`
	IsConnected   bool
}

type SenderTrackInfo struct {
	ClientTrackInfo
	providerConnection *SFUConnection
}

// TODO Add function addSenderTrack
// TODO Add function addReceiverTrack

type ReceiverTrackInfo struct {
	ClientTrackInfo
	providerConnection *SFUConnection
}

func NewSessionManagerConnection(managerIP string, preferredClientID uint, providersPath string, transcoder Transcoder) (*SessionManagerConnection, error) {
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
		managerIP:     managerIP,
		providersPath: providersPath,
		providers:     map[string]*SFUConnection{},
		remoteClient:  map[uint]*ConnectedClient{},
		transcoder:    transcoder,
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
			case "SessionJoined":
				smc.handleSessionJoined(msg.Message)
			case "ClientAddedToProvider":
				smc.handleClientAddedToProvider(msg.Message)
			case "RemoteClientAdded":
				smc.handleRemoteClientAdded(msg.Message)
			default:
				// Unknown message type, ignore or log
			}
		}
	}()
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

func (smc *SessionManagerConnection) handleSessionJoined(payload json.RawMessage) {
	var msg SessionJoinedMessage
	if err := json.Unmarshal(payload, &msg); err != nil {
		fmt.Printf("failed to unmarshal payload: %v\n", err)
		return
	}
	fmt.Printf("Received SessionJoined: %+v\n", msg)
	LogWithMessage(NameManagerConnection, ClientSessionJoined, true, true,
		fmt.Sprintf("codecMode=%s defaultProvider=%s nProviders=%d", msg.CodecMode, msg.DefaultProvider, len(msg.Providers)),
	)
	smc.localClient.SetTracksConnectionStatus(msg.CodecMode, msg.Providers, false)
}

func (smc *SessionManagerConnection) handleClientAddedToProvider(payload json.RawMessage) {
	var msg ClientAddedToProviderMessage
	if err := json.Unmarshal(payload, &msg); err != nil {
		fmt.Printf("failed to unmarshal payload: %v\n", err)
		return
	}
	fmt.Printf("Received ClientAddedToProvider: %+v\n", msg)
	smc.mut.Lock()
	defer smc.mut.Unlock()
	conn := NewSFUConnection(msg.ProviderKey, msg.SenderVideoTracks, msg.SenderAudioTracks, smc.transcoder)
	smc.providers[msg.ProviderKey] = conn

	conn.OnFullyConnected(smc.clientID, smc.authKey, msg.Address, msg.Port)
	smc.localClient.SetTracksAsConnected(msg.SenderVideoTracks, msg.SenderAudioTracks)
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

	smc.conn.WriteJSONMessageSafe("JoinMessage", providers)
	fmt.Printf("Sent providers: %+v\n", providers)
}
