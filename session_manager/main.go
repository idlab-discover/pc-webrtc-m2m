package main

import (
	"flag"
	"net/http"

	"github.com/gorilla/websocket"
)

var (
	upgrader = websocket.Upgrader{
		CheckOrigin: func(r *http.Request) bool { return true },
	}
)

func main() {
	LogInit()
	managerConfig := flag.String("c", "", "Path to the config file that will be used")
	flag.Parse()
	if *managerConfig == "" {
		Log("MAIN", CriticalFail, true, true)
		println("Cannot open config file at")
		return
	}
	// WebSocket handler
	sm := NewSessionManager(*managerConfig)
	go func() {
		sm.StartListening()
	}()

	println("waiting")
	select {}
}

// Provision provider
// Send Config JSON
// Provider alerts manager that he is fully ready to receive clients
