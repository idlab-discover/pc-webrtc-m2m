package metrics

import (
	"fmt"
	"goweb/shared/src/logger"
	"sync"
	"sync/atomic"
	"time"
)

type OverallTrackMetrics struct {
	bitrateMeters map[string]*TrackMeter
	mut           sync.Mutex
}

func NewOverallTrackMetrics() *OverallTrackMetrics {
	return &OverallTrackMetrics{
		bitrateMeters: make(map[string]*TrackMeter),
		mut:           sync.Mutex{},
	}
}

func (o *OverallTrackMetrics) AddTrackMeter(trackID string) *TrackMeter {
	o.mut.Lock()
	defer o.mut.Unlock()
	t := &TrackMeter{}
	o.bitrateMeters[trackID] = t
	return t
}

func (o *OverallTrackMetrics) StartMeasuring() {
	go func() {
		ticker := time.NewTicker(1 * time.Second)
		defer ticker.Stop()
		for {
			<-ticker.C
			o.mut.Lock()
			output := fmt.Sprintf("ts=%d stats=[", time.Now().UnixMilli())
			for trackID, meter := range o.bitrateMeters {
				output += fmt.Sprintf("trackID=%s@bitrate=%dbps;", trackID, meter.Bytes*8)
				if(meter.Bytes == 0){
					meter.Valid = false
					meter.WasValidPrevious = false
				} else {
					if meter.WasValidPrevious {
						meter.Valid = true
					}
					meter.WasValidPrevious = true
				}
				atomic.StoreUint64(&meter.PrevBytes, meter.Bytes)
				atomic.SwapUint64(&meter.Bytes, 0)
			}
			output += "]"
			logger.LogWithMessage("OverallTrackMetrics", logger.TrackMetricsReport, true, true, output)
			o.mut.Unlock()
		}
	}()
}
