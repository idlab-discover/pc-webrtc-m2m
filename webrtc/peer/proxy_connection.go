package main

import (
	"bytes"
	"encoding/binary"
	"fmt"
	"net"
	"strconv"
	"strings"
	"sync"
	"time"
)

const (
	ReadyPacketType        uint32 = 0
	TilePacketType         uint32 = 1
	AudioPacketType        uint32 = 2
	ControlPacketType      uint32 = 3
	TrackStatusPacketType  uint32 = 4
	CapturerIntrinsicsType uint32 = 5
)

// TODO seperate this into different struct. We also want to use packet type for control packets (i.e. fov)
type RemoteInputPacketHeader struct {
}

// TODO Refactor this
type RemoteInputVideoPacketHeader struct {
	ClientNr    uint32
	FrameNr     uint32
	FrameLen    uint32
	FrameOffset uint32
	PacketLen   uint32
	CapturerID  uint32
	TileNr      uint32
}

type RemoteInputAudioPacketHeader struct {
	// TODO: do we have special audio fields?
	ClientNr    uint32
	FrameNr     uint32
	FrameLen    uint32
	FrameOffset uint32
	PacketLen   uint32
}

// TODO split audio and video? Technically can both use this struct
type RemoteTile struct {
	frameNr    uint32
	currentLen uint32
	fileLen    uint32
	fileData   []byte
}

type RemoteCapturer struct {
	capturerID uint32

	// Cond gives better performance compared to high priority lock
	// High prio lock => latency between 3 and 10ms
	// Condi lock => latency between 0 and 1ms
	cond_video []*sync.Cond // TODO add one for every tile

	incomplete_tiles []map[uint32]RemoteTile // We probably want to limit the max number of incomplete tiles?
	//And maybe use something else than a simple map because atm there is technically a max frame limit
	complete_tiles [][1]RemoteTile
	ready_status   []bool
}

func NewRemoteCapturer(capturerID uint32, nTiles uint32, m *sync.Mutex) *RemoteCapturer {
	rm := &RemoteCapturer{
		capturerID:       capturerID,
		cond_video:       make([]*sync.Cond, nTiles),
		incomplete_tiles: make([]map[uint32]RemoteTile, nTiles),
		complete_tiles:   make([][1]RemoteTile, nTiles),
		ready_status:     make([]bool, nTiles),
	}
	for i := uint32(0); i < nTiles; i++ {
		rm.cond_video[i] = sync.NewCond(m)
		rm.incomplete_tiles[i] = make(map[uint32]RemoteTile)
		//rm.complete_tiles[i] = make([]RemoteTile, 1) // We will only save 1 frame for each tile max
	}
	println("Creating remote capturer with ID", capturerID, "and", nTiles, "tiles")
	return rm
}

type ProxyConnection struct {
	addr *net.UDPAddr
	conn *net.UDPConn
	m    PriorityLock

	remote_capturers map[uint32]*RemoteCapturer // CapturerID -> RemoteCapturer

	incomplete_audio_frames map[uint32]RemoteTile
	complete_audio_frames   []RemoteTile

	frame_counters map[uint32]uint32
	send_mutex     sync.Mutex

	mtx_video sync.Mutex

	cond_audio *sync.Cond
	mtx_audio  sync.Mutex

	wsHandler *WebsocketHandler
}

type SetupCallback func(int)

func NewProxyConnection() *ProxyConnection {
	return &ProxyConnection{nil, nil, NewPriorityPreferenceLock(),
		make(map[uint32]*RemoteCapturer),                   // Video
		make(map[uint32]RemoteTile), make([]RemoteTile, 0), // Audio
		make(map[uint32]uint32), sync.Mutex{},
		sync.Mutex{},      // Video mutex
		nil, sync.Mutex{}, // Audio mutex
		nil,
	}
}

