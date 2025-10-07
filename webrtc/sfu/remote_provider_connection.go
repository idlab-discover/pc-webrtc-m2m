package main

import (
	"fmt"
	"goweb/peer/src/logger"
	"goweb/peer/src/utils"
	"net/url"
	"sync"

	"github.com/gorilla/websocket"
)

const NameProviderConnectionBase = "ProviderConnectionBase"

type ProviderConnection interface {
	ConnectWebSocket(selfProviderType string, selfProviderKey string, selfAuthKey string) error
	SetupWebsocket(ws *ThreadSafeWebsocket)
	SetupForwarding() error
}

type ProviderConnectionBase struct {
	providerType string
	providerKey  string
	address      string
	port         uint
	authKey      string
	websocket    *ThreadSafeWebsocket
	mut          sync.Mutex
}

func NewProviderConnectionBase(providerType string, providerKey string, address string, port uint, authKey string) ProviderConnectionBase {
	return ProviderConnectionBase{
		providerType: providerType,
		providerKey:  providerKey,
		address:      address,
		port:         port,
		authKey:      authKey,
		mut:          sync.Mutex{},
	}
}

func (p *ProviderConnectionBase) ConnectWebSocket(selfProviderType string, selfProviderKey string, selfAuthKey string) error {
	LogWithMessage(NameProviderConnectionBase, RemoteProviderConnectionConnecting, true, true, fmt.Sprintf("providerType=%s providerKey=%s address=%s port=%d", p.providerType, p.providerKey, p.address, p.port))
	u := url.URL{Scheme: "ws", Host: fmt.Sprintf("%s:%d", p.address, p.port), Path: "websocket_provider"}
	query := url.Values{}
	query.Set("providerKey", selfProviderKey)
	query.Set("providerType", selfProviderType)
	query.Set("authKey", selfAuthKey)

	u.RawQuery = query.Encode()
	conn, _, err := websocket.DefaultDialer.Dial(u.String(), nil)

	if err != nil {
		fmt.Printf("WebRTCPeer: NewWSHandler: using %s ERROR: %s\n", u.String(), err)
		LogWithMessage(NameProviderConnectionBase, RemoteProviderConnectionFailed, true, true, fmt.Sprintf("providerType=%s providerKey=%s address=%s port=%d error=%s", p.providerType, p.providerKey, p.address, p.port, err.Error()))
		panic(err)
	}
	p.websocket = &ThreadSafeWebsocket{
		Conn:  conn,
		Mutex: sync.Mutex{},
	}
	p.startListening()
	LogWithMessage(NameProviderConnectionBase, RemoteProviderConnectionSuccess, true, true, fmt.Sprintf("providerType=%s providerKey=%s address=%s port=%d", p.providerType, p.providerKey, p.address, p.port))
	return nil
}

func (p *ProviderConnectionBase) startListening() {
	go func() {
		for {
			var msg utils.ClientMessage
			if err := p.websocket.ReadJSON(&msg); err != nil {
				fmt.Printf("SessionManager: webSocketHandler: ReadMessage: error %s\n", err.Error())
				p.onClose()
				break
			}
			logger.LogWithMessage(NameProviderConnectionBase, logger.ReceivedWSMessage, true, true,
				fmt.Sprintf("origin=provider providerType=%s providerKey=%s type=%s", p.providerKey, p.providerKey, msg.MessageType),
			)
			switch msg.MessageType {
			case "SubscribeToRemoteClient":
				// TODO Handle subscribed tracks
				// Transceivers have been added at remote provider
				// All we have to do is add tracks from user to the provider
				// Something like this: trackLocal := senderTrack.WebRTCTrack, add this to peer connection
				// And then renegotiate
			}
		}
	}()
}

func (p *ProviderConnectionBase) SetupWebsocket(ws *ThreadSafeWebsocket) {
	p.websocket = ws
	p.startListening()
}

func (p *ProviderConnectionBase) onClose() {
	// TODO
}
