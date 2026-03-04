package readers

import "metrics/core/logger"

const NameMetricReaderFactory = "MetricReaderFactory"

type MetricReaderSubFactory interface {
	CreateReader(metricClientId uint) MetricReaderBase
}

type MetricReaderFactory struct {
	webSocketSubFactory MetricReaderSubFactory
}

// Websocket -> dict with metricClientID/connection WebSocketReader -> When connected set ws and start listening
func NewMetricReaderFactory( /*TODO Maybe config*/ ) *MetricReaderFactory {
	logger.Log(NameMetricReaderFactory, logger.Creating, true, true)
	wsFactory := NewMetricReaderWebSocketSubFactory()
	ms := &MetricReaderFactory{
		webSocketSubFactory: wsFactory,
	}
	logger.Log(NameMetricReaderFactory, logger.Created, true, true)
	return ms
}

func (f *MetricReaderFactory) CreateNewMetricReader(connectionType string, metricClientId uint) MetricReaderBase {
	switch connectionType {
	case "websocket":
		return f.webSocketSubFactory.CreateReader(metricClientId)
	// case "local"
	// 	return NewLocalMetricReader()
	// case "webrtc":
	// 	return NewWebSocketMetricReader()
	default:
		return nil
	}
}
