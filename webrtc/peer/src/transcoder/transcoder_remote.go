package transcoder

import (
	"goweb/peer/src/proxy"
)

type TranscoderRemote struct {
	proxy_con     *proxy.ProxyConnection
	tracksMapping map[string]uint32
	frameCounter  uint32
	isReady       bool
}

func NewTranscoderRemote(tracksMapping map[string]uint32, proxy_con *proxy.ProxyConnection) *TranscoderRemote {
	return &TranscoderRemote{
		tracksMapping: tracksMapping,
		proxy_con:     proxy_con,
		frameCounter:  0,
		isReady:       true,
	}
}

func (t *TranscoderRemote) EncodeFrame(trackID string) []byte {
	return t.proxy_con.NextFrame(t.tracksMapping[trackID])
}
