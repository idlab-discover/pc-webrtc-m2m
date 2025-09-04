package main

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

	ClientAddingTransceivers   uint = 2000
	ClientAddedTransceivers    uint = 2001
	ClientAddingTrackFromOther uint = 2002

	ProviderConfigReadDefaultStart uint = 4000
	ProviderConfigReadDefaultEnd   uint = 4001

	ReceivedWSMessage uint = 7000

	CriticalFail uint = 9999
)

const (
	LogRed    = "31m"
	LogGreen  = "32m"
	LogYellow = "33m"
	LogBlue   = "34m"
)

// TODO increase buffer size and prevent automatic flushing
func LogInit(name string, nameShort string, color string) {
	logDir := filepath.Join(".", "logs")
	if err := os.MkdirAll(logDir, 0755); err != nil {
		fmt.Println("Failed to create log directory:", err)
		return
	}
	timestamp := time.Now().Format("20060102_150405")
	logPath := filepath.Join(logDir, fmt.Sprintf("%s_log_%s", nameShort, timestamp))
	f, err := os.OpenFile(logPath, os.O_CREATE|os.O_WRONLY|os.O_APPEND, 0644)
	if err != nil {
		fmt.Println("Failed to open log file:", err)
		return
	}
	logName = name
	logFile = f
	logColor = color
	if logColor != "" {
		applyColor = true
	}
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

func _log(msg string, writeToConsole, writeToFile bool) {
	if writeToConsole {
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
