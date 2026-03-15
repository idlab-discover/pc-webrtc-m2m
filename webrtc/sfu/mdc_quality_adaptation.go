package main

import (
	"fmt"
	"math/rand/v2"
	"strconv"
	"strings"
)

type CompositeQualityChoiceMetric struct {
	ClientID           uint32
	EstimatedBandwidth uint32
	UsedBandwidth      uint32
	RemainingBandwidth uint32
	SelectedQuality    int32
	MaxAllowedQuality  int32
	Bitrate0           uint64
	Bitrate1           uint64
	Bitrate2           uint64
	Bitrate3           uint64
	Bitrate4           uint64
	Bitrate5           uint64
	Bitrate6           uint64

	ScreenPosX float32
	ScreenPosY float32
	Distance   float32
	PacketLoss float32
}

type MDCQualityAdaptation struct {
	nDescriptions        uint
	qualities            [][]uint
	descriptionToQuality [][]int
	highestQualityInBand []int

	comp *CompositeMetricDefinition[CompositeQualityChoiceMetric]
}

type MDCAdaptationClient struct {
	clientID         uint
	trackCounts      uint
	startingQuality  int
	selectedQuality  int
	screenPos        [2]float32
	distance         float32
	qualitiesBitrate []uint64
	tracks           []*MDCAdaptationClientTrack
	clientPtr        *ClientConnection
}

type MDCAdaptationClientTrack struct {
	trackID     string
	shouldPlay  bool
	bitrate     uint64
	webrtcTrack *ReceiverTrack
}

