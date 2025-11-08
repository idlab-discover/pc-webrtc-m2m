package point_cloud

import "encoding/binary"

// AV1Payloader payloads AV1 packets
type PointCloudPayloader struct {
	frameCounter uint32
}

// Payload fragments a AV1 packet across one or more byte arrays
// See AV1Packet for description of AV1 Payload Header
func (p *PointCloudPayloader) Payload(mtu uint16, payload []byte) (payloads [][]byte) {
	payloadDataOffset := uint32(0)
	payloadLen := uint32(len(payload))
	payloadRemaining := payloadLen
	for payloadRemaining > 0 {
		currentFragmentSize := uint32(1000)
		if payloadRemaining < currentFragmentSize {
			currentFragmentSize = payloadRemaining
		}
		buf := make([]byte, currentFragmentSize+24)
		//	binary.LittleEndian.PutUint32(buf[0:], TilePacketType)
		binary.LittleEndian.PutUint32(buf[0:], 0)
		binary.LittleEndian.PutUint32(buf[4:], p.frameCounter)
		binary.LittleEndian.PutUint32(buf[8:], payloadLen)
		binary.LittleEndian.PutUint32(buf[12:], payloadDataOffset)
		binary.LittleEndian.PutUint32(buf[16:], currentFragmentSize)
		binary.LittleEndian.PutUint32(buf[20:], 0)

		copy(buf[24:], payload[payloadDataOffset:(payloadDataOffset+currentFragmentSize)])

		payloads = append(payloads, buf)
		payloadDataOffset += currentFragmentSize
		payloadRemaining -= currentFragmentSize
	}
	p.frameCounter++
	return payloads
}

func NewPointCloudPayloader() *PointCloudPayloader {
	return &PointCloudPayloader{0}
}