func (pc *ProxyConnection) sendPacket(b []byte, offset uint32, packet_type uint32) {
	buffProxy := make([]byte, 1300)
	binary.LittleEndian.PutUint32(buffProxy[0:], packet_type)
	// TODO Add internal ID mapping string to int
	copy(buffProxy[4:], b[offset:])
	pc.send_mutex.Lock()
	_, err := pc.conn.WriteToUDP(buffProxy, pc.addr)
	pc.send_mutex.Unlock()
	if err != nil {
		fmt.Printf("WebRTCPeer: ERROR: %s\n", err)
		panic(err)
	}
}

func (pc *ProxyConnection) SetupConnection(port string) {
	address, err := net.ResolveUDPAddr("udp", port)
	if err != nil {
		fmt.Printf("WebRTCPeer: ERROR: %s\n", err)
		return
	}

	// Create a UDP connection
	pc.conn, err = net.ListenUDP("udp", address)
	if err != nil {
		fmt.Printf("WebRTCPeer: ERROR: %s\n", err)
		return
	}

	// Create a buffer to read incoming messages
	port_info := strings.Split(port, ":")
	if port_info[0] == "" {
		port_int, _ := strconv.Atoi(port_info[1])
		port_string := strconv.Itoa(port_int + 1)
		port = "127.0.0.1:" + port_string
	} else {
		port_int, _ := strconv.Atoi(port_info[1])
		port_string := strconv.Itoa(port_int + 1)
		port = port_info[0] + ":" + port_string
	}

	pc.addr, err = net.ResolveUDPAddr("udp", port)
	if err != nil {
		fmt.Printf("WebRTCPeer: ERROR: %s\n", err)
		return
	}

	pc.SendPeerReadyPacket()
	buffer := make([]byte, 1500)

	// Wait for incoming messages
	fmt.Println("WebRTCPeer: Waiting for a message...", port, pc.addr.IP.String())
	_, pc.addr, err = pc.conn.ReadFromUDP(buffer)
	if err != nil {
		fmt.Printf("WebRTCPeer: ERROR: %s\n", err)
		return
	}

	fmt.Println("WebRTCPeer: Connected to Unity DLL")

}

