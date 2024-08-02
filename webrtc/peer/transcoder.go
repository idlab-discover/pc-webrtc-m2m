package main

type Transcoder interface {
	UpdateBitrate(bitrate uint32)
	UpdateProjection()
	EncodeFrame(uint32) []byte
}

type TranscoderRemote struct {
	proxy_con    *ProxyConnection
	frameCounter uint32
	isReady      bool
}

func NewTranscoderRemote(proxy_con *ProxyConnection) *TranscoderRemote {
	return &TranscoderRemote{proxy_con, 0, true}
}

func (t *TranscoderRemote) UpdateBitrate(bitrate uint32) {
	// Do nothing
}

func (t *TranscoderRemote) UpdateProjection() {
	// Do nothing
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

func (t *TranscoderDummy) UpdateBitrate(bitrate uint32) {
	// Do nothing
}

func (t *TranscoderDummy) UpdateProjection() {
	// Do nothing
}

func (t *TranscoderDummy) EncodeFrame(tile uint32) []byte {
	return nil
}
