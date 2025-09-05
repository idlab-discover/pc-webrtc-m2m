package main

import (
	"flag"
	"fmt"
	"os"
)

const (
	Idle     int = 0
	Hello    int = 1
	Offer    int = 2
	Answer   int = 3
	Ready    int = 4
	Finished int = 5
)

var proxyConn *ProxyConnection
var clientID *int

var saveResults bool
var isDebug bool

var resultWriter ResultWriter

type DebugConfig struct {
	Fps          int                      `json:"fps"`
	Descriptions []DebugDescriptionConfig `json:"descriptions"`
}

type DebugDescriptionConfig struct {
	Delay   int `json:"delay"`
	Bitrate int `json:"bitrate"`
}

type DebugController struct {
	config         DebugConfig
	startTimestamp uint64
}

func main() {
	LogInit("SFUPeer", "sfup", LogGreen)
	managerIP := flag.String("manager", "", "IP address of the session manager instance, with port")
	providersPath := flag.String("providers", "", "Path to JSON file containing all the preferred providers with tracks")
	preferredClientID := flag.Uint("c", 0, "Preferred client ID")
	flag.Parse()
	smc, err := NewSessionManagerConnection(*managerIP, *preferredClientID, *providersPath, NewTranscoderFixed(1000000, 30))
	if err != nil {
		LogWithMessage(NameManagerConnection, Failed, true, true, fmt.Sprintf("error=%v", err))
		return
	}
	smc.StartListening()
	select {}
	return

	proxyPort := flag.String("p", ":0", "Port through which the DLL is connected")
	useProxyInput := flag.Bool("i", false, "Receive content from the DLL to forward over WebRTC")
	//useProxyOutput := flag.Bool("o", false, "Forward content received over WebRTC to the DLL")
	clientID = flag.Int("c", 0, "Client ID")

	//debugConfigFile := flag.String("dbg", "", "Path to debug config file")
	//disableGCC := flag.Bool("d", false, "Disables GCC based bandwidth estimation")
	flag.Parse()

	if *useProxyInput && *proxyPort == ":0" {
		println("WebRTCPeer: ERROR: port cannot equal :0")
		os.Exit(1)
	}

	fmt.Printf("WebRTCPeer: Starting client %d\n", *clientID)

	//var transcoder Transcoder
	if *useProxyInput {
		//	proxyConn = NewProxyConnection()
		//	proxyConn.SetupConnection(*proxyPort)
		//	proxyConn.StartListening(uint32(*numberOfCapturers), uint32(*numberOfTiles))
		//	transcoder = NewTranscoderRemote(proxyConn)
	} else {

		/*if *debugConfigFile == "" {
			fmt.Println("Debug file is empty")
			return
		}
		jsonFile, err := os.Open(*debugConfigFile)
		if err != nil {
			fmt.Println("Error opening file:", err)
			return
		}
		defer jsonFile.Close()
		byteValue, err := ioutil.ReadAll(jsonFile)
		if err != nil {
			fmt.Println("Error reading file:", err)
			return
		}

		var dbgConfig DebugConfig

		err = json.Unmarshal(byteValue, &dbgConfig)
		if err != nil {
			fmt.Println("Error unmarshaling JSON:", err)
			return
		}
		transcoder = NewTranscoderDebug(dbgConfig)
		println("tesssds")
		fmt.Printf("dbgConfig: %+v\n", dbgConfig)*/
	}
	select {}
}
