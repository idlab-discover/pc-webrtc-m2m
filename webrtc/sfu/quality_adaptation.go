package main

type QualityAdaptation interface {
	PerformAdaptation(clc *ClientConnection, targetBitrate int, allClients map[uint]*ClientConnection) []string
}
