package main

import (
	"encoding/json"
	"fmt"
	"goweb/shared/src/logger"
	"net/url"
	"strings"
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
	managerIP   string
	providerKey string
	authKey     string
	websocket   *ThreadSafeWebsocket
	sfu         *SFU
}

type SessionManagerMessage struct {
	MessageType string          `json:"messageType"`
	Message     json.RawMessage `json:"message"`
}

type TrackSimple struct {
	TrackID string `json:"trackID"`
}

type NewClientMessage struct {
	ClientID          uint          `json:"clientID"`
	AuthKey           string        `json:"authKey"`
	SenderVideoTracks []SenderTrack `json:"senderVideoTracks"`

	SenderAudioTracks []SenderTrack  `json:"senderAudioTracks"`
	RemoteClients     []RemoteClient `json:"remoteClients"`
}

type RemoteProviderAddedMessage struct {
	ProviderType string `json:"providerType"`
	ProviderKey  string `json:"providerKey"`
	Address      string `json:"address"`
	Port         uint   `json:"port"`
	AuthKey      string `json:"authKey"`
}

type ProviderRemoteProviderClientMessage struct {
	ProviderKey string        `json:"providerKey"`
	ClientID    uint          `json:"clientID"`
	VideoTracks []TrackSimple `json:"videoTracks"`
	AudioTracks []TrackSimple `json:"audioTracks"`
}

type RemoteClient struct {
	ClientID            uint          `json:"clientID"`
	ReceiverAudioTracks []TrackSimple `json:"receiverAudioTracks"`
	ReceiverVideoTracks []TrackSimple `json:"receiverVideoTracks"`
}

func NewSessionManagerConnection(managerIP string, providerKey string, authKey string, sfu *SFU) (*SessionManagerConnection, error) {
	logger.LogWithMessage(NameManagerConnection, logger.Creating, true, true, fmt.Sprintf("managerIP=%s providerKey=%s authKey=%s", managerIP, providerKey, authKey))
	u := url.URL{
		Scheme: "ws",
		Host:   managerIP,
		Path:   "/websocket_provider",
	}
	query := url.Values{}
	query.Set("providerKey", providerKey)
	if strings.TrimSpace(authKey) != "" {
		query.Set("authKey", authKey)
	}
	u.RawQuery = query.Encode()

	conn, _, err := websocket.DefaultDialer.Dial(u.String(), nil)
	if err != nil {
		logger.LogWithMessage(NameManagerConnection, logger.Failed, true, true, fmt.Sprintf("managerIP=%s providerKey=%s authKey=%s error=%v", managerIP, providerKey, authKey, err))
		return nil, fmt.Errorf("failed to connect to manager: %w", err)
	}
	smc := &SessionManagerConnection{
		managerIP:   managerIP,
		providerKey: providerKey,
		authKey:     authKey,
		websocket: &ThreadSafeWebsocket{
			conn, sync.Mutex{},
		},
		sfu: sfu,
	}
	logger.Log(NameManagerConnection, logger.Created, true, true)
	return smc, nil
}

func (smc *SessionManagerConnection) StartListening() {
	go func() {
		for {
			var msg SessionManagerMessage

			if err := smc.websocket.ReadJSON(&msg); err != nil {
				panic(err)
				//fmt.Printf("error reading message: %v\n", err)
				//continue // TODO Handle errors
			}

			logger.LogWithMessage(NameManagerConnection, logger.ReceivedWSMessage, true, true,
				fmt.Sprintf("origin=manager type=%s", msg.MessageType),
			)
			switch msg.MessageType {
			case "FullyConnected":
				smc.handleFullyConnected(msg.Message)
			case "ClientConnected":
				smc.handleClientConnected(msg.Message)
			case "ClientDisconnected":
				smc.handleClientDisconnected(msg.Message)
			case "AddTrack":
				smc.handleAddTrack(msg.Message)
			case "RemoveTrack":
				smc.handleRemoveTrack(msg.Message)
			case "RemoteProviderConnected":
				smc.handleRemoteProviderConnected(msg.Message)
			case "RemoteProviderDisconnected":
				smc.handleRemoteProviderDisconnected(msg.Message)
			case "AddVirtualClient":
				smc.handleVirtualClientAdded(msg.Message)
			case "RemoveVirtualClient":
				smc.handleVirtualClientRemoved(msg.Message)
			default:
				// Unknown message type, ignore or log
			}
		}

	}()
}

