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

const (
	// Object Status
	Creating   uint = 0
	Created    uint = 1
	Destroying uint = 2
	Destroyed  uint = 3
	Disposing  uint = 4
	Disposed   uint = 5
	Failed     uint = 6

	CriticalFail uint = 9999
)

// TODO increase buffer size and prevent automatic flushing
func LogInit() {
	logDir := filepath.Join(".", "logs")
	if err := os.MkdirAll(logDir, 0755); err != nil {
		fmt.Println("Failed to create log directory:", err)
		return
	}
	timestamp := time.Now().Format("20060102_150405")
	logPath := filepath.Join(logDir, "session_log_"+timestamp)
	f, err := os.OpenFile(logPath, os.O_CREATE|os.O_WRONLY|os.O_APPEND, 0644)
	if err != nil {
		fmt.Println("Failed to open log file:", err)
		return
	}
	logFile = f
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
		fmt.Print(msg)
	}
	if fileEnabled && writeToFile {
		(*logFile).WriteString(msg)
	}
}