func (pc *ProxyConnection) StartListening(nCapturers uint32, nTiles uint32) {
	println("WebRTCPeer: Start listening for incoming data from DLL")
	pc.cond_audio = sync.NewCond(&pc.mtx_audio)
	// TODO make this dynamic
	for i := 0; i < int(nCapturers); i++ {
		pc.remote_capturers[uint32(i)] = NewRemoteCapturer(uint32(i), nTiles, &pc.mtx_video)
	}
	go func() {
		for {
			buffer := make([]byte, 1500)
			_, _, _ = pc.conn.ReadFromUDP(buffer)
			ptype := binary.LittleEndian.Uint32(buffer[:4])
			if ptype == TilePacketType {
				bufBinary := bytes.NewBuffer(buffer[4:32])
				var p RemoteInputVideoPacketHeader
				err := binary.Read(bufBinary, binary.LittleEndian, &p) // TODO: make sure we check endianess of system here and use that instead!
				if err != nil {
					fmt.Printf("WebRTCPeer: Error: %s\n", err)
					return
				}

				pc.mtx_video.Lock()
				incompleteTileBuffer := pc.remote_capturers[p.CapturerID].incomplete_tiles[p.TileNr]
				//pc.m.Lock()
				_, exists := incompleteTileBuffer[p.FrameNr]
				if !exists {
					r := RemoteTile{
						p.FrameNr,
						0,
						p.FrameLen,
						make([]byte, p.FrameLen),
					}
					incompleteTileBuffer[p.FrameNr] = r
					//fmt.Printf("WebRTCPeer: [VIDEO] DLL first packet of frame %d from tile %d with length %d  at %d\n",
					//	p.FrameNr, p.TileNr, p.FrameLen, time.Now().UnixNano()/int64(time.Millisecond))
				}

				value := incompleteTileBuffer[p.FrameNr]
				copy(value.fileData[p.FrameOffset:p.FrameOffset+p.PacketLen], buffer[32:32+p.PacketLen])
				value.currentLen = value.currentLen + p.PacketLen
				pc.remote_capturers[p.CapturerID].incomplete_tiles[p.TileNr][p.FrameNr] = value
				if value.currentLen == value.fileLen {
					//fmt.Printf("WebRTCPeer: [VIDEO] DLL sent frame %d from tile %d with length %d  at %d\n",
					//	p.FrameNr, p.TileNr, p.FrameLen, time.Now().UnixNano()/int64(time.Millisecond))
					// For now we will only save 1 frame for each tile max (do we want to save more?)
					// TODO use channels instead
					pc.remote_capturers[p.CapturerID].complete_tiles[p.TileNr][0] = value
					pc.remote_capturers[p.CapturerID].ready_status[p.TileNr] = true
					delete(pc.remote_capturers[p.CapturerID].incomplete_tiles[p.TileNr], p.FrameNr)
					pc.remote_capturers[p.CapturerID].cond_video[p.TileNr].Broadcast()
				}
				pc.mtx_video.Unlock()
				//pc.m.Unlock()
			} else if ptype == AudioPacketType {
				bufBinary := bytes.NewBuffer(buffer[4:24])
				var p RemoteInputAudioPacketHeader
				err := binary.Read(bufBinary, binary.LittleEndian, &p) // TODO: make sure we check endianess of system here and use that instead!
				if err != nil {
					fmt.Printf("WebRTCPeer: Error: %s\n", err)
					return
				}
				pc.mtx_audio.Lock()
				_, exists := pc.incomplete_audio_frames[p.FrameNr]
				if !exists {
					r := RemoteTile{
						p.FrameNr,
						0,
						p.FrameLen,
						make([]byte, p.FrameLen),
					}
					pc.incomplete_audio_frames[p.FrameNr] = r
					//		fmt.Printf("WebRTCPeer: [AUDIO] DLL first packet of audio frame %d with length %d  at %d\n",
					//	p.FrameNr, p.FrameLen, time.Now().UnixNano()/int64(time.Millisecond))
				}
				value := pc.incomplete_audio_frames[p.FrameNr]
				copy(value.fileData[p.FrameOffset:p.FrameOffset+p.PacketLen], buffer[24:24+p.PacketLen])
				value.currentLen = value.currentLen + p.PacketLen
				pc.incomplete_audio_frames[p.FrameNr] = value
				if value.currentLen == value.fileLen {
					//fmt.Printf("WebRTCPeer: [AUDIO] DLL sent audio frame %d with length %d  at %d\n",
					//	p.FrameNr, p.FrameLen, time.Now().UnixNano()/int64(time.Millisecond))
					// For now we will only save 1 frame for each tile max (do we want to save more?)
					// TODO use channels instead
					if len(pc.complete_audio_frames) == 0 {
						pc.complete_audio_frames = append(pc.complete_audio_frames, value)
					} else {
						pc.complete_audio_frames[0] = value
					}

					delete(pc.incomplete_audio_frames, p.FrameNr)
					pc.cond_audio.Broadcast()
				}
				pc.mtx_audio.Unlock()
			} else if ptype == ControlPacketType {
				if pc.wsHandler != nil {
					pc.wsHandler.SendMessage(WebsocketPacket{
						uint64(*clientID),
						7,
						string(buffer[4:]),
					})
				}

			} else if ptype == CapturerIntrinsicsType {
				go func() {
					for pc.wsHandler == nil {
						time.Sleep(100 * time.Millisecond)
					}
					pc.wsHandler.SendMessage(WebsocketPacket{
						uint64(*clientID),
						8,
						string(buffer[4:]),
					})
				}()
			}

		}
	}()
}

func (pc *ProxyConnection) SendPeerReadyPacket() {
	pc.sendPacket(make([]byte, 100), 0, ReadyPacketType)
}

func (pc *ProxyConnection) SendTilePacket(b []byte, offset uint32) {
	pc.sendPacket(b, offset, TilePacketType)
}

func (pc *ProxyConnection) SendAudioPacket(b []byte, offset uint32) {
	pc.sendPacket(b, offset, AudioPacketType)
}

func (pc *ProxyConnection) SendControlPacket(b []byte) {
	pc.sendPacket(b, 0, ControlPacketType)
}

