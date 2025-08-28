package main

import (
	"encoding/json"
	"sync"

	"github.com/gorilla/websocket"
)

type ClientMessage struct {
	MessageType uint32          `json:"messageType"`
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

func (t *ThreadSafeWebsocket) WriteMessageSafe(messageType int, data []byte) error {
	t.Lock()
	defer t.Unlock()
	return t.WriteMessage(messageType, data)
}
