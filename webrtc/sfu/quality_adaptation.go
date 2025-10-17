package main

type QualityAdaptation interface {
	PerformAdaptation(clc *ClientConnection, targetBitrate int) []string
}
