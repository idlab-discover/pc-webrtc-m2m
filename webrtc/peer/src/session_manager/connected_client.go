package session_manager

import (
	"fmt"
	"goweb/shared/src/logger"
	"sync"
)

type ConnectedClient struct {
	ClientID    uint
	CodecMode   string
	IsConnected bool
	VideoTracks map[string]*ClientTrackInfo
	AudioTracks map[string]*ClientTrackInfo
	mut         sync.Mutex
}

func NewConnectedClient(clientID uint) *ConnectedClient {
	return &ConnectedClient{
		VideoTracks: make(map[string]*ClientTrackInfo),
		AudioTracks: make(map[string]*ClientTrackInfo),
		mut:         sync.Mutex{},
	}
}

func (cc *ConnectedClient) AddVideoTracks(tracks []ClientTrackInfo) {
	cc.mut.Lock()
	defer cc.mut.Unlock()
	for _, track := range tracks {
		trackCopy := track
		cc.VideoTracks[track.TrackID] = &trackCopy
	}
	logger.LogWithMessage(NameManagerConnection, logger.ClientAddedSenderVideoTrack, true, true,
		fmt.Sprintf("clientID=%d nTracks=%d", cc.ClientID, len(tracks)),
	)
}

func (cc *ConnectedClient) AddAudioTracks(tracks []ClientTrackInfo) {
	cc.mut.Lock()
	defer cc.mut.Unlock()
	for _, track := range tracks {
		trackCopy := track
		cc.AudioTracks[track.TrackID] = &trackCopy
	}
	logger.LogWithMessage(NameManagerConnection, logger.ClientAddedSenderAudioTrack, true, true,
		fmt.Sprintf("clientID=%d nTracks=%d", cc.ClientID, len(tracks)),
	)
}

func (cc *ConnectedClient) SetTracksConnectionStatus(codecMode string, providers []ConnectionProviderMessage, isConnected bool) {
	cc.mut.Lock()
	defer cc.mut.Unlock()
	cc.CodecMode = codecMode
	// Overwrite all settings to ensure same behaviour with SessionManager
	for _, p := range providers {
		for _, t := range p.VideoTracks {
			track := t
			track.IsConnected = isConnected
			// TODO Do something with callback here
			cc.VideoTracks[t.TrackID] = &track
		}
		for _, t := range p.AudioTracks {
			track := t
			track.IsConnected = isConnected
			cc.AudioTracks[t.TrackID] = &track
		}
	}
}

// In C# this will trigger a callback so systems knows he can read now
func (cc *ConnectedClient) SetTracksAsConnected(videoTracks []TrackSimple, audioTracks []TrackSimple) {
	cc.mut.Lock()
	defer cc.mut.Unlock()
	for _, t := range videoTracks {
		cc.VideoTracks[t.TrackID].IsConnected = true
	}
	for _, t := range audioTracks {
		cc.AudioTracks[t.TrackID].IsConnected = true
	}
}
