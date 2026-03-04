package main

import (
	"flag"
	"metrics/core"
	"metrics/core/logger"
)

func main() {
	logger.LogInit("METRICS", "met", logger.LogYellow)
	config := flag.String("c", "", "Path to the config file that will be used")
	port := flag.Uint("p", 6080, "Port to listen on")
	flag.Parse()
	if *config == "" {
		println("Cannot open config file at")
		return
	}
	// WebSocket handler
	s := core.NewMetricsServer(*config)
	go func() {
		s.StartListening(*port)
	}()

	select {}
}

// Provision provider
// Send Config JSON
// Provider alerts manager that he is fully ready to receive clients
