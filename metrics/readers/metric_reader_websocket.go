package readers

type MetricReaderWebSocket struct {
	onDataReceived func([]byte)
}

func NewWebSocketMetricReader() *MetricReaderWebSocket {
	return &MetricReaderWebSocket{}
}

func (r *MetricReaderWebSocket) Connect(onDataReceived func([]byte)) {
	r.onDataReceived = onDataReceived
}
