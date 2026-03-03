package readers

func CreateNewMetricReader(producerType string) MetricReaderBase {
	switch producerType {
	case "websocket":
		return NewWebSocketMetricReader()
	// case "webrtc":
	// 	return NewWebSocketMetricReader()
	default:
		return nil
	}
}
