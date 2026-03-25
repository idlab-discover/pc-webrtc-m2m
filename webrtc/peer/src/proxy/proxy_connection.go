package proxy

import (
	"bytes"
	"encoding/binary"
	"fmt"
	"goweb/peer/src/utils"
	"goweb/shared/src/logger"
	"net"
	"sync"
)

const NameProxyConnection = "ProxyConnection"

const (
	ReadyPacketType        uint32 = 0
	FramePacketType        uint32 = 1
	RemoteClientTracksType uint32 = 2
	AudioPacketType        uint32 = 22
	ControlPacketType      uint32 = 3
	TrackStatusPacketType  uint32 = 4
	CapturerIntrinsicsType uint32 = 5
)

// TODO seperate this into different struct. We also want to use packet type for control packets (i.e. fov)
type RemoteInputPacketHeader struct {
	InternalTrackID uint32 // 4
	ClientID        uint32 // 8
	FrameNr         uint32 // 12
	FrameLen        uint32 // 16
	FrameOffset     uint32 // 20
	PacketLen       uint32 // 24
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

type RemoteFrame struct {
	frameNr    uint32
	currentLen uint32
	fileLen    uint32
	fileData   []byte
}

// TODO split audio and video? Technically can both use this struct
type RemoteTile struct {
	frameNr    uint32
	currentLen uint32
	fileLen    uint32
	fileData   []byte
}

type RemoteCapturer struct {
	internalTrackID uint32
	mtx             sync.Mutex
	// Cond gives better performance compared to high priority lock
	// High prio lock => latency between 3 and 10ms
	// Condi lock => latency between 0 and 1ms
	cond *sync.Cond // TODO add one for every tile

	incomplete_frames map[uint32]*RemoteFrame // We probably want to limit the max number of incomplete tiles?
	//And maybe use something else than a simple map because atm there is technically a max frame limit
	complete_frame *RemoteFrame
	ready_status   bool
}

func NewRemoteCapturer(internalTrackID uint32) *RemoteCapturer {
	rm := &RemoteCapturer{
		internalTrackID:   internalTrackID,
		mtx:               sync.Mutex{},
		incomplete_frames: make(map[uint32]*RemoteFrame),
		complete_frame:    nil,
		ready_status:      false,
	}
	rm.cond = sync.NewCond(&rm.mtx)
	println("Creating remote capturer with internal ID", internalTrackID)
	return rm
}

func (rc *RemoteCapturer) addFrameContent(p RemoteInputPacketHeader, buffer []byte) {
	rc.mtx.Lock()
	defer rc.mtx.Unlock()
	incomplete_frame, exists := rc.incomplete_frames[p.FrameNr]
	if !exists {
		//fmt.Println("Creating new incomplete frame for frameNr", p.FrameNr, "with length", p.FrameLen, " and internalID", p.InternalTrackID, " for clientID", p.ClientID)
		incomplete_frame = &RemoteFrame{
			frameNr:    p.FrameNr,
			currentLen: 0,
			fileLen:    p.FrameLen,
			fileData:   make([]byte, p.FrameLen),
		}
		rc.incomplete_frames[p.FrameNr] = incomplete_frame
		if p.FrameNr%10 == 0 {
			logger.LogFrameWithMessage(NameProxyConnection, logger.ProxyConFirstPacketRecv, true, true, fmt.Sprintf("trackID=%s frameSize=%d", p.InternalTrackID, p.FrameLen), (uint)(p.FrameNr))
		}
	}
	copy(incomplete_frame.fileData[p.FrameOffset:p.FrameOffset+p.PacketLen], buffer[28:28+p.PacketLen])
	incomplete_frame.currentLen = incomplete_frame.currentLen + p.PacketLen
	if incomplete_frame.currentLen == incomplete_frame.fileLen {
		rc.complete_frame = incomplete_frame
		rc.ready_status = true
		delete(rc.incomplete_frames, p.FrameNr)
		rc.cond.Broadcast()
		if p.FrameNr%10 == 0 {
			logger.LogFrameWithMessage(NameProxyConnection, logger.ProxyConFullyRecv, true, true, fmt.Sprintf("trackID=%s frameSize=%d", p.InternalTrackID, p.FrameLen), (uint)(p.FrameNr))
		}
	}
}

type OnSubscribeToTracksReceived func(clientID uint32, videoTrackIDs []string, audioTrackIDs []string)

type pendingSubscription struct {
	clientID uint32
	videoIDs []string
	audioIDs []string
}

type ProxyConnection struct {
	addr *net.UDPAddr
	conn *net.UDPConn
	m    utils.PriorityLock

	remote_capturers     map[uint32]*RemoteCapturer // TODO Try this without locking
	remote_client_tracks map[string]uint32          // Map trackID to internalTrackID
	send_mutex           sync.Mutex
	rm_client_mutex      sync.Mutex

	WsHandler *utils.ThreadSafeWebsocket

	onSubscribeToTracksReceived OnSubscribeToTracksReceived
	pending_subscriptions       []pendingSubscription
}

type SetupCallback func(int)

func NewProxyConnection() *ProxyConnection {
	return &ProxyConnection{
		addr:                 nil,
		conn:                 nil,
		m:                    utils.NewPriorityPreferenceLock(),
		remote_capturers:     make(map[uint32]*RemoteCapturer), // Video and audio
		remote_client_tracks: map[string]uint32{},
		send_mutex:            sync.Mutex{},
		WsHandler:             nil,
		pending_subscriptions: make([]pendingSubscription, 0),
	}
}

func (pc *ProxyConnection) SetOnSubscribeToTracksReceived(cb OnSubscribeToTracksReceived) {
	pc.rm_client_mutex.Lock()
	pc.onSubscribeToTracksReceived = cb
	pending := pc.pending_subscriptions
	pc.pending_subscriptions = pc.pending_subscriptions[:0]
	pc.rm_client_mutex.Unlock()
	// Drain in a goroutine to avoid deadlocking if the caller holds a lock that cb also needs.
	go func() {
		for _, p := range pending {
			cb(p.clientID, p.videoIDs, p.audioIDs)
		}
	}()
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

func (pc *ProxyConnection) sendPacketWithID(b []byte, offset uint32, packet_type uint32, internalTrackID uint32) {
	buffProxy := make([]byte, 1300)
	binary.LittleEndian.PutUint32(buffProxy[0:], packet_type)
	binary.LittleEndian.PutUint32(buffProxy[4:], internalTrackID)
	// TODO Add internal ID mapping string to int
	copy(buffProxy[8:], b[offset:])
	pc.send_mutex.Lock()
	_, err := pc.conn.WriteToUDP(buffProxy, pc.addr)
	pc.send_mutex.Unlock()
	if err != nil {
		fmt.Printf("WebRTCPeer: ERROR: %s\n", err)
		panic(err)
	}
}

func (pc *ProxyConnection) SetupConnection(portThis string, portDLL string) {
	address, err := net.ResolveUDPAddr("udp", "127.0.0.1:"+portThis)
	if err != nil {
		fmt.Printf("WebRTCPeer: ERROR 0: %s\n", err)
		return
	}

	// Create a UDP connection
	pc.conn, err = net.ListenUDP("udp", address)
	if err != nil {
		fmt.Printf("WebRTCPeer: ERROR 1: %s\n", err)
		return
	}

	// Create a buffer to read incoming messages
	addrDLL := "127.0.0.1:" + portDLL

	pc.addr, err = net.ResolveUDPAddr("udp", addrDLL)
	if err != nil {
		fmt.Printf("WebRTCPeer: ERROR 2: %s\n", err)
		return
	}

	pc.SendPeerReadyPacket()
	buffer := make([]byte, 1500)

	// Wait for incoming messages
	fmt.Println("WebRTCPeer: Waiting for a message...", portThis, pc.addr.IP.String())
	_, pc.addr, err = pc.conn.ReadFromUDP(buffer)
	if err != nil {
		fmt.Printf("WebRTCPeer: ERROR 3: %s\n", err)
		return
	}

	fmt.Println("WebRTCPeer: Connected to Unity DLL")

}

type CameraInfo struct {
	Info string `json:"info"`
}

func (pc *ProxyConnection) StartListening(nTracks uint32) {
	println("WebRTCPeer: Start listening for incoming data from DLL")

	// TODO make this dynamic
	for i := 0; i < int(nTracks); i++ {
		pc.remote_capturers[uint32(i)] = NewRemoteCapturer(uint32(i))
	}
	go func() {
		for {
			buffer := make([]byte, 1500)
			//fmt.Printf("WebRTCPeer: Waiting for frame packet for %d tracks...\n", nTracks)
			_, _, err := pc.conn.ReadFromUDP(buffer)
			if err != nil {
				fmt.Printf("WebRTCPeer: Error reading UDP packet: %s\n", err)
				continue
			}
			ptype := binary.LittleEndian.Uint32(buffer[:4])
			//fmt.Printf("WebRTCPeer: Received packet of type %d\n", ptype)
			if ptype == FramePacketType {
				bufBinary := bytes.NewBuffer(buffer[4:28])
				var p RemoteInputPacketHeader
				err := binary.Read(bufBinary, binary.LittleEndian, &p) // TODO: make sure we check endianess of system here and use that instead!
				if err != nil {
					fmt.Printf("WebRTCPeer: Error: %s\n", err)
					return
				}
				var remote_capturer *RemoteCapturer
				var exists bool
				if remote_capturer, exists = pc.remote_capturers[p.InternalTrackID]; !exists {
					continue
				}
				remote_capturer.addFrameContent(p, buffer)
			} else if ptype == ControlPacketType {
				if pc.WsHandler != nil {
					pc.WsHandler.WriteJSONMessageSafe(
						"ControlPacket",
						CameraInfo{Info: string(buffer[4:])},
					)
				}
			} else if ptype == RemoteClientTracksType {
				pc.rm_client_mutex.Lock()
				bufBinary := bytes.NewBuffer(buffer[4:])
				var clientID uint32
				err := binary.Read(bufBinary, binary.LittleEndian, &clientID)
				if err != nil {
					fmt.Printf("WebRTCPeer: Error: %s\n", err)
					return
				}
				var nEntries uint32
				err = binary.Read(bufBinary, binary.LittleEndian, &nEntries)
				if err != nil {
					fmt.Printf("WebRTCPeer: Error: %s\n", err)
					return
				}
				println("Received subscription for", nEntries, "tracks for client", clientID)
				if nEntries == 0 {
					pc.rm_client_mutex.Unlock()
					continue
				}
				videoIDs := make([]string, 0)
				audioIDs := make([]string, 0)
				for i := uint32(0); i < nEntries; i++ {
					var trackIDLen uint32
					err := binary.Read(bufBinary, binary.LittleEndian, &trackIDLen)
					if err != nil {
						fmt.Printf("WebRTCPeer: Error: %s\n", err)
						return
					}
					var trackID []byte
					trackID = make([]byte, trackIDLen)
					err = binary.Read(bufBinary, binary.LittleEndian, &trackID)
					if err != nil {
						fmt.Printf("WebRTCPeer: Error: %s\n", err)
						return
					}
					var internalTrackID uint32
					err = binary.Read(bufBinary, binary.LittleEndian, &internalTrackID)
					if err != nil {
						fmt.Printf("WebRTCPeer: Error: %s\n", err)
						return
					}
					var isVideo bool
					err = binary.Read(bufBinary, binary.LittleEndian, &isVideo)
					if err != nil {
						fmt.Printf("WebRTCPeer: Error: %s\n", err)
						return
					}
					if isVideo {
						videoIDs = append(videoIDs, string(trackID))
					} else {
						audioIDs = append(audioIDs, string(trackID))
					}
					pc.remote_client_tracks[string(trackID)] = internalTrackID
					println("Mapping remote track", string(trackID), "to internal ID", internalTrackID, "isVideo:", isVideo)
				}
				if pc.onSubscribeToTracksReceived != nil {
					pc.onSubscribeToTracksReceived(clientID, videoIDs, audioIDs)
				} else {
					pc.pending_subscriptions = append(pc.pending_subscriptions, pendingSubscription{clientID, videoIDs, audioIDs})
				}
				pc.rm_client_mutex.Unlock()
			}
		}
	}()
}

func (pc *ProxyConnection) SendPeerReadyPacket() {
	pc.sendPacket(make([]byte, 100), 0, ReadyPacketType)
}

func (pc *ProxyConnection) SendFramePacket(internalTrackID uint32, b []byte, offset uint32) {
	pc.sendPacketWithID(b, offset, FramePacketType, internalTrackID)
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

func (pc *ProxyConnection) NextFrame(internalTrackID uint32) (uint32, []byte) {
	isNextFrameReady := false
	remoteCapturer := pc.remote_capturers[internalTrackID]
	for !isNextFrameReady {
		remoteCapturer.mtx.Lock()
		//pc.m.HighPriorityLock()
		//_, exists := pc.complete_tiles[tile]
		//if !exists {
		//	pc.complete_tiles[tile] = make([]RemoteTile, 0, 1)
		//}
		if remoteCapturer.ready_status {
			isNextFrameReady = true
		} else {
			remoteCapturer.cond.Wait()
			isNextFrameReady = true
			//pc.m.HighPriorityUnlock()
			//time.Sleep(time.Millisecond)
		}
	}
	data := remoteCapturer.complete_frame.fileData
	frameNr := remoteCapturer.complete_frame.frameNr
	if frameNr%10 == 0 {
		//fmt.Printf("WebRTCPeer: [VIDEO] Sending out frame %d of internalTrackID %d with size %d at %d\n",
		//	frameNr, internalTrackID, remoteCapturer.complete_frame.fileLen, time.Now().UnixNano()/int64(time.Millisecond))
	}
	remoteCapturer.ready_status = false
	//remoteCapturer.complete_tiles[tile] = remoteCapturer.complete_tiles[tile][:0] // Clear the complete tile buffer for this tile
	// Do we still need frame counter? Seems more logical to use the actual frame nr
	remoteCapturer.mtx.Unlock()
	//pc.m.HighPriorityUnlock()
	return frameNr, data
}

func (pc *ProxyConnection) GetInternalTrackID(trackID string) (uint32, bool) {
	pc.rm_client_mutex.Lock()
	defer pc.rm_client_mutex.Unlock()
	internalID, exists := pc.remote_client_tracks[trackID]
	return internalID, exists
}
