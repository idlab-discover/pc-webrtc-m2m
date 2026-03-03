package metrics

import (
	"metrics/readers"
	"unsafe"
)

type MetricProducerConnection struct {
	Server       *MetricsServer
	Id           uint
	reader       readers.MetricReaderBase
	ProducerType string
	Metrics      map[uint]*MetricValueCollection /*TODO this needs to be change to be unique per client*/
}

func NewMetricProducerConnection(server *MetricsServer, id uint, producerType string) *MetricProducerConnection {
	return &MetricProducerConnection{
		Server:       server,
		Id:           id,
		reader:       readers.CreateNewMetricReader(producerType),
		ProducerType: producerType,
		Metrics:      make(map[uint]*MetricValueCollection),
	}
}

func (p *MetricProducerConnection) AddMetric(metricId uint, definition *MetricDefinition) {
	p.Metrics[metricId] = NewMetricValueCollection(definition) // TODO: Make the size configurable
}

func (p *MetricProducerConnection) OnDataReceived(buffer []byte) {
	processedBytes := uint(0)
	for processedBytes < uint(len(buffer)) {
		metricId := *(*uint)(unsafe.Pointer(&buffer[0]))
		processedBytes += 4
		numValues := *(*int)(unsafe.Pointer(&buffer[processedBytes]))
		processedBytes += 4
		var col *MetricValueCollection
		var ok bool
		if col, ok = p.Metrics[metricId]; !ok {
			return
		}
		for i := 0; i < numValues; i++ {
			col.AddValue(buffer[processedBytes:(col.ValueSize + 8)])
			processedBytes += col.ValueSize + 8
		}
	}
	/*TODO Maybe we also want to add a callback whenever a metric value is updated? For things like dashboards?*/
	p.Server.OnDataReceived(p, buffer)
}
