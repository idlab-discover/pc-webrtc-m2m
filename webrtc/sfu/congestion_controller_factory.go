package main

import (
	"encoding/json"
	"reflect"
	"unsafe"

	"github.com/pion/interceptor/pkg/cc"
	"github.com/pion/interceptor/pkg/gcc"
)

type GCCSettings struct {
	MinBitrate     int `json:"minBitrate"`
	MaxBitrate     int `json:"maxBitrate"`
	InitialBitrate int `json:"initialBitrate"`
}

func CreateBandwidthEstimator(clc *ClientConnection, sfuSettings *SFUSettings) (*cc.InterceptorFactory, error) {
	var gccSettings GCCSettings // This marshalling should probably only happen once!
	if err := json.Unmarshal(sfuSettings.CCSettings, &gccSettings); err != nil {
		return nil, err
	}
	congestionController, err := cc.NewInterceptor(func() (cc.BandwidthEstimator, error) {
		bwEst, err := gcc.NewSendSideBWE(gcc.SendSideBWEMinBitrate(gccSettings.MinBitrate), gcc.SendSideBWEInitialBitrate(gccSettings.InitialBitrate), gcc.SendSideBWEMaxBitrate(gccSettings.MaxBitrate))
		clc.BandwidthEstimator = bwEst
		return bwEst, err
	})
	if err != nil {
		panic(err)
	}
	congestionController.OnNewPeerConnection(func(id string, estimator cc.BandwidthEstimator) {
		pointerVal := reflect.ValueOf(estimator)
		val := reflect.Indirect(pointerVal)

		lossControllerFieldPtr := val.FieldByName("lossController")
		lossControllerField := reflect.Indirect((lossControllerFieldPtr))

		minBitrateField := lossControllerField.FieldByName("minBitrate")
		ptrToMin := unsafe.Pointer(minBitrateField.UnsafeAddr())
		actualMinPtr := (*int)(ptrToMin)
		*actualMinPtr = gccSettings.MinBitrate

		maxBitrateField := lossControllerField.FieldByName("maxBitrate")
		ptrToMax := unsafe.Pointer(maxBitrateField.UnsafeAddr())
		actualMaxPtr := (*int)(ptrToMax)
		*actualMaxPtr = gccSettings.MaxBitrate

		clc.BandwidthEstimator = estimator
	})
	return congestionController, nil
}
