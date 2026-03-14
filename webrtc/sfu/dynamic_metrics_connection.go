package main

import (
	"encoding/binary"
	"fmt"
	"metrics/core"
	"sync"
)

// DynamicMetricsConnection mirrors the Unity metric registration/update flow,
// but writes directly into the local Go metrics producer.
type DynamicMetricsConnection struct {
	server         *core.MetricsServer
	provider       *core.MetricProducerConnection
	metricByName   map[string]metricDefinition
	registeredByID map[uint32]metricDefinition
	mut            sync.Mutex
}

func NewDynamicMetricsConnection(server *core.MetricsServer, provider *core.MetricProducerConnection) *DynamicMetricsConnection {
	return &DynamicMetricsConnection{
		server:         server,
		provider:       provider,
		metricByName:   make(map[string]metricDefinition),
		registeredByID: make(map[uint32]metricDefinition),
	}
}

func RegisterPushMetric[T any](c *DynamicMetricsConnection, metricName string) (*GenericMetricDefinition[T], error) {
	c.mut.Lock()
	defer c.mut.Unlock()
	if existing, ok := c.metricByName[metricName]; ok {
		typed, ok := existing.(*GenericMetricDefinition[T])
		if !ok {
			return nil, fmt.Errorf("metric %q already exists with a different type", metricName)
		}
		return typed, nil
	}
	metric, err := NewGenericMetricDefinition[T](metricName)
	if err != nil {
		return nil, err
	}
	if err := c.registerMetricWithProducer(metric); err != nil {
		return nil, err
	}
	return metric, nil
}

func RegisterCompositePushMetric[T any](c *DynamicMetricsConnection, metricName string) (*CompositeMetricDefinition[T], error) {
	c.mut.Lock()
	defer c.mut.Unlock()
	if existing, ok := c.metricByName[metricName]; ok {
		typed, ok := existing.(*CompositeMetricDefinition[T])
		if !ok {
			return nil, fmt.Errorf("metric %q already exists with a different type", metricName)
		}
		return typed, nil
	}
	metric, err := NewCompositeMetricDefinition[T](metricName)
	if err != nil {
		return nil, err
	}
	if err := c.registerMetricWithProducer(metric); err != nil {
		return nil, err
	}
	return metric, nil
}

func RegisterPullMetric[T any](c *DynamicMetricsConnection, metricName string, cb func() T) (uint32, *GenericMetricDefinition[T], error) {
	metric, err := RegisterPushMetric[T](c, metricName)
	if err != nil {
		return 0, nil, err
	}
	cbID := metric.AddCallback(cb)
	return cbID, metric, nil
}

func RegisterCompositePullMetric[T any](c *DynamicMetricsConnection, metricName string, cb func() T) (uint32, *CompositeMetricDefinition[T], error) {
	metric, err := RegisterCompositePushMetric[T](c, metricName)
	if err != nil {
		return 0, nil, err
	}
	cbID := metric.AddCallback(cb)
	return cbID, metric, nil
}

func (c *DynamicMetricsConnection) registerMetricWithProducer(def metricDefinition) error {
	if c.server == nil {
		return fmt.Errorf("metrics provider is nil")
	}
	metricId := c.server.AddMetricDefinitionLocal(c.provider, def.Name(), def.Header(), def.IsComposite())
	def.SetMetricID(metricId)
	c.metricByName[def.Name()] = def
	c.registeredByID[def.MetricID()] = def
	return nil
}

func (c *DynamicMetricsConnection) UpdateMetrics() error {
	c.mut.Lock()
	defer c.mut.Unlock()
	if c.provider == nil {
		return fmt.Errorf("metrics provider is nil")
	}
	packetSize := 0 // producer client id
	metricData := make(map[uint32][]encodedMetricValue)
	for _, def := range c.metricByName {
		values, err := def.RetrieveValues()
		if err != nil {
			return err
		}
		if len(values) == 0 {
			continue
		}
		metricData[def.MetricID()] = values
		packetSize += 8 // metricID + count
		for _, value := range values {
			packetSize += value.totalSize()
		}
	}
	if len(metricData) == 0 {
		return nil
	}
	dataSize := packetSize
	packetSize += 8 // provider client id and data size
	buffer := make([]byte, packetSize)
	offset := 0
	binary.LittleEndian.PutUint32(buffer[offset:], c.provider.Id)
	offset += 4
	binary.LittleEndian.PutUint32(buffer[offset:], uint32(dataSize))
	offset += 4
	for metricID, values := range metricData {
		binary.LittleEndian.PutUint32(buffer[offset:], metricID)
		offset += 4
		binary.LittleEndian.PutUint32(buffer[offset:], uint32(len(values)))
		offset += 4
		for _, value := range values {
			binary.LittleEndian.PutUint64(buffer[offset:], uint64(value.timestamp))
			offset += 8
			copy(buffer[offset:], value.payload)
			offset += len(value.payload)
		}
	}
	for _, def := range c.metricByName {
		def.ClearValues()
	}
	c.provider.OnDataReceived(buffer)
	return nil
}
