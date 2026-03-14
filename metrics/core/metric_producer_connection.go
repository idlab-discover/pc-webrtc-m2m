package core

import (
	"fmt"
	"metrics/core/logger"
	"metrics/core/readers"
	"unsafe"
)

const NameMetricProducerConnection = "MetricProducerConnection"

type MetricProducerConnection struct {
	Server       *MetricsServer
	Id           uint32
	reader       readers.MetricReaderBase
	ProducerType string
	Metrics      map[uint32]*MetricValueCollection /*TODO this needs to be change to be unique per client*/
}

func NewMetricProducerConnection(server *MetricsServer, id uint32, producerType string, readerConnectionType string, factory *readers.MetricReaderFactory) *MetricProducerConnection {
	logger.LogWithMessage(NameMetricProducerConnection, logger.Creating, true, true, fmt.Sprintf("metricClientId=%d", id))
	pc := &MetricProducerConnection{
		Server:       server,
		Id:           id,
		ProducerType: producerType,
		Metrics:      make(map[uint32]*MetricValueCollection),
	}
	if readerConnectionType != "" && factory != nil {
		pc.reader = factory.CreateNewMetricReader(readerConnectionType, id)
		pc.reader.SetOnDataReceived(pc.OnDataReceived)
	}
	logger.LogWithMessage(NameMetricProducerConnection, logger.Created, true, true, fmt.Sprintf("metricClientId=%d", id))
	return pc
}

func (p *MetricProducerConnection) AddMetric(metricId uint32, definition *MetricDefinition) {
	println("Adding metric with id", metricId, "to producer", p.Id)
	p.Metrics[metricId] = NewMetricValueCollection(definition) // TODO: Make the size configurable
}

func (p *MetricProducerConnection) OnDataReceived(buffer []byte) {

	processedBytes := uint32(0)
	metricClientId := *(*uint32)(unsafe.Pointer(&buffer[processedBytes]))
	processedBytes += 4
	dataSize := *(*uint32)(unsafe.Pointer(&buffer[processedBytes]))
	processedBytes += 4
	println("OnDataReceived called for metricClientId", p.Id, " == ", metricClientId, "dataSize", dataSize, "bytes")
	for processedBytes < uint32(len(buffer)) {
		metricId := *(*uint32)(unsafe.Pointer(&buffer[processedBytes]))
		processedBytes += 4
		numValues := *(*int32)(unsafe.Pointer(&buffer[processedBytes]))
		println("Received data for metricId", metricId, "numValues", numValues)
		processedBytes += 4
		var col *MetricValueCollection
		var ok bool
		if col, ok = p.Metrics[metricId]; !ok {
			println("Received data for unknown metricId, ignoring", metricId, "offset", processedBytes)
			return
		}
		for i := int32(0); i < numValues; i++ {
			col.AddValue(buffer[processedBytes : processedBytes+(col.ValueSize+8)])
			processedBytes += col.ValueSize + 8
		}
	}
	/*TODO Maybe we also want to add a callback whenever a metric value is updated? For things like dashboards?*/
	p.Server.OnDataReceived(p, buffer)
}
