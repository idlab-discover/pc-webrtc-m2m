package transcoder

import "time"

type Transcoder interface {
	EncodeFrame(string) []byte
}

type TranscoderFixed struct {
	bitrate       uint
	fps           uint
	lastFrameTime time.Time
	sleepTime     time.Duration
}

func NewTranscoderFixed(bitrate uint, fps uint) *TranscoderFixed {
	interval := time.Duration(1000.0/float64(fps)) * time.Millisecond
	return &TranscoderFixed{bitrate: bitrate, fps: fps, lastFrameTime: time.Now(), sleepTime: interval}
}

func (t *TranscoderFixed) EncodeFrame(trackID string) []byte {
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
	return make([]byte, t.bitrate/t.fps/8)
}

/*type TranscoderRemote struct {
	proxy_con    *ProxyConnection
	frameCounter uint32
	isReady      bool
}

func NewTranscoderRemote(proxy_con *ProxyConnection) *TranscoderRemote {
	return &TranscoderRemote{proxy_con, 0, true}
}

func (t *TranscoderRemote) EncodeFrame(trackID string) []byte {
	return proxyConn.NextTile(trackID)
}

type TranscoderDummy struct {
	proxy_con    *ProxyConnection
	frameCounter uint32
	isReady      bool
}

func NewTranscoderDummy(proxy_con *ProxyConnection) *TranscoderDummy {
	return &TranscoderDummy{proxy_con, 0, true}
}

func (t *TranscoderDummy) EncodeFrame(trackID string) []byte {
	return nil
}

type TranscoderDebug struct {
	dbgConfig      DebugConfig
	isReady        bool
	m_video        sync.Mutex
	cond_video     map[uint32]*sync.Cond
	complete_tiles map[uint32]bool
}

func NewTranscoderDebug(dbgConfig DebugConfig) *TranscoderDebug {
	t := &TranscoderDebug{
		dbgConfig:      dbgConfig,
		isReady:        true,
		m_video:        sync.Mutex{},
		cond_video:     make(map[uint32]*sync.Cond, 0),
		complete_tiles: make(map[uint32]bool, 0),
	}
	for i := uint32(0); i < uint32(len(t.dbgConfig.Descriptions)); i++ {
		t.cond_video[i] = sync.NewCond(&t.m_video)
	}
	t.produceFrames()
	return t
}

func (t *TranscoderDebug) EncodeFrame(trackID string) []byte {
	t.m_video.Lock()
	if !t.complete_tiles[tile] {
		t.cond_video[tile].Wait()
	}
	t.complete_tiles[tile] = false
	t.m_video.Unlock()
	time.Sleep(time.Duration(t.dbgConfig.Descriptions[tile].Delay) * time.Millisecond)
	return make([]byte, t.dbgConfig.Descriptions[tile].Bitrate/t.dbgConfig.Fps/8)
}

func (t *TranscoderDebug) produceFrames() {
	go func() {
		interval := time.Duration(1000.0/float64(t.dbgConfig.Fps)) * time.Millisecond
		ticker := time.NewTicker(interval)
		defer ticker.Stop()
		for {
			select {
			case <-ticker.C:
				// Add new frame
				t.m_video.Lock()
				for i := uint32(0); i < uint32(len(t.dbgConfig.Descriptions)); i++ {
					t.complete_tiles[i] = true
					t.cond_video[i].Broadcast()
				}
				t.m_video.Unlock()
			}
		}
	}()
}*/
