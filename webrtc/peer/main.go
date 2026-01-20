package main

import (
	"flag"
	"fmt"
	"goweb/peer/src/proxy"
	"goweb/peer/src/session_manager"
	"goweb/peer/src/sfu"
	"goweb/peer/src/timer"
	"goweb/peer/src/transcoder"
	"goweb/shared/src/logger"
	"os"
	"strconv"
	"strings"
)

const (
	Idle     int = 0
	Hello    int = 1
	Offer    int = 2
	Answer   int = 3
	Ready    int = 4
	Finished int = 5
)

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

func sfuProviderFactory(transcoder transcoder.Transcoder, ipFilter string) session_manager.ProviderFactory {
	return func(providerKey string, videoTracks []session_manager.TrackSimple, audioTracks []session_manager.TrackSimple) session_manager.Provider {
		return sfu.NewSFUConnection(providerKey, videoTracks, audioTracks, transcoder, ipFilter)
	}
}

func main() {
	// Only has effect on Windows
	_ = timer.TimeBeginPeriod(1)
	defer timer.TimeEndPeriod(1)

	// General Command Line args
	subDir := flag.String("subDir", "", "Subdirectory for logs")
	preferredClientID := flag.Uint("c", 0, "Preferred client ID")
	ipFilter := flag.String("ipFilter", "", "IP Prefix to filter on (e.g., 192.168.1.)")
	// DLL Command Line args
	proxyPortThis := flag.String("r", ":0", "Port of this")
	proxyPortDLL := flag.String("p", ":0", "Port of the DLL")
	useProxy := flag.Bool("i", false, "Receive content from the DLL to forward over WebRTC")
	videoTracks := flag.String("vt", "0", "Pairs of video track string ID and internal integer ID")
	audioTracks := flag.String("at", "", "Pairs of audio track string ID and internal integer ID")
	sfuProviderKey := flag.String("sfuKey", "proxy", "Key of the SFU provider to use")
	sfuIP := flag.String("sfuIP", "", "IP address of the SFU instance, with port")
	sfuPort := flag.Uint("sfuPort", 0, "Port of the SFU instance")
	sfuAuthKey := flag.String("sfuAuth", "", "Authentication key for the SFU instance")
	// Debug Mode Command Line args
	managerIP := flag.String("manager", "", "IP address of the session manager instance, with port")
	providersPath := flag.String("providers", "", "Path to JSON file containing all the preferred providers with tracks")
	transcoderType := flag.String("tr", "fixed", "Type of transcoder to use (fixed, file, spectator, loopback)")
	transcoderConfigPath := flag.String("trcfg", "", "Path to JSON file containing the configuration for the transcoder (file or loopback)")
	enableConsoleOutput := flag.Bool("console", false, "Enable console output for logger")
	flag.Parse()
	logger.LogInit("SFUPeer", fmt.Sprintf("sfup_cl%d", *preferredClientID), logger.LogGreen, 10, *subDir, *enableConsoleOutput)
	var tr transcoder.Transcoder
	if *useProxy {
		videoTracks := parseTrackIDs(*videoTracks)
		audioTracks := parseTrackIDs(*audioTracks)

		proxyConn := proxy.NewProxyConnection()
		tr := transcoder.NewTranscoderRemote(videoTracks, proxyConn)
		proxyConn.SetupConnection(*proxyPortThis, *proxyPortDLL)
		proxyConn.StartListening((uint32(len(videoTracks) + len(audioTracks))))
		sfuConn := sfu.NewSFUConnection(
			*sfuProviderKey,
			trackMapToSlice(videoTracks),
			trackMapToSlice(audioTracks),
			tr,
			*ipFilter,
		)
		sfuConn.ProxyConn = proxyConn
		sfuConn.OnFullyConnected(*preferredClientID, *sfuAuthKey, *sfuIP, *sfuPort)
	} else {
		switch *transcoderType {
		case "file":
			tr = transcoder.NewTranscoderFile(*preferredClientID, *providersPath, *transcoderConfigPath)
		case "spectator":
			tr = transcoder.NewTranscoderSpectator()
		case "loopback":
			tr = transcoder.NewTranscoderLoopback(*preferredClientID, *providersPath)
		case "fixed":
			fallthrough
		default:
			tr = transcoder.NewTranscoderFixed(1000000, 30)
		}
		factory := sfuProviderFactory(tr, *ipFilter)
		smc, err := session_manager.NewSessionManagerConnection(*managerIP, *preferredClientID, *providersPath, tr, factory)
		if err != nil {
			logger.LogWithMessage(session_manager.NameManagerConnection, logger.Failed, true, true, fmt.Sprintf("error=%v", err))
			return
		}
		smc.StartListening()
	}

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

func parseTrackIDs(input string) map[string]uint32 {
	result := make(map[string]uint32)
	pairs := strings.Split(input, ";")
	for _, pair := range pairs {
		parts := strings.Split(pair, ":")
		if len(parts) == 2 {
			id := parts[0]
			internalID, err := strconv.ParseUint(parts[1], 10, 32)
			if err == nil {
				result[id] = uint32(internalID)
			}
		}
	}
	return result
}

func trackMapToSlice(trackMap map[string]uint32) []session_manager.TrackSimple {
	tracks := make([]session_manager.TrackSimple, 0, len(trackMap))
	for id, _ := range trackMap {
		tracks = append(tracks, session_manager.TrackSimple{
			TrackID:     id,
			IsConnected: false,
		})
	}
	return tracks
}
