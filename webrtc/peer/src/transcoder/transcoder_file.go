package transcoder

import (
	"encoding/json"
	"fmt"
	"goweb/shared/src/logger"
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

func NewTranscoderFile(preferredClientID uint, trackPath string, configPath string) *TranscoderFile {
	logger.LogWithMessage("TranscoderFile", logger.Creating, true, true, fmt.Sprintf("trackPath=%s configPath=%s", trackPath, configPath))
	data, err := os.ReadFile(trackPath)
	if err != nil {
		logger.LogWithMessage("TranscoderFile", logger.Failed, true, true, fmt.Sprintf("trackPath: %s", trackPath))
		panic(err)
	}
	var trackCfg TranscoderFileTracksConfig
	if err := json.Unmarshal(data, &trackCfg); err != nil {
		logger.LogWithMessage("TranscoderFile", logger.Failed, true, true, fmt.Sprintf("trackPath: %s reason=marshal", trackPath))
		panic(err)
	}
	dataCfg, err := os.ReadFile(configPath)
	if err != nil {
		logger.LogWithMessage("TranscoderFile", logger.Failed, true, true, fmt.Sprintf("configPath: %s", configPath))
		panic(err)
	}
	var cfgFile TranscoderFileConfig
	if err := json.Unmarshal(dataCfg, &cfgFile); err != nil {
		logger.LogWithMessage("TranscoderFile", logger.Failed, true, true, fmt.Sprintf("configPath: %s reason=marshal", configPath))
		panic(err)
	}
	t := &TranscoderFile{
		tracks: make(map[string]*TranscoderFileTrack),
	}
	for _, provider := range trackCfg.Providers {
		for _, track := range provider.VideoTracks {
			trackConfigPath := fmt.Sprintf("%s/%s/config.json", cfgFile.TrackDirectory, track.TrackID)
			internalTrackID := fmt.Sprintf("cl%d_%s", preferredClientID, track.TrackID)
			t.tracks[internalTrackID] = NewTranscoderFileTrack(internalTrackID, trackConfigPath, cfgFile.MaxFrameNr)
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

const NameTranscoderFileTrack = "TranscoderFileTrack"

func NewTranscoderFileTrack(trackID string, path string, maxFrameNr uint) *TranscoderFileTrack {
	logger.LogWithMessage(NameTranscoderFileTrack, logger.Creating, true, true, fmt.Sprintf("trackID=%s path=%s maxFrameNr=%d", trackID, path, maxFrameNr))
	data, err := os.ReadFile(path)
	if err != nil {
		logger.LogWithMessage(NameTranscoderFileTrack, logger.Failed, true, true, fmt.Sprintf("path=%s", path))
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
	logger.LogWithMessage(NameTranscoderFileTrack, logger.Created, true, true, fmt.Sprintf("path=%s maxFrameNr=%d fps=%d", path, maxFrameNr, cfg.FPS))
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
	//println("Serving frame:", filename, len(data))
	return data
}
