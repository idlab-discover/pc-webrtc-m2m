package readers

type MetricReaderBase interface {
	Connect(onDataReceived func([]byte))
}
