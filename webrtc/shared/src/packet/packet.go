package packet

import "unsafe"

type FramePacketHeader struct {
	ClientNr  uint32 // 8
	FrameNr   uint32 // 12
	FrameLen  uint32 // 16
	SeqOffset uint32 // 20
	SeqLen    uint32 // 24
}

func BytesToFramePacket(b []byte) *FramePacket {
	return (*FramePacket)(unsafe.Pointer(&b[0]))
}

func BytesToFramePacketHeader(b []byte) *FramePacketHeader {
	return (*FramePacketHeader)(unsafe.Pointer(&b[0]))
}

type FramePacket struct {
	FramePacketHeader // 24
	Data              [1152]byte
}

type VideoFramePacket struct {
	FramePacketHeader        // 24
	CapturerID        uint32 // 28
	TileNr            uint32 // 32
	Data              [1148]byte
}

type AudioFramePacket struct {
	FramePacketHeader // 24
	Data              [1152]byte
}

/*type FramePacket struct {
	ClientNr  uint32
	FrameNr   uint32
	TileNr    uint32
	TileLen   uint32
	SeqOffset uint32
	SeqLen    uint32
	Data      [1144]byte
}*/

/*func NewFramePacket(clientNr, frameNr, tileNr, tileLen, seqOffset, seqLen uint32, dataSubArray []byte) *FramePacket {
	packet := &FramePacket{
		ClientNr:  clientNr,
		FrameNr:   frameNr,
		TileNr:    tileNr,
		TileLen:   tileLen,
		SeqOffset: seqOffset,
		SeqLen:    seqLen,
	}
	copy(packet.Data[:], dataSubArray[seqOffset:(seqOffset+seqLen)])
	return packet
}
*/
