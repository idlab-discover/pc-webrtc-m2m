package main

import (
	"encoding/json"
	"fmt"
	"sync"

	"github.com/gorilla/websocket"
)

type ClientMessage struct {
	MessageType string          `json:"messageType"`
	Message     json.RawMessage `json:"message"`
}

type ThreadSafeWebsocket struct {
	*websocket.Conn
	sync.Mutex
}

func (t *ThreadSafeWebsocket) WriteJSONSafe(v interface{}) error {
	t.Lock()
	defer t.Unlock()
	return t.WriteJSON(v)
}

func (t *ThreadSafeWebsocket) WriteJSONMessageSafe(messageType string, v interface{}) error {
	msgBytes, err := json.Marshal(v)
	if err != nil {
		fmt.Printf("failed to marshal ClientFullyConnectedMessage: %v\n", err)
		return err
	}
	m := ClientMessage{
		MessageType: messageType,
		Message:     json.RawMessage(msgBytes),
	}

	return t.WriteJSONSafe(m)
}

func (t *ThreadSafeWebsocket) WriteMessageSafe(messageType int, data []byte) error {
	t.Lock()
	defer t.Unlock()
	return t.WriteMessage(messageType, data)
}
