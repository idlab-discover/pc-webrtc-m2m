package main

import (
	"fmt"
	"goweb/shared/src/logger"
)

const NameQualityAdaptationFactory = "QualityAdaptationFactory"

func CreateQualityAdaptation(adaptationType string) QualityAdaptation {
	logger.LogWithMessage(NameQualityAdaptationFactory, logger.Creating, true, true, fmt.Sprintf("adaptationType=%s", adaptationType))
	var adaptation QualityAdaptation
	switch adaptationType {
	case "random":
		adaptation = NewRandomQualityAdaptation()
	case "mdc":
		adaptation = NewMDCQualityAdaptation()
	case "mdc_random":
		adaptation = NewMDCRandomQualityAdaptation()
	default:
		adaptation = nil
	}
	if adaptation == nil {
		logger.LogWithMessage(NameQualityAdaptationFactory, logger.Failed, true, true, fmt.Sprintf("adaptationType=%s", adaptationType))
	} else {
		logger.LogWithMessage(NameQualityAdaptationFactory, logger.Created, true, true, fmt.Sprintf("adaptationType=%s", adaptationType))
	}
	return adaptation
}
