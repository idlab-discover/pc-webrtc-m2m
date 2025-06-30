package main

import (
	"encoding/csv"
	"fmt"
	"io"
	"os"
	"strconv"
	"strings"
	"sync"
	"time"
)

type Transcoder interface {
	EncodeFrame(uint32) []byte
}

type TranscoderRemote struct {
	proxy_con    *ProxyConnection
	frameCounter uint32
	isReady      bool
}

type DebugDescription struct {
	DescriptionID  int
	CurrentFrameNr int
	Frames         []DebugFrameSize
}

type DebugFrameSize struct {
	FrameNr      int
	Size         int
	EncodingTime int
}

func NewTranscoderRemote(proxy_con *ProxyConnection) *TranscoderRemote {
	return &TranscoderRemote{proxy_con, 0, true}
}

func (t *TranscoderRemote) EncodeFrame(tile uint32) []byte {
	return proxyConn.NextTile(tile)
}

type TranscoderDummy struct {
	proxy_con    *ProxyConnection
	frameCounter uint32
	isReady      bool
}

func NewTranscoderDummy(proxy_con *ProxyConnection) *TranscoderDummy {
	return &TranscoderDummy{proxy_con, 0, true}
}

func (t *TranscoderDummy) EncodeFrame(tile uint32) []byte {
	return nil
}

type TranscoderDebug struct {
	fps             int
	loopFrames      bool
	waitForEncode   bool
	dbgDescriptions []DebugDescription
	isReady         bool
	m_video         sync.Mutex
	cond_video      map[uint32]*sync.Cond
	complete_tiles  map[uint32]bool
}

func MakeDebugDescriptions(dbgConfig DebugConfig) []DebugDescription {
	descriptions := make([]DebugDescription, len(dbgConfig.Descriptions))
	for i, desc := range dbgConfig.Descriptions {
		file, err := os.Open(desc.SizesPath)
		if err != nil {
			panic(err)
		}
		defer file.Close()

		// Create a new CSV reader and set the delimiter to semicolon
		reader := csv.NewReader(file)
		reader.Comma = ';'
		reader.TrimLeadingSpace = true

		// Read the header
		_, err = reader.Read()
		if err != nil {
			panic("failed to read header: " + err.Error())
		}

		var frames []DebugFrameSize

		// Read each record
		for {
			record, err := reader.Read()
			if err == io.EOF {
				break
			}
			if err != nil {
				fmt.Println("error reading record:", err)
				continue
			}

			// Parse the relevant fields
			frameNr, err := strconv.Atoi(strings.TrimSpace(record[0]))
			if err != nil {
				fmt.Println("invalid frame number:", record[0])
				continue
			}
			encodedSize, err := strconv.Atoi(strings.TrimSpace(record[3]))
			if err != nil {
				fmt.Println("invalid encoded size:", record[3])
				continue
			}
			encodingTime, err := strconv.Atoi(strings.TrimSpace(record[4]))
			if err != nil {
				fmt.Println("invalid encoding time:", record[4])
				continue
			}

			frame := DebugFrameSize{
				FrameNr:      frameNr,
				Size:         encodedSize,
				EncodingTime: encodingTime,
			}

			frames = append(frames, frame)
		}
		descriptions[i] = DebugDescription{
			DescriptionID:  i,
			CurrentFrameNr: 0,
			Frames:         frames,
		}
	}
	return descriptions
}

func NewTranscoderDebug(dbgConfig DebugConfig) *TranscoderDebug {
	dbgDescriptions := MakeDebugDescriptions(dbgConfig)
	t := &TranscoderDebug{
		fps:             dbgConfig.Fps,
		loopFrames:      dbgConfig.LoopFrames,
		waitForEncode:   dbgConfig.WaitForEncode,
		dbgDescriptions: dbgDescriptions,
		isReady:         true,
		m_video:         sync.Mutex{},
		cond_video:      make(map[uint32]*sync.Cond, 0),
		complete_tiles:  make(map[uint32]bool, 0),
	}
	for i := uint32(0); i < uint32(len(t.dbgDescriptions)); i++ {
		t.cond_video[i] = sync.NewCond(&t.m_video)
	}
	t.produceFrames()
	return t
}

func (t *TranscoderDebug) EncodeFrame(tile uint32) []byte {
	t.m_video.Lock()
	if !t.complete_tiles[tile] {
		t.cond_video[tile].Wait()
	}
	t.complete_tiles[tile] = false
	t.m_video.Unlock()
	dbgDescription := &(t.dbgDescriptions[tile])
	currentFrame := &(*dbgDescription).Frames[(*dbgDescription).CurrentFrameNr]
	if t.waitForEncode {
		time.Sleep(time.Duration((*currentFrame).EncodingTime) * time.Millisecond)
	}
	if (*dbgDescription).CurrentFrameNr >= len((*dbgDescription).Frames)-1 {
		if t.loopFrames {
			(*dbgDescription).CurrentFrameNr = 0
		} else {
			fmt.Printf("TranscoderDebug: No more frames for tile %d last frame %d\n", tile, (*dbgDescription).CurrentFrameNr)
			return nil // No more frames to encode
		}
	}
	(*dbgDescription).CurrentFrameNr++
	return make([]byte, (*currentFrame).Size) // Return a byte slice of the encoded size
}

func (t *TranscoderDebug) produceFrames() {
	go func() {
		interval := time.Duration(1000.0/float64(t.fps)) * time.Millisecond
		ticker := time.NewTicker(interval)
		defer ticker.Stop()
		for range ticker.C {
			// Add new frame
			t.m_video.Lock()
			for i := uint32(0); i < uint32(len(t.dbgDescriptions)); i++ {
				t.complete_tiles[i] = true
				t.cond_video[i].Broadcast()
			}
			t.m_video.Unlock()
		}
	}()
}
