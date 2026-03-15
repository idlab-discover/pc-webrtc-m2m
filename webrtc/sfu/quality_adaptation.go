package main

type QualityAdaptationsToDo struct {
	ToPlay  []*ReceiverTrack
	ToPause []*ReceiverTrack
}

func NewQualityAdaptationsToDo() *QualityAdaptationsToDo {
	return &QualityAdaptationsToDo{
		ToPlay:  make([]*ReceiverTrack, 0),
		ToPause: make([]*ReceiverTrack, 0),
	}
}

type QualityAdaptationOutput struct {
	EstimatedBandwidth uint32
	UsedBandwidth      uint32
	RemainingBandwidth uint32
	ChoicesString      []string
	Choices            []QualityChoice
}

type QualityChoice struct {
	ClientID          uint
	SelectedQuality   int
	MaxAllowedQuality int
	ScreenPosX        float32
	ScreenPosY        float32
	Distance          float32
	QualitiesBitrate  []uint64 // This will be removed later as the bitrates will be saved once for all clients
}

// TODO Fix this again so it works with remote providers again
type QualityAdaptation interface {
	PerformAdaptation(clc *ClientConnection, targetBitrate int, allClients map[uint]*ClientConnection, adaptationsToDo map[uint]*QualityAdaptationsToDo) *QualityAdaptationOutput
}
