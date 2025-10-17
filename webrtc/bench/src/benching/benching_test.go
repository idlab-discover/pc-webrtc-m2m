package benching

import (
	"bytes"
	"encoding/binary"
	"goweb/shared/src/packet"
	"testing"
	"time"
)

var sampleFrame = func() []byte {
	b := make([]byte, 1180)
	// fill with some values (little-endian)
	binary.LittleEndian.PutUint32(b[0:4], 1)
	binary.LittleEndian.PutUint32(b[4:8], 2)
	binary.LittleEndian.PutUint32(b[8:12], 3)
	binary.LittleEndian.PutUint32(b[12:16], 4)
	binary.LittleEndian.PutUint32(b[16:20], 5)
	return b
}()

var sampleFrameHeader = func() []byte {
	b := make([]byte, 20)
	// fill with some values (little-endian)
	binary.LittleEndian.PutUint32(b[0:4], 1)
	binary.LittleEndian.PutUint32(b[4:8], 2)
	binary.LittleEndian.PutUint32(b[8:12], 3)
	binary.LittleEndian.PutUint32(b[12:16], 4)
	binary.LittleEndian.PutUint32(b[16:20], 5)
	return b
}()

func BenchmarkBinaryReadFrame(b *testing.B) {
	var h packet.FramePacket
	r := bytes.NewReader(sampleFrame)
	b.ResetTimer()
	for i := 0; i < b.N; i++ {
		r.Reset(sampleFrame) // simulate reading same bytes repeatedly
		if err := binary.Read(r, binary.LittleEndian, &h); err != nil {
			b.Fatal(err)
		}
	}
}

func BenchmarkManualFrame(b *testing.B) {
	for i := 0; i < b.N; i++ {
		a := binary.LittleEndian.Uint32(sampleFrame[0:4])
		_ = a
		bv := binary.LittleEndian.Uint32(sampleFrame[4:8])
		_ = bv
		_ = binary.LittleEndian.Uint32(sampleFrame[8:12])
		_ = binary.LittleEndian.Uint32(sampleFrame[12:16])
		_ = binary.LittleEndian.Uint32(sampleFrame[16:20])
	}
}

func BenchmarkUnsafeFrame(b *testing.B) {
	for i := 0; i < b.N; i++ {
		h := *packet.BytesToFramePacket(sampleFrame)
		_ = h
	}
}

// -------------------------------
func BenchmarkBinaryReadFrameHeader(b *testing.B) {
	var h packet.FramePacketHeader
	r := bytes.NewReader(sampleFrame)
	b.ResetTimer()
	for i := 0; i < b.N; i++ {
		r.Reset(sampleFrame) // simulate reading same bytes repeatedly
		if err := binary.Read(r, binary.LittleEndian, &h); err != nil {
			b.Fatal(err)
		}
	}
}

func BenchmarkManualFrameHeader(b *testing.B) {
	for i := 0; i < b.N; i++ {
		a := binary.LittleEndian.Uint32(sampleFrameHeader[0:4])
		_ = a
		bv := binary.LittleEndian.Uint32(sampleFrameHeader[4:8])
		_ = bv
		_ = binary.LittleEndian.Uint32(sampleFrameHeader[8:12])
		_ = binary.LittleEndian.Uint32(sampleFrameHeader[12:16])
		_ = binary.LittleEndian.Uint32(sampleFrameHeader[16:20])
	}
}

func BenchmarkUnsafeFrameHeader(b *testing.B) {
	for i := 0; i < b.N; i++ {
		h := *packet.BytesToFramePacketHeader(sampleFrameHeader)
		_ = h
	}
}

func BenchmarkGetTime(b *testing.B) {
	for i := 0; i < b.N; i++ {
		h := time.Now().UnixMilli()
		_ = h
	}
}
