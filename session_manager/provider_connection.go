package main

import (
	"encoding/json"
	"fmt"
	"sync"
)

const NameProvider = "ProviderConnection"

type ProviderConnection struct {
	parent      *SessionManager
	ProviderKey string
	Address     string
	Port        uint
	AuthKey     string
	websocket   *ThreadSafeWebsocket
	config      map[string]interface{}
	IsReady     bool
	mut         sync.Mutex
}

func NewProviderConnection(parent *SessionManager, providerKey string, address string, port uint, authKey string, config map[string]interface{}) *ProviderConnection {
	Log(NameProvider, Creating, true, true)
	pro := &ProviderConnection{
		parent:      parent,
		ProviderKey: providerKey,
		Address:     address,
		Port:        port,
		AuthKey:     authKey,
		config:      config,
		mut:         sync.Mutex{},
	}
	Log(NameProvider, Created, true, true)
	return pro
}

func (pc *ProviderConnection) SetupProvider(ws *ThreadSafeWebsocket) {
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
	pc.websocket.WriteJSONSafe(m)
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

			}
		}
	}()
}

func (clc *ProviderConnection) onClose() {
	clc.parent.onProviderClose(clc)
}
