package session_manager

import (
	"encoding/json"
	"fmt"
	"log"
	"net/url"
	"os"
	"sync"

	"goweb/peer/src/logger"
	"goweb/peer/src/transcoder"
	"goweb/peer/src/utils"

	"github.com/gorilla/websocket"
)

type Provider interface {
	OnFullyConnected(clientID uint, authKey string, address string, port uint)
	SubscribeToRemoteClientTracks(clients []RemoteClientSimple)
	// Add other methods needed by session_manager
}
type ProviderFactory func(providerKey string, videoTracks []TrackSimple, audioTracks []TrackSimple) Provider

const NameManagerConnection = "SessionManagerConnection"

const (
	FullyConnected     uint32 = 0
	ClientConnected    uint32 = 1
	ClientDisconnected uint32 = 2
	AddTrack           uint32 = 3
	RemoveTrack        uint32 = 4
)

type SessionManagerConnection struct {
	managerIP       string
	clientID        uint
	authKey         string
	providersPath   string
	providers       map[string]Provider
	localClient     *ConnectedClient
	remoteClient    map[uint]*ConnectedClient
	transcoder      transcoder.Transcoder
	conn            *utils.ThreadSafeWebsocket
	providerFactory ProviderFactory
	mut             sync.Mutex
}

type SessionManagerMessage struct {
	MessageType string          `json:"messageType"`
	Message     json.RawMessage `json:"message"`
}

type TrackSimple struct {
	TrackID     string `json:"trackID"`
	IsConnected bool   `json:"isConnected"`
}

type RemoteClientSimple struct {
	ClientID    uint          `json:"clientID"`
	VideoTracks []TrackSimple `json:"videoTracks"`
	AudioTracks []TrackSimple `json:"audioTracks"`
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
	providerConnection Provider
}

// TODO Add function addSenderTrack
// TODO Add function addReceiverTrack

type ReceiverTrackInfo struct {
	ClientTrackInfo
	providerConnection Provider
}

func NewSessionManagerConnection(managerIP string, preferredClientID uint, providersPath string, transcoder transcoder.Transcoder, providerFactory ProviderFactory) (*SessionManagerConnection, error) {
	logger.LogWithMessage(NameManagerConnection, logger.Creating, true, true, fmt.Sprintf("managerIP=%s preferredclientID=%d", managerIP, preferredClientID))
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
		logger.Log(NameManagerConnection, logger.Failed, true, true)
		return nil, fmt.Errorf("failed to connect to manager: %w", err)
	}
	smc := &SessionManagerConnection{
		managerIP:       managerIP,
		providersPath:   providersPath,
		providers:       map[string]Provider{},
		remoteClient:    map[uint]*ConnectedClient{},
		transcoder:      transcoder,
		providerFactory: providerFactory,
		conn: &utils.ThreadSafeWebsocket{
			conn, sync.Mutex{},
		},
		mut: sync.Mutex{},
	}
	logger.Log(NameManagerConnection, logger.Created, true, true)
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
			logger.LogWithMessage(NameManagerConnection, logger.ReceivedWSMessage, true, true,
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
			case "ProviderRemoteClientTracksConnected":
				smc.handleRemoteClientTracksConnected(msg.Message)
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

	logger.LogWithMessage(NameManagerConnection, logger.ClientConnectionComplete, true, true,
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
	logger.LogWithMessage(NameManagerConnection, logger.ClientSessionJoined, true, true,
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
	// TODO maybe change this by using track info from client
	// i.e. use trackID from message to collect real tracks from smc.localClient
	conn := smc.providerFactory(msg.ProviderKey, msg.SenderVideoTracks, msg.SenderAudioTracks)
	smc.providers[msg.ProviderKey] = conn

	conn.OnFullyConnected(smc.clientID, smc.authKey, msg.Address, msg.Port)
	smc.localClient.SetTracksAsConnected(msg.SenderVideoTracks, msg.SenderAudioTracks)
	for _, rc := range msg.RemoteClients {
		rClient := smc.remoteClient[rc.ClientID]
		rClient.SetTracksAsConnected(rc.VideoTracks, rc.AudioTracks)
	}
	conn.SubscribeToRemoteClientTracks(msg.RemoteClients) // TODO Make sure this does not interfere with WebRTC negotiation
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
	logger.LogWithMessage(NameManagerConnection, logger.RemoteClientAdded, true, true,
		fmt.Sprintf("clientID=%d", msg.ClientID),
	)
}

type ProviderTracksConnectedMessage struct {
	ProviderKey string        `json:"providerKey"`
	ClientID    uint          `json:"clientID"`
	VideoTracks []TrackSimple `json:"videoTracks"`
	AudioTracks []TrackSimple `json:"audioTracks"`
}

func (smc *SessionManagerConnection) handleRemoteClientTracksConnected(payload json.RawMessage) {
	var msg ProviderTracksConnectedMessage
	if err := json.Unmarshal(payload, &msg); err != nil {
		fmt.Printf("failed to unmarshal payload: %v\n", err)
		return
	}
	fmt.Printf("Received TracksConnected: %+v\n", msg)
	smc.mut.Lock()
	defer smc.mut.Unlock()
	smc.remoteClient[msg.ClientID].SetTracksAsConnected(msg.VideoTracks, msg.AudioTracks)
	smc.providers[msg.ProviderKey].SubscribeToRemoteClientTracks([]RemoteClientSimple{{
		ClientID:    msg.ClientID,
		VideoTracks: msg.VideoTracks,
		AudioTracks: msg.AudioTracks,
	}})
}

func (smc *SessionManagerConnection) sendProviders() {
	logger.Log(NameManagerConnection, logger.ClientSendingProviders, true, true)
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
