package core

import (
	"metrics/core/logger"
	"metrics/core/readers"
	"unsafe"
)

const NameMetricProducerConnection = "MetricProducerConnection"

type MetricProducerConnection struct {
	Server       *MetricsServer
	Id           uint
	reader       readers.MetricReaderBase
	ProducerType string
	Metrics      map[uint]*MetricValueCollection /*TODO this needs to be change to be unique per client*/
}

func NewMetricProducerConnection(server *MetricsServer, id uint, producerType string, readerConnectionType string, factory *readers.MetricReaderFactory) *MetricProducerConnection {
	logger.Log(NameMetricProducerConnection, logger.Creating, true, true)
	pc := &MetricProducerConnection{
		Server:       server,
		Id:           id,
		ProducerType: producerType,
		reader:       factory.CreateNewMetricReader(readerConnectionType, id),
		Metrics:      make(map[uint]*MetricValueCollection),
	}
	pc.reader.SetOnDataReceived(pc.OnDataReceived)
	logger.Log(NameMetricProducerConnection, logger.Created, true, true)
	return pc
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
