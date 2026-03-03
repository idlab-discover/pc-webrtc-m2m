package metrics

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
	config := flag.String("c", "", "Path to the config file that will be used")
	flag.Parse()
	if *config == "" {
		println("Cannot open config file at")
		return
	}
	// WebSocket handler
	s := NewMetricsServer(*config)
	go func() {
		s.StartListening()
	}()

	select {}
}

// Provision provider
// Send Config JSON
// Provider alerts manager that he is fully ready to receive clients
