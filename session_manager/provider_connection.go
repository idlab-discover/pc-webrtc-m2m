package main

import (
	"fmt"
	"sync"
)

const NameProvider = "ProviderConnection"

type ProviderConnection struct {
	ProviderKey string
	Address     string
	Port        uint
	AuthKey     string
	websocket   *ThreadSafeWebsocket
	config      map[string]interface{}
	IsReady     bool
	mut         sync.Mutex
}

func NewProviderConnection(providerKey string, address string, port uint, authKey string, config map[string]interface{}) *ProviderConnection {
	Log(NameProvider, Creating, true, true)
	pro := &ProviderConnection{
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
}

func (clc *ProviderConnection) startListening() {
	go func() {
		for {
			var msg ClientMessage
			if err := clc.websocket.ReadJSON(&msg); err != nil {
				fmt.Printf("WebRTCSFU: webSocketHandler: ReadMessage: error %w\n", err)
				break
			}

			switch msg.MessageType {
			case 3:

			}
		}
	}()
}
