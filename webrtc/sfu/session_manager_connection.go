package main

import (
	"encoding/json"
	"fmt"
	"net/url"
	"strings"

	"github.com/gorilla/websocket"
)

const (
	FullyConnected     uint32 = 0
	ClientConnected    uint32 = 1
	ClientDisconnected uint32 = 2
	AddTrack           uint32 = 3
	RemoveTrack        uint32 = 4
)

type SessionManagerConnection struct {
	managerIP  string
	providerID string
	authKey    string
	conn       *websocket.Conn
	sfu        *SFU
}

type SessionManagerMessage struct {
	MessageType string          `json:"messageType"`
	Message     json.RawMessage `json:"message"`
}

type NewClientMessage struct {
	ClientID            uint            `json:"clientID"`
	AuthKey             string          `json:"authKey"`
	SenderVideoTracks   []SenderTrack   `json:"senderVideoTracks"`
	ReceiverVideoTracks []ReceiverTrack `json:"receiverVideoTracks"`
}

func NewSessionManagerConnection(managerIP, providerID, authKey string, sfu *SFU) (*SessionManagerConnection, error) {
	u := url.URL{
		Scheme: "ws",
		Host:   managerIP,
		Path:   "/ws",
	}
	query := url.Values{}
	query.Set("providerID", providerID)
	if strings.TrimSpace(authKey) != "" {
		query.Set("authKey", authKey)
	}
	u.RawQuery = query.Encode()

	conn, _, err := websocket.DefaultDialer.Dial(u.String(), nil)
	if err != nil {
		return nil, fmt.Errorf("failed to connect to manager: %w", err)
	}
	smc := &SessionManagerConnection{
		managerIP:  managerIP,
		providerID: providerID,
		authKey:    authKey,
		conn:       conn,
		sfu:        sfu,
	}

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
	AddressAndPort     string          `json:"addressAndPort"`
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
	smc.sfu.SetupSFU(msg)
}

func (smc *SessionManagerConnection) handleClientConnected(payload json.RawMessage) {
	var msg NewClientMessage
	if err := json.Unmarshal(payload, &msg); err != nil {
		fmt.Printf("failed to unmarshal payload: %v\n", err)
		return
	}
	fmt.Printf("Received FullyConnected: %+v\n", msg)
	smc.sfu.SetupSFU(fcPayload)
}
func (smc *SessionManagerConnection) handleClientDisconnected(payload json.RawMessage) {

}
func (smc *SessionManagerConnection) handleAddTrack(payload json.RawMessage) {

}
func (smc *SessionManagerConnection) handleRemoveTrack(payload json.RawMessage) {

}

// manager ip
// provider key
// onFullyConnected => receive settings from manager
// onClientConnected => clientID + tracks etc...
// onClientDisconnected => remove client, maybe wait until peer disconnects?
// onAddTrack TODO probably later
// onRemoveTrack TODO probably later
