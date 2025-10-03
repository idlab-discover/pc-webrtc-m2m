package transcoder

import (
	"encoding/json"
	"fmt"
	"goweb/peer/src/logger"
	"os"
	"time"
)

type TranscoderFile struct {
	tracks map[string]*TranscoderFileTrack
}

type TranscoderFileConfig struct {
	TrackDirectory string `json:"trackDirectory"`
	MaxFrameNr     uint   `json:"maxFrameNr"`
}

type TranscoderFileTracksConfig struct {
	PreferredClientID uint                           `json:"preferredClientID"`
	Providers         []TranscoderFileProviderConfig `json:"providers"`
}

type TranscoderFileProviderConfig struct {
	VideoTracks []TranscoderFileTrackIDConfig `json:"videoTracks"`
}
type TranscoderFileTrackIDConfig struct {
	TrackID string `json:"trackID"`
}

func NewTranscoderFile(trackPath string, configPath string) *TranscoderFile {
	logger.Log("TranscoderFile", logger.Creating, true, true)
	data, err := os.ReadFile(trackPath)
	if err != nil {
		panic(err)
	}
	var trackCfg TranscoderFileTracksConfig
	if err := json.Unmarshal(data, &trackCfg); err != nil {
		panic(err)
	}
	dataCfg, err := os.ReadFile(configPath)
	if err != nil {
		panic(err)
	}
	var cfgFile TranscoderFileConfig
	if err := json.Unmarshal(dataCfg, &cfgFile); err != nil {
		panic(err)
	}
	t := &TranscoderFile{
		tracks: make(map[string]*TranscoderFileTrack),
	}
	for _, provider := range trackCfg.Providers {
		for _, track := range provider.VideoTracks {
			trackConfigPath := fmt.Sprintf("%s/%s/config.json", cfgFile.TrackDirectory, track.TrackID)
			t.tracks[fmt.Sprintf("cl%d_%s", trackCfg.PreferredClientID, track.TrackID)] = NewTranscoderFileTrack(trackConfigPath, cfgFile.MaxFrameNr)
		}
	}
	logger.Log("TranscoderFile", logger.Created, true, true)
	return t
}

func (t *TranscoderFile) EncodeFrame(trackID string) []byte {
	track, ok := t.tracks[trackID]
	if !ok {
		return nil
	}
	return track.NextFrame()
}

type TranscoderFileTrack struct {
	configPath       string
	contentDirectory string
	maxFrameNr       uint
	currentFrame     uint
	fps              uint
	lastFrameTime    time.Time
	sleepTime        time.Duration
}

type TranscoderFileTrackConfig struct {
	ContentDirectory string `json:"contentDirectory"`
	FPS              uint   `json:"fps"`
}

func NewTranscoderFileTrack(path string, maxFrameNr uint) *TranscoderFileTrack {
	data, err := os.ReadFile(path)
	if err != nil {
		panic(err)
	}
	var cfg TranscoderFileTrackConfig
	if err := json.Unmarshal(data, &cfg); err != nil {
		panic(err)
	}

	sleepTime := time.Duration(1000.0/float64(cfg.FPS)) * time.Millisecond

	f := &TranscoderFileTrack{
		configPath:       path,
		contentDirectory: cfg.ContentDirectory,
		fps:              cfg.FPS,
		sleepTime:        sleepTime,
		maxFrameNr:       maxFrameNr,
	}

	return f
}

func (t *TranscoderFileTrack) NextFrame() []byte {
	filename := fmt.Sprintf("%s/frame_%04d.bin", t.contentDirectory, t.currentFrame)
	data, err := os.ReadFile(filename)
	if err != nil {
		return nil
	}

	now := time.Now()
	nextFrameTime := t.lastFrameTime.Add(t.sleepTime)
	sleepDuration := nextFrameTime.Sub(now)
	if sleepDuration > 0 {
		time.Sleep(sleepDuration)
		t.lastFrameTime = nextFrameTime
	} else {
		// If we're behind, reset to now to avoid drift
		t.lastFrameTime = now
	}
	t.currentFrame = (t.currentFrame + 1) % t.maxFrameNr
	println("Serving frame:", filename, len(data))
	return data
}
