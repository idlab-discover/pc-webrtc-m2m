package transcoder

import (
	"encoding/json"
	"fmt"
	"goweb/shared/src/logger"
	"os"
	"strings"
	"sync"
)

const NameTranscoderLoopback = "TranscoderLoopback"

type TranscoderLoopback struct {
	preferredClientID uint
	tracks            map[string]*LoopbackCapturer
}

type LoopbackFrame struct {
	frameNr    uint32
	currentLen uint32
	fileLen    uint32
	fileData   []byte
}

type LoopbackCapturer struct {
	mtx  sync.Mutex
	cond *sync.Cond

	incomplete_frames map[uint32]*LoopbackFrame
	complete_frame    *LoopbackFrame
	ready_status      bool
}

type TranscoderLoopbackTracksConfig struct {
	Providers []TranscoderLoopbackProviderConfig `json:"providers"`
}

type TranscoderLoopbackProviderConfig struct {
	VideoTracks []TranscoderFileTrackIDConfig `json:"videoTracks"`
}

func NewTranscoderLoopback(preferredClientID uint, trackPath string) *TranscoderLoopback {
	logger.LogWithMessage(NameTranscoderLoopback, logger.Creating, true, true, fmt.Sprintf("configPath=%s", trackPath))
	data, err := os.ReadFile(trackPath)
	if err != nil {
		logger.LogWithMessage(NameTranscoderLoopback, logger.Failed, true, true, fmt.Sprintf("configPath: %s", trackPath))
		panic(err)
	}
	var trackCfg TranscoderLoopbackTracksConfig
	if err := json.Unmarshal(data, &trackCfg); err != nil {
		logger.LogWithMessage(NameTranscoderLoopback, logger.Failed, true, true, fmt.Sprintf("configPath: %s reason=marshal", trackPath))
		panic(err)
	}
	t := &TranscoderLoopback{
		preferredClientID: preferredClientID,
		tracks:            make(map[string]*LoopbackCapturer),
	}
	for _, provider := range trackCfg.Providers {
		for _, track := range provider.VideoTracks {
			internalTrackID := fmt.Sprintf("cl%d_%s", preferredClientID, track.TrackID)
			loopbackCapturer := &LoopbackCapturer{
				incomplete_frames: make(map[uint32]*LoopbackFrame),
				complete_frame:    nil,
				ready_status:      false,
			}
			loopbackCapturer.cond = sync.NewCond(&loopbackCapturer.mtx)
			t.tracks[internalTrackID] = loopbackCapturer
		}
	}
	logger.Log(NameTranscoderLoopback, logger.Created, true, true)
	return t
}
func (t *TranscoderLoopback) EncodeFrame(trackID string) (uint32, []byte) {
	track, ok := t.tracks[trackID]
	if !ok {
		return 0, nil
	}
	track.mtx.Lock()
	defer track.mtx.Unlock()
	for !track.ready_status {
		track.cond.Wait()
	}
	loopbackFrame := t.tracks[trackID].complete_frame
	t.tracks[trackID].complete_frame = nil
	t.tracks[trackID].ready_status = false
	return loopbackFrame.frameNr, loopbackFrame.fileData
}

func (t *TranscoderLoopback) GetLoopbackCapturerForTrack(trackID string) *LoopbackCapturer {
	idx := strings.Index(trackID, "_")
	if idx == -1 {
		logger.LogWithMessage(NameTranscoderLoopback, logger.SFUInvalidTrack, true, true, fmt.Sprintf("type=loopback trackID=%s", trackID))
		return nil
	}
	mappedTrackID := fmt.Sprintf("cl%d_%s", t.preferredClientID, trackID[idx+1:])
	track, ok := t.tracks[mappedTrackID]
	if !ok {
		return nil
	}
	return track
}

func (t *LoopbackCapturer) InsertFrameData(frameNr uint32, data []byte, fileLen uint32) {
	t.mtx.Lock()
	defer t.mtx.Unlock()
	loopbackFrame, ok := t.incomplete_frames[frameNr]
	if !ok {
		loopbackFrame = &LoopbackFrame{
			frameNr:    frameNr,
			currentLen: 0,
			fileLen:    fileLen,
			fileData:   make([]byte, fileLen),
		}
		t.incomplete_frames[frameNr] = loopbackFrame
	}
	copy(loopbackFrame.fileData[loopbackFrame.currentLen:], data)
	loopbackFrame.currentLen += uint32(len(data))
	if loopbackFrame.currentLen >= loopbackFrame.fileLen {
		t.complete_frame = loopbackFrame
		delete(t.incomplete_frames, frameNr)
		t.ready_status = true
		t.cond.Signal()
	}
}
