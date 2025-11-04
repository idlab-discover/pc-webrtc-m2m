package metrics

type TrackMeter struct {
	PrevBytes uint64
	Bytes uint64
	Valid bool
}