func (pc *ProxyConnection) SendTrackStatusPacket(clientID uint32, lastFrameNr uint32, capturerID uint32, tileID uint32, isVideo bool, wasAdded bool) {
	b := make([]byte, 4+4+4+4+1+1)
	binary.LittleEndian.PutUint32(b[0:], clientID)
	binary.LittleEndian.PutUint32(b[4:], lastFrameNr)
	binary.LittleEndian.PutUint32(b[8:], capturerID)
	binary.LittleEndian.PutUint32(b[12:], tileID)
	if isVideo {
		b[16] = 1
	} else {
		b[16] = 0
	}

	if wasAdded {
		b[17] = 1
	} else {
		b[17] = 0
	}
	pc.sendPacket(b, 0, TrackStatusPacketType)
}

func (pc *ProxyConnection) SendCapturerIntrinsicsPacket(clientID uint32, cameraIntrinsics string) {
	b := make([]byte, 4+len(cameraIntrinsics))
	binary.LittleEndian.PutUint32(b[0:], clientID)
	copy(b[4:], []byte(cameraIntrinsics))

	pc.sendPacket(b, 0, CapturerIntrinsicsType)
}

func (pc *ProxyConnection) NextTile(capturerID uint32, tile uint32) []byte {
	isNextFrameReady := false
	remoteCapturer := pc.remote_capturers[capturerID]
	for !isNextFrameReady {
		pc.mtx_video.Lock()
		//pc.m.HighPriorityLock()
		//_, exists := pc.complete_tiles[tile]
		//if !exists {
		//	pc.complete_tiles[tile] = make([]RemoteTile, 0, 1)
		//}
		if remoteCapturer.ready_status[tile] {
			isNextFrameReady = true
		} else {
			remoteCapturer.cond_video[tile].Wait()
			isNextFrameReady = true
			//pc.m.HighPriorityUnlock()
			//time.Sleep(time.Millisecond)
		}
	}
	data := remoteCapturer.complete_tiles[tile][0].fileData
	frameNr := remoteCapturer.complete_tiles[tile][0].frameNr
	if frameNr%10 == 0 {
		fmt.Printf("WebRTCPeer: [VIDEO] Sending out frame %d of capturer %d from tile %d with size %d at %d\n",
			frameNr, capturerID, tile, remoteCapturer.complete_tiles[tile][0].fileLen, time.Now().UnixNano()/int64(time.Millisecond))
	}
	remoteCapturer.ready_status[tile] = false
	//remoteCapturer.complete_tiles[tile] = remoteCapturer.complete_tiles[tile][:0] // Clear the complete tile buffer for this tile
	// Do we still need frame counter? Seems more logical to use the actual frame nr
	pc.frame_counters[tile] += 1
	pc.mtx_video.Unlock()
	//pc.m.HighPriorityUnlock()
	return data
}

func (pc *ProxyConnection) NextAudioFrame() []byte {
	isNextFrameReady := false
	for !isNextFrameReady {
		pc.mtx_audio.Lock()
		//pc.m.HighPriorityLock()
		//_, exists := pc.complete_tiles[tile]
		//if !exists {
		//	pc.complete_tiles[tile] = make([]RemoteTile, 0, 1)
		//}
		if len(pc.complete_audio_frames) > 0 {
			isNextFrameReady = true
		} else {
			pc.cond_audio.Wait()
			isNextFrameReady = true
			//pc.m.HighPriorityUnlock()
			//time.Sleep(time.Millisecond)
		}
	}
	data := pc.complete_audio_frames[0].fileData
	//frameNr := pc.complete_audio_frames[0].frameNr
	/*	fmt.Printf("WebRTCPeer: [AUDIO] Sending out audio frame %d with size %d at %d\n",
		pc.complete_audio_frames[0].frameNr, pc.complete_audio_frames[0].fileLen, time.Now().UnixNano()/int64(time.Millisecond))*/
	pc.complete_audio_frames = pc.complete_audio_frames[:0]
	// Do we still need frame counter? Seems more logical to use the actual frame nr
	pc.mtx_audio.Unlock()
	//pc.m.HighPriorityUnlock()
	return data
}
