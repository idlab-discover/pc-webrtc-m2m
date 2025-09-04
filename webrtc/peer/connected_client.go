package main

import (
	"encoding/json"
	"fmt"
	"sync"
)

type ClientTrack struct {
	ClientID      uint            `json:"clientID"`
	ProviderKey   string          `json:"providerKey"`
	TrackID       string          `json:"trackID"`
	CapturerType  string          `json:"capturerType"`
	TrackType     string          `json:"trackType"`
	TrackSettings json.RawMessage `json:"trackSettings"`
}

type ConnectedClient struct {
	ClientID    uint
	IsConnected bool
	VideoTracks map[string]ClientTrackInfo
	AudioTracks map[string]ClientTrackInfo
	mut         sync.Mutex
}

func NewConnectedClient(clientID uint) *ConnectedClient {
	return &ConnectedClient{
		VideoTracks: make(map[string]ClientTrackInfo),
		AudioTracks: make(map[string]ClientTrackInfo),
		mut:         sync.Mutex{},
	}
}

func (cc *ConnectedClient) AddVideoTracks(tracks []ClientTrackInfo) {
	cc.mut.Lock()
	defer cc.mut.Unlock()
	for _, track := range tracks {
		cc.VideoTracks[track.TrackID] = track
	}
	LogWithMessage(NameManagerConnection, ClientAddedSenderVideoTrack, true, true,
		fmt.Sprintf("clientID=%d nTracks=%d", cc.ClientID, len(tracks)),
	)
}

func (cc *ConnectedClient) AddAudioTracks(tracks []ClientTrackInfo) {
	cc.mut.Lock()
	defer cc.mut.Unlock()
	for _, track := range tracks {
		cc.AudioTracks[track.TrackID] = track
	}
	LogWithMessage(NameManagerConnection, ClientAddedSenderAudioTrack, true, true,
		fmt.Sprintf("clientID=%d nTracks=%d", cc.ClientID, len(tracks)),
	)
}
