package main

import (
	"encoding/binary"
	"math"
	"metrics/core"
	metricsLogger "metrics/core/logger"
	"reflect"
	"sync"
	"testing"
)

var metricsLoggerInitOnce sync.Once

func newTestProducer(id uint32) *core.MetricProducerConnection {
	metricsLoggerInitOnce.Do(func() {
		metricsLogger.LogInit("test", "test", "")
	})
	return &core.MetricProducerConnection{
		Server:  &core.MetricsServer{},
		Id:      id,
		Metrics: make(map[uint32]*core.MetricValueCollection),
	}
}

func TestBuildGenericHeaderForType_Int32(t *testing.T) {
	header, size, _, err := buildGenericHeaderForType(reflect.TypeFor[int32]())
	if err != nil {
		t.Fatalf("buildGenericHeaderForType failed: %v", err)
	}
	if len(header) != 5 {
		t.Fatalf("expected header length 5, got %d", len(header))
	}
	if MetricValueType(header[0]) != MetricTypeSignedNumeric {
		t.Fatalf("expected signed numeric type header, got %d", header[0])
	}
	if got := binary.LittleEndian.Uint32(header[1:]); got != 4 {
		t.Fatalf("expected size 4 in header, got %d", got)
	}
	if size != 4 {
		t.Fatalf("expected size 4, got %d", size)
	}
}

func TestGenericMetricDefinition_AddAndRetrieve(t *testing.T) {
	def, err := NewGenericMetricDefinition[int32](7, "int_metric")
	if err != nil {
		t.Fatalf("NewGenericMetricDefinition failed: %v", err)
	}
	if err := def.AddCapturedValue(42); err != nil {
		t.Fatalf("AddCapturedValue failed: %v", err)
	}
	def.AddCallback(func() int32 { return -3 })

	values, err := def.RetrieveValues()
	if err != nil {
		t.Fatalf("RetrieveValues failed: %v", err)
	}
	if len(values) != 2 {
		t.Fatalf("expected 2 values (1 push + 1 callback), got %d", len(values))
	}

	first := int32(binary.LittleEndian.Uint32(values[0].payload))
	second := int32(binary.LittleEndian.Uint32(values[1].payload))
	if first != 42 {
		t.Fatalf("expected first value 42, got %d", first)
	}
	if second != -3 {
		t.Fatalf("expected second value -3, got %d", second)
	}

	def.ClearValues()
	values, err = def.RetrieveValues()
	if err != nil {
		t.Fatalf("RetrieveValues after clear failed: %v", err)
	}
	if len(values) != 1 {
		t.Fatalf("expected only callback value after clear, got %d", len(values))
	}
}

type compositeSample struct {
	A int32
	B uint32
	C float32
}

func TestCompositeMetricDefinition_EncodeOrderAndHeader(t *testing.T) {
	def, err := NewCompositeMetricDefinition[compositeSample](3, "composite")
	if err != nil {
		t.Fatalf("NewCompositeMetricDefinition failed: %v", err)
	}
	if !def.IsComposite() {
		t.Fatalf("expected composite definition")
	}
	if len(def.Header()) == 0 {
		t.Fatalf("expected non-empty composite header")
	}

	input := compositeSample{A: -10, B: 15, C: 1.25}
	if err := def.AddCapturedValue(input); err != nil {
		t.Fatalf("AddCapturedValue failed: %v", err)
	}

	values, err := def.RetrieveValues()
	if err != nil {
		t.Fatalf("RetrieveValues failed: %v", err)
	}
	if len(values) != 1 {
		t.Fatalf("expected 1 composite value, got %d", len(values))
	}

	payload := values[0].payload
	if got := int32(binary.LittleEndian.Uint32(payload[0:4])); got != -10 {
		t.Fatalf("field A mismatch: got %d", got)
	}
	if got := binary.LittleEndian.Uint32(payload[4:8]); got != 15 {
		t.Fatalf("field B mismatch: got %d", got)
	}
	if got := math.Float32frombits(binary.LittleEndian.Uint32(payload[8:12])); got != float32(1.25) {
		t.Fatalf("field C mismatch: got %f", got)
	}
}

func TestDynamicMetricsConnection_UpdateMetricsWritesToProducer(t *testing.T) {
	producer := newTestProducer(99)
	conn := NewDynamicMetricsConnection(producer)

	metric, err := RegisterPushMetric[int32](conn, "latency_ms")
	if err != nil {
		t.Fatalf("RegisterPushMetric failed: %v", err)
	}
	collection, ok := producer.Metrics[0]
	if !ok {
		t.Fatalf("expected metric collection to exist after registration")
	}
	collection.SaveInMemory = true

	if err := metric.AddCapturedValue(123); err != nil {
		t.Fatalf("AddCapturedValue failed: %v", err)
	}

	if err := conn.UpdateMetrics(); err != nil {
		t.Fatalf("UpdateMetrics failed: %v", err)
	}

	if collection.NCurrentValues != 1 {
		t.Fatalf("expected NCurrentValues=1, got %d", collection.NCurrentValues)
	}

	valueBytes := collection.ValueBuffer[:collection.ValueSize+8]
	encoded := int32(binary.LittleEndian.Uint32(valueBytes[8:12]))
	if encoded != 123 {
		t.Fatalf("expected encoded value 123, got %d", encoded)
	}

	remaining, err := metric.RetrieveValues()
	if err != nil {
		t.Fatalf("RetrieveValues after update failed: %v", err)
	}
	if len(remaining) != 0 {
		t.Fatalf("expected captured values to be cleared after update, got %d", len(remaining))
	}
}
