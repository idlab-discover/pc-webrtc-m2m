package session_manager

import "encoding/json"

type ClientConnectedSettings struct {
	ClientID uint   `json:"clientID"`
	AuthKey  string `json:"authKey"`
}

type ClientTracksAdded struct {
	Providers     []ProviderMessage     `json:"providers"`
	VideoTracks   []ClientTrackInfo     `json:"videoTracks"`
	AudioTracks   []ClientTrackInfo     `json:"audioTracks"`
	RemoteClients []RemoteClientMessage `json:"remoteClients"`
}
type ProviderMessage struct {
	ProviderType     string          `json:"providerType"`
	ProviderKey      string          `json:"providerKey"`
	ProviderSettings json.RawMessage `json:"providerSettings"`
}
type RemoteClientMessage struct {
	ClientID    uint              `json:"clientID"`
	VideoTracks []ClientTrackInfo `json:"videoTracks"`
	AudioTracks []ClientTrackInfo `json:"audioTracks"`
}

type SessionJoinedMessage struct {
	DefaultProvider string                      `json:"defaultProvider"`
	CodecMode       string                      `json:"codecMode"`
	Providers       []ConnectionProviderMessage `json:"providers"`
	// TODO Add clients to this
}