type SFUSettings struct {
	UseABR             bool            `json:"useABR"`
	AdaptationMethod   string          `json:"adaptationMethod"`
	AdaptationSettings json.RawMessage `json:"adaptationSettings"`
	UseCC              bool            `json:"useCCs"`
	CCMethod           string          `json:"ccMethod"`
	CCSettings         json.RawMessage `json:"ccSettings"`
	ContentType        string          `json:"contentType"`
	VerifyAuthKey      bool            `json:"verifyAuthKey"`
	NackSettings       NackSettings    `json:"nackSettings"`
	GatherTrackStats   bool            `json:"gatherTrackStats"`
}

type NackSettings struct{}

func (smc *SessionManagerConnection) handleFullyConnected(payload json.RawMessage) {
	var msg SFUSettings
	if err := json.Unmarshal(payload, &msg); err != nil {
		fmt.Printf("failed to unmarshal payload: %v\n", err)
		return
	}
	fmt.Printf("Received FullyConnected: %+v\n", msg)
	go func() {
		smc.sfu.SetupSFU(msg)
	}()
}

func (smc *SessionManagerConnection) handleClientConnected(payload json.RawMessage) {
	var msg NewClientMessage
	if err := json.Unmarshal(payload, &msg); err != nil {
		fmt.Printf("failed to unmarshal payload: %v\n", err)
		return
	}
	fmt.Printf("Received ClientConnected: %+v\n", msg)
	smc.sfu.AddClient(msg)
	msgNew := ClientMessage{
		MessageType: "ClientAdded",
		Message:     payload,
	}
	smc.websocket.WriteJSONSafe(msgNew)
}
func (smc *SessionManagerConnection) handleClientDisconnected(payload json.RawMessage) {

}
func (smc *SessionManagerConnection) handleAddTrack(payload json.RawMessage) {

}
func (smc *SessionManagerConnection) handleRemoveTrack(payload json.RawMessage) {

}
func (smc *SessionManagerConnection) handleRemoteProviderConnected(payload json.RawMessage) {
	var msg RemoteProviderAddedMessage
	if err := json.Unmarshal(payload, &msg); err != nil {
		fmt.Printf("failed to unmarshal payload: %v\n", err)
		return
	}
	fmt.Printf("Received RemoteProviderConnected: %+v\n", msg)
	smc.sfu.AddRemoteProvider(msg, smc.providerKey, smc.authKey)

}
func (smc *SessionManagerConnection) handleRemoteProviderDisconnected(payload json.RawMessage) {

}

func (smc *SessionManagerConnection) handleVirtualClientAdded(payload json.RawMessage) {
	var msg ProviderRemoteProviderClientMessage
	if err := json.Unmarshal(payload, &msg); err != nil {
		fmt.Printf("failed to unmarshal payload: %v\n", err)
		return
	}
	fmt.Printf("Received VirtualClientAdded: %+v\n", msg)
	succes := smc.sfu.AddVirtualClient(msg)
	if !succes {
		fmt.Printf("Failed to add virtual client: %+v\n", msg)
		return
	}
	// Alert session manager virtual client was added
	smc.websocket.WriteJSONMessageSafe("VirtualClientAdded", msg)
}

func (smc *SessionManagerConnection) handleVirtualClientRemoved(payload json.RawMessage) {

}

// manager ip
// provider key
// onFullyConnected => receive settings from manager
// onClientConnected => clientID + tracks etc...
// onClientDisconnected => remove client, maybe wait until peer disconnects?
// onAddTrack TODO probably later
// onRemoveTrack TODO probably later
