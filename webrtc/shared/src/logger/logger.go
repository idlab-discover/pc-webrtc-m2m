package logger

import (
	"fmt"
	"os"
	"path/filepath"
	"time"
)

const convertToString = false
const enableLogging = true
const fileEnabled = true

var logFile *os.File
var logName string
var applyColor bool
var logColor string
var logEveryNFrames = uint(100)
var logEnableConsoleOutput = false

const (
	// Object Status
	Creating   uint = 0
	Created    uint = 1
	Destroying uint = 2
	Destroyed  uint = 3
	Disposing  uint = 4
	Disposed   uint = 5
	Failed     uint = 6

	ConfigLoading       uint = 100
	ConfigLoaded        uint = 101
	ConfigLoadingFailed uint = 102

	InvalidProvider        uint = 1000
	InvalidProviderAuthKey uint = 1001
	ProviderAlreadyExists  uint = 1002
	CreatingProvider       uint = 1003
	CreatedProvider        uint = 1004
	ClientAddedToProvider  uint = 1005

	ClientConnectionComplete      uint = 2000
	ClientSendingProviders        uint = 2001
	ClientAddedSenderVideoTrack   uint = 2002
	ClientAddedSenderAudioTrack   uint = 2003
	ClientAddedToBufferedProvider uint = 2004
	ClientSessionJoined           uint = 2005
	ClientAddingTransceivers      uint = 2006
	ClientAddedTransceivers       uint = 2007
	ClientAddingTrackFromOther    uint = 2008
	ClientSignalRenegotiation     uint = 2009
	ClientOnTrackCalled           uint = 2010

	RemoteClientAdded uint = 3000

	ProviderConfigReadDefaultStart uint = 4000
	ProviderConfigReadDefaultEnd   uint = 4001

	RemoteProviderConnectionStarted           uint = 5000
	RemoteProviderConnectionFailed            uint = 5001
	RemoteProviderConnectionConnecting        uint = 5002
	RemoteProviderConnectionSuccess           uint = 5003
	RemoteProviderConnectForwardingStarted    uint = 5004
	RemoteProviderConnectForwardingFailed     uint = 5005
	RemoteProviderConnectForwardingConnecting uint = 5006
	RemoteProviderConnectForwardingSuccess    uint = 5007
	RemoteProviderAddVirtualClient            uint = 5008

	FrameSending         uint = 6000
	FrameFullySent       uint = 6001
	FrameFirstPacketRecv uint = 6002
	FrameFullyRecv       uint = 6003

	ReceivedWSMessage uint = 7000

	SFUReceivedOffer          uint = 8000
	SFUClientConnectionChange uint = 8001

	TrackMetricsReport uint = 9000
	EstimatedBitrate   uint = 9001
	SystemResources    uint = 9002
	IPFilterCheck      uint = 9003
	MutLock uint = 9004
	MutUnlock uint = 9005
	InitStatus         uint = 9998
	CriticalFail       uint = 9999
)

const (
	LogRed    = "31m"
	LogGreen  = "32m"
	LogYellow = "33m"
	LogBlue   = "34m"
)

// TODO increase buffer size and prevent automatic flushing
func LogInit(name string, nameShort string, color string, everyNFrames uint, subDir string, enableConsoleOutput bool) {
	logDir := filepath.Join(".", "logs")
	if subDir != "" {
		logDir = filepath.Join(logDir, subDir)
	}
	if err := os.MkdirAll(logDir, 0755); err != nil {
		fmt.Println("Failed to create log directory:", err)
		return
	}
	timestamp := time.Now().Format("20060102_150405")
	logPath := filepath.Join(logDir, fmt.Sprintf("%s_log_%s.log", nameShort, timestamp))
	f, err := os.OpenFile(logPath, os.O_CREATE|os.O_WRONLY|os.O_APPEND, 0644)
	if err != nil {
		fmt.Println("Failed to open log file:", err)
		return
	}
	logName = name
	logFile = f
	logColor = color
	logEveryNFrames = everyNFrames
	logEnableConsoleOutput = enableConsoleOutput
	if logColor != "" {
		applyColor = true
	}
	LogWithMessage("Init", InitStatus, true, true, fmt.Sprintf("ts=%d", time.Now().UnixMilli()))
}

func Log(name string, status uint, writeToConsole, writeToFile bool) {
	if enableLogging {
		outputString := fmt.Sprintf("id=%s status=%d\n", name, status)
		_log(outputString, writeToConsole, writeToFile)
	}
}

func LogWithMessage(name string, status uint, writeToConsole, writeToFile bool, message string) {
	if enableLogging {
		outputString := fmt.Sprintf("id=%s status=%d %s\n", name, status, message)
		_log(outputString, writeToConsole, writeToFile)
	}
}

func LogFrameWithMessage(name string, status uint, writeToConsole, writeToFile bool, message string, frameNr uint) {
	if enableLogging && (frameNr%logEveryNFrames == 0) {
		outputString := fmt.Sprintf("id=%s status=%d ts=%d frame=%d %s\n", name, status, time.Now().UnixMilli(), frameNr, message)
		_log(outputString, writeToConsole, writeToFile)
	}
}

func _log(msg string, writeToConsole, writeToFile bool) {
	if writeToConsole && logEnableConsoleOutput {
		if applyColor {
			fmt.Printf("\033[%s[%s]: %s\033[0m", logColor, logName, msg)
		} else {
			fmt.Printf("[%s]: %s", logName, msg)
		}
	}
	if fileEnabled && writeToFile {
		(*logFile).WriteString(msg)
	}
}
