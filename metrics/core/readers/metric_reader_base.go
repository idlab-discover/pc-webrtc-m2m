package readers

type MetricReaderBase interface {
	SetOnDataReceived(onDataReceived func([]byte))
	GetConnectionAddress() string
}
