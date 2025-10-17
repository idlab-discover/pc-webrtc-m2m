package main

import "math/rand/v2"

type RandomQualityAdaptation struct {
}

func NewRandomQualityAdaptation() *RandomQualityAdaptation {
	return &RandomQualityAdaptation{}
}
func (rqa *RandomQualityAdaptation) PerformAdaptation(clc *ClientConnection, targetBitrate int) []string {
	// Implement random quality adaptation logic here
	keys := make([]string, 0, len(clc.ReceiverVideoTracks))
	for k := range clc.ReceiverVideoTracks {
		keys = append(keys, k)
	}
	rand.Shuffle(len(keys), func(i, j int) {
		keys[i], keys[j] = keys[j], keys[i]
	})
	var adaptations []string
	for _, k := range keys {
		receiverTrack := clc.ReceiverVideoTracks[k]
		senderTrack := receiverTrack.CorrespondingSenderTrack
		if targetBitrate-int(senderTrack.trackMeter.Bytes*8) > 0 {
			receiverTrack.Play()
			adaptations = append(adaptations, k)
			targetBitrate -= int(senderTrack.trackMeter.Bytes * 8)
		} else {
			receiverTrack.Pause()
		}

	}
	// For demonstration, we just return an empty list
	return adaptations
}
