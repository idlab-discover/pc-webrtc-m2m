package main

import (
	"math/rand/v2"
	"strconv"
	"strings"
)

type MDCQualityAdaptation struct {
	nDescriptions uint
	qualities [][]uint
	descriptionToQuality [][]int
}

type MDCAdaptationClient struct {
	clientID uint
	trackCounts uint
	selectedQuality int
	qualitiesBitrate []uint64
	tracks []*MDCAdaptationClientTrack
}

type MDCAdaptationClientTrack struct {
	trackID string
	shouldPlay bool
	bitrate uint64
	webrtcTrack *ReceiverTrack
}

func NewMDCQualityAdaptation() *MDCQualityAdaptation {
	nDescriptions := uint(3)
	qualities := [][]uint{
		{0, 1, 2}, 	// 0
		{0, 1}, 	// 1
		{0},	    // 2
		{1, 2},     // 3
		{1},        // 4
		{2},        // 5
		{},
	}
	descriptionToQuality := [][]int{
		{0, 1, 2},
		{0, 1, 3, 4},
		{0, 3, 5},
	}
	return &MDCQualityAdaptation{
		nDescriptions: nDescriptions,
		qualities:     qualities,
		descriptionToQuality: descriptionToQuality,
	}
}
func (mqa *MDCQualityAdaptation) PerformAdaptation(clc *ClientConnection, targetBitrate int) []string {
	// Implement MDC quality adaptation logic here

	// Description MAX
	clients := map[uint]*MDCAdaptationClient{}
	for k := range clc.ReceiverVideoTracks {
		if clc.ReceiverVideoTracks[k].CorrespondingSenderTrack.trackMeter.Valid == false {
			continue
		}
		tokens := strings.Split(k, "_")
		clientID64, _ := strconv.ParseUint(tokens[0][2:], 10, 32)
		descriptionID64, _ := strconv.ParseUint(tokens[4], 10, 32)
		clientID := uint(clientID64)
		descriptionID := uint(descriptionID64)
		if _, ok := clients[clientID]; !ok {
			clients[clientID] = &MDCAdaptationClient{
				clientID:    clientID,
				trackCounts: 0,
				tracks:      make([]*MDCAdaptationClientTrack, mqa.nDescriptions),
				qualitiesBitrate: make([]uint64, len(mqa.qualities)),
				selectedQuality: -1,
			}
		}
		clients[clientID].tracks[descriptionID] = &MDCAdaptationClientTrack{
			trackID:    k,
			shouldPlay: false,
			bitrate: clc.ReceiverVideoTracks[k].CorrespondingSenderTrack.trackMeter.PrevBytes * 8,
			webrtcTrack: clc.ReceiverVideoTracks[k],
		}

		for _, qual := range mqa.descriptionToQuality[descriptionID] {
			clients[clientID].qualitiesBitrate[qual] += clc.ReceiverVideoTracks[k].CorrespondingSenderTrack.trackMeter.PrevBytes * 8
		}

		clients[clientID].trackCounts++
	}
	
	var adaptations []string
	totalUsedBitrate := uint64(0)
	for _, client := range clients {
		if client.trackCounts != mqa.nDescriptions {
			continue
		}
		totalUsedBitrate += client.qualitiesBitrate[0]
		client.selectedQuality = 0
	}
	keys := make([]uint, 0, len(clients))
	for k := range clients {
		keys = append(keys, k)
	}
	rand.Shuffle(len(keys), func(i, j int) {
		keys[i], keys[j] = keys[j], keys[i]
	})
	for i := range mqa.qualities[1:] {
		if totalUsedBitrate < uint64(targetBitrate) {
			break
		}
		for _, k := range keys {
			client := clients[k]
			if client.trackCounts != mqa.nDescriptions {
				continue
			}
			if totalUsedBitrate < uint64(targetBitrate) {
				break
			}
			totalUsedBitrate -= client.qualitiesBitrate[client.selectedQuality]
			client.selectedQuality = i
			totalUsedBitrate += client.qualitiesBitrate[client.selectedQuality]
		}
	}
	for _, client := range clients {
		for _, desc := range mqa.qualities[client.selectedQuality] {
			client.tracks[desc].shouldPlay = true
		}
		for _, track := range client.tracks {
			if track.shouldPlay {
				track.webrtcTrack.Play()
				adaptations = append(adaptations, track.trackID)
			} else {
				track.webrtcTrack.Pause()
			}
		}
	}
	
	return adaptations
}
