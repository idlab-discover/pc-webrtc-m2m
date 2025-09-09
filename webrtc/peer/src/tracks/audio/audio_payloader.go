package audio

import "encoding/binary"

// AV1Payloader payloads AV1 packets
type AudioPayloader struct {
	frameCounter uint32
}

// Payload fragments a AV1 packet across one or more byte arrays
// See AudioPayloader for description of AV1 Payload Header
func (p *AudioPayloader) Payload(mtu uint16, payload []byte) (payloads [][]byte) {
	payloadDataOffset := uint32(0)
	payloadLen := uint32(len(payload))
	payloadRemaining := payloadLen
	for payloadRemaining > 0 {
		currentFragmentSize := uint32(1152)
		if payloadRemaining < currentFragmentSize {
			currentFragmentSize = payloadRemaining
		}
		buf := make([]byte, currentFragmentSize+20)
		binary.LittleEndian.PutUint32(buf[0:], 0)
		binary.LittleEndian.PutUint32(buf[4:], p.frameCounter)

		binary.LittleEndian.PutUint32(buf[8:], payloadLen)
		binary.LittleEndian.PutUint32(buf[12:], payloadDataOffset)
		binary.LittleEndian.PutUint32(buf[16:], currentFragmentSize)
		copy(buf[20:], payload[payloadDataOffset:(payloadDataOffset+currentFragmentSize)])
		payloads = append(payloads, buf)
		payloadDataOffset += currentFragmentSize
		payloadRemaining -= currentFragmentSize
	}
	p.frameCounter++
	return payloads
}

func NewAudioPayloader() *AudioPayloader {
	return &AudioPayloader{}
}
