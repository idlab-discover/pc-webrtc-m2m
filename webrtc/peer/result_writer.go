package main

import (
	"fmt"
	"os"
	"sync"
	"time"
)

type ResultWriter struct {
	saveInterval int
	outputFile   *os.File
	clients      map[int]ClientResults
	clientID     int
	m            sync.Mutex
}

type ClientResults struct {
	descriptionRecords map[int]DescriptionRecord
}

type DescriptionRecord struct {
	resultRecords map[int]*ResultRecord
}
type ResultRecord struct {
	frameNr            int
	sizeInBytes        int
	entryTimestamp     int64
	completedTimestamp int64
}

func NewResultWriter(clientID int, saveInterval int, fileName string) ResultWriter {
	if saveInterval == 0 || fileName == "" {
		return ResultWriter{}
	}
	outputFile, err := os.OpenFile(fileName, os.O_WRONLY|os.O_CREATE|os.O_TRUNC, 0644)
	if err != nil {
		panic(err)
	}
	outputFile.WriteString(getHeader())
	return ResultWriter{
		clientID:     clientID,
		saveInterval: saveInterval,
		outputFile:   outputFile,
		m:            sync.Mutex{},
		clients:      make(map[int]ClientResults),
	}
}

func (rw *ResultWriter) AddDescription(clientID int, descriptionID int) bool {
	if rw.saveInterval == 0 {
		return false
	}
	rw.m.Lock()
	defer rw.m.Unlock()
	if _, exists := rw.clients[clientID]; !exists {
		rw.clients[clientID] = ClientResults{
			descriptionRecords: make(map[int]DescriptionRecord, 0),
		}
	}
	if _, exists := rw.clients[clientID].descriptionRecords[descriptionID]; !exists {
		rw.clients[clientID].descriptionRecords[descriptionID] = DescriptionRecord{
			resultRecords: make(map[int]*ResultRecord, 0),
		}
	}
	return true
}

func (rw *ResultWriter) CreateRecord(clientID int, descriptionID int, frameNr int) bool {
	if rw.saveInterval == 0 || frameNr%rw.saveInterval != 0 {
		return false
	}
	rw.m.Lock()
	defer rw.m.Unlock()
	rw.clients[clientID].descriptionRecords[descriptionID].resultRecords[frameNr] = &ResultRecord{
		frameNr:        frameNr,
		entryTimestamp: time.Now().UnixMilli(),
	}
	return true
}

func (rw *ResultWriter) SetSizeInBytes(clientID int, descriptionID int, frameNr int, sizeInBytes int) bool {
	if rw.saveInterval == 0 || frameNr%rw.saveInterval != 0 {
		return false
	}
	rw.m.Lock()
	defer rw.m.Unlock()
	if client, exists := rw.clients[clientID]; exists {
		if descRecord, exists := client.descriptionRecords[descriptionID]; exists {
			if record, exists := descRecord.resultRecords[frameNr]; exists {
				record.sizeInBytes = sizeInBytes
				return true
			}
		}
	}
	return false
}

func (rw *ResultWriter) SetFrameComplete(clientID int, descriptionID int, frameNr int) bool {
	if rw.saveInterval == 0 || frameNr%rw.saveInterval != 0 {
		return false
	}
	rw.m.Lock()
	defer rw.m.Unlock()
	if client, exists := rw.clients[clientID]; exists {
		if descRecord, exists := client.descriptionRecords[descriptionID]; exists {
			if record, exists := descRecord.resultRecords[frameNr]; exists {
				record.completedTimestamp = time.Now().UnixMilli()
				rw.saveRecord(clientID, descriptionID, record)
				return true
			}
		}
	}
	return false
}

func (rw *ResultWriter) saveRecord(clientID int, descriptionID int, record *ResultRecord) {
	rw.outputFile.WriteString(fmt.Sprintf("%d;%d;%d;%d;%d;%d;%t\n",
		clientID,
		descriptionID,
		record.frameNr,
		record.sizeInBytes,
		record.entryTimestamp,
		record.completedTimestamp,
		clientID == rw.clientID,
	))
}

func getHeader() string {
	return "clientID;descriptionID;frameNr;sizeInBytes;entryTimestamp;completedTimestamp;isSender\n"
}
