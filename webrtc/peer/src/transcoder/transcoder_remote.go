package transcoder

import (
	"goweb/peer/src/proxy"
	"goweb/shared/src/logger"
)

type TranscoderRemote struct {
	proxy_con     *proxy.ProxyConnection
	tracksMapping map[string]uint32
	isReady       bool
}

const NameTranscoderRemote = "TranscoderRemote"

func NewTranscoderRemote(tracksMapping map[string]uint32, proxy_con *proxy.ProxyConnection) *TranscoderRemote {
	logger.Log(NameTranscoderRemote, logger.Creating, true, true)
	tr := &TranscoderRemote{
		tracksMapping: tracksMapping,
		proxy_con:     proxy_con,
		isReady:       true,
	}
	logger.Log(NameTranscoderRemote, logger.Created, true, true)
	return tr
}

func (t *TranscoderRemote) EncodeFrame(trackID string) (uint32, []byte) {
	return t.proxy_con.NextFrame(t.tracksMapping[trackID])
}
