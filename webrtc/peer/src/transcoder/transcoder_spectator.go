package transcoder

import (
	"goweb/shared/src/logger"
)

type TranscoderSpectator struct {
	isReady bool
}

const NameTranscoderSpectator = "TranscoderSpectator"

func NewTranscoderSpectator() *TranscoderSpectator {
	logger.Log(NameTranscoderSpectator, logger.Creating, true, true)
	tr := &TranscoderSpectator{
		isReady: true,
	}
	logger.Log(NameTranscoderSpectator, logger.Created, true, true)
	return tr
}

func (t *TranscoderSpectator) EncodeFrame(trackID string) (uint32, []byte) {
	return 0, nil
}
