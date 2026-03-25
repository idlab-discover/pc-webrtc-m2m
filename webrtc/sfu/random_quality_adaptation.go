package main

import "math/rand/v2"

type RandomQualityAdaptation struct {
}

func NewRandomQualityAdaptation() *RandomQualityAdaptation {
	return &RandomQualityAdaptation{}
}
func (rqa *RandomQualityAdaptation) PerformAdaptation(clc *ClientConnection, targetBitrate int, allClients map[uint]*ClientConnection, adaptationsToDo map[uint]*QualityAdaptationsToDo) *QualityAdaptationOutput {
	// Implement random quality adaptation logic here
	//targetBitrate = int(float64(targetBitrate) * 0.85)
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
		client := receiverTrack.CorrespondingSenderTrack.Parent
		adaptationsForClient, exists := adaptationsToDo[client.clientID]
		senderTrack := receiverTrack.CorrespondingSenderTrack
		if senderTrack.trackMeter.Valid == false {
			continue
		}
		if !exists {
			adaptationsForClient = NewQualityAdaptationsToDo()
			adaptationsToDo[client.clientID] = adaptationsForClient
		}
		if targetBitrate-int(senderTrack.trackMeter.PrevBytes*8) > 0 {
			adaptationsForClient.ToPlay = append(adaptationsForClient.ToPlay, receiverTrack)
			adaptations = append(adaptations, receiverTrack.TrackID)
			targetBitrate -= int(senderTrack.trackMeter.PrevBytes * 8)
		} else {
			adaptationsForClient.ToPause = append(adaptationsForClient.ToPause, receiverTrack)
		}
	}

	return &QualityAdaptationOutput{
		EstimatedBandwidth: uint32(targetBitrate),
		RemainingBandwidth: uint32(targetBitrate),
		ChoicesString:      adaptations,
	}
}