func NewMDCQualityAdaptation() *MDCQualityAdaptation {
	nDescriptions := uint(3)
	qualities := [][]uint{
		{0, 1, 2}, // 0
		{0, 1},    // 1
		{0},       // 2
		{1, 2},    // 3
		{1},       // 4
		{2},       // 5
		{},
	}
	descriptionToQuality := [][]int{
		{0, 1, 2},
		{0, 1, 3, 4},
		{0, 3, 5},
	}
	highestQualityInBand := []int{0, 3, 5, -1}
	return &MDCQualityAdaptation{
		nDescriptions:        nDescriptions,
		qualities:            qualities,
		descriptionToQuality: descriptionToQuality,
		highestQualityInBand: highestQualityInBand,
	}
}
func (mqa *MDCQualityAdaptation) PerformAdaptation(clc *ClientConnection, targetBitrate int, allClients map[uint]*ClientConnection, adaptationsToDo map[uint]*QualityAdaptationsToDo) *QualityAdaptationOutput {
	// Implement MDC quality adaptation logic here

	// Description MAX
	clients := map[uint]*MDCAdaptationClient{}

	for k := range clc.ReceiverVideoTracks {
		if clc.ReceiverVideoTracks[k].CorrespondingSenderTrack.trackMeter.Valid == false {
			continue
		}
		tokens := strings.Split(k, "_")
		clientID64, _ := strconv.ParseUint(tokens[0][2:], 10, 32)
		descriptionID64, _ := strconv.ParseUint(tokens[3], 10, 32)
		clientID := uint(clientID64)
		descriptionID := uint(descriptionID64)
		var client *ClientConnection
		if c, ok := allClients[clientID]; ok {
			client = c
		} else {
			continue
		}
		if _, ok := clients[clientID]; !ok {
			clients[clientID] = &MDCAdaptationClient{
				clientID:         clientID,
				trackCounts:      0,
				tracks:           make([]*MDCAdaptationClientTrack, mqa.nDescriptions),
				qualitiesBitrate: make([]uint64, len(mqa.qualities)),
				selectedQuality:  -1,
				clientPtr:        client,
			}
		}
		clients[clientID].tracks[descriptionID] = &MDCAdaptationClientTrack{
			trackID:     k,
			shouldPlay:  false,
			bitrate:     clc.ReceiverVideoTracks[k].CorrespondingSenderTrack.trackMeter.PrevBytes * 8,
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
		client.startingQuality, client.screenPos = mqa.calculateStartingQuality(clc, client.clientPtr)
		client.selectedQuality = client.startingQuality
	}
	// TODO
	//			Sort clients based on starting quality
	//			If there isnt enough bitrate reduce quality of clients with higher starting quality first
	//			Limit to max 1 quality level decrease at the same time
	//		    Clients that have the lowest quality level should only be removed in worst case scenarios (as it will cause nothing to be rendered for this clients)
	keys := make([]uint, 0, len(clients))
	for k := range clients {
		keys = append(keys, k)
	}
	rand.Shuffle(len(keys), func(i, j int) {
		keys[i], keys[j] = keys[j], keys[i]
	})
	for i := 1; i < len(mqa.qualities); i++ {
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
	choices := make([]QualityChoice, 0)
	for _, client := range clients {
		if client.selectedQuality > -1 {
			for _, desc := range mqa.qualities[client.selectedQuality] {
				client.tracks[desc].shouldPlay = true
			}
		}
		choices = append(choices, QualityChoice{
			ClientID:          client.clientID,
			ScreenPosX:        client.screenPos[0],
			ScreenPosY:        client.screenPos[1],
			Distance:          client.distance,
			SelectedQuality:   client.selectedQuality,
			MaxAllowedQuality: client.startingQuality,
			QualitiesBitrate:  client.qualitiesBitrate,
		})
		for _, track := range client.tracks {
			// TODO Maybe move this outside and do this once globally?
			adaptationsForClient, exists := adaptationsToDo[client.clientID]
			if !exists {
				adaptationsForClient = NewQualityAdaptationsToDo()
				adaptationsToDo[client.clientID] = adaptationsForClient
			}
			if track.shouldPlay {
				adaptationsForClient.ToPlay = append(adaptationsForClient.ToPlay, track.webrtcTrack)
				adaptations = append(adaptations, track.trackID)
			} else {
				adaptationsForClient.ToPause = append(adaptationsForClient.ToPause, track.webrtcTrack)
			}
		}
	}

	return &QualityAdaptationOutput{
		EstimatedBandwidth: uint32(targetBitrate),
		UsedBandwidth:      uint32(totalUsedBitrate),
		RemainingBandwidth: uint32(targetBitrate) - uint32(totalUsedBitrate),
		ChoicesString:      adaptations,
		Choices:            choices,
	}
}

func (mqa *MDCQualityAdaptation) calculateStartingQuality(client *ClientConnection, otherClient *ClientConnection) (int, [2]float32) {
	activeBand, screenPos := mqa.calculateActiveBand(client, otherClient)
	if activeBand < uint(len(mqa.highestQualityInBand)) {
		return mqa.highestQualityInBand[activeBand], screenPos
	}
	return -1, screenPos
}

func (mqa *MDCQualityAdaptation) calculateActiveBand(client *ClientConnection, otherClient *ClientConnection) (uint, [2]float32) {
	nBands := uint(3)
	camSpace := MultiplyPoint(client.PositionMatrix.WorldToCameraMatrix, otherClient.PositionMatrix.Position)
	clipSpace := ConvertToClipspace(client.PositionMatrix.ProjectionMatrix, camSpace)
	ndcSpace := [3]float32{
		clipSpace[0] / clipSpace[3],
		clipSpace[1] / clipSpace[3],
		clipSpace[2] / clipSpace[3],
	}
	fmt.Printf("WebRTCSFU: calculatePointVisibility: Pos x %f y %f z %f\n", ndcSpace[0], ndcSpace[1], ndcSpace[2])

	bandSpacing := 1.0 / float32(nBands) * 1.0
	for i := uint(0); i < nBands; i++ {
		if (ndcSpace[0] >= 0-bandSpacing*float32(i+1)) && (ndcSpace[0] <= 0+bandSpacing*float32(i+1)) {
			return i, [2]float32{ndcSpace[0], ndcSpace[1]}
		}
	}
	if ndcSpace[0] >= -1.25 && ndcSpace[0] <= 1.25 {
		return nBands - 1, [2]float32{ndcSpace[0], ndcSpace[1]}
	}

	return nBands, [2]float32{ndcSpace[0], ndcSpace[1]}
}
