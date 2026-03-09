package main

import (
	"encoding/binary"
	"fmt"
	"reflect"
	"sync"
	"time"
)

type encodedMetricValue struct {
	timestamp int64
	payload   []byte
}

func (v encodedMetricValue) totalSize() int {
	return 8 + len(v.payload)
}

type metricDefinition interface {
	MetricID() uint32
	Name() string
	Header() []byte
	IsComposite() bool
	SetMetricID(id uint32)
	RetrieveValues() ([]encodedMetricValue, error)
	ClearValues()
}

type baseMetricDefinition struct {
	metricID       uint32
	name           string
	header         []byte
	composite      bool
	capturedValues []encodedMetricValue
	mut            sync.Mutex
}

func (d *baseMetricDefinition) SetMetricID(id uint32) {
	d.metricID = id
}

func (d *baseMetricDefinition) MetricID() uint32 {
	return d.metricID
}

func (d *baseMetricDefinition) Name() string {
	return d.name
}

func (d *baseMetricDefinition) Header() []byte {
	return d.header
}

func (d *baseMetricDefinition) IsComposite() bool {
	return d.composite
}

func (d *baseMetricDefinition) ClearValues() {
	d.mut.Lock()
	defer d.mut.Unlock()
	d.capturedValues = d.capturedValues[:0]
}

type GenericMetricDefinition[T any] struct {
	baseMetricDefinition
	converter scalarConverter
	callbacks map[uint32]func() T
	nextCBID  uint32
}

func NewGenericMetricDefinition[T any](name string) (*GenericMetricDefinition[T], error) {
	var sample T
	header, _, converter, err := buildGenericHeaderForType(reflect.TypeOf(sample))
	if err != nil {
		return nil, err
	}
	return &GenericMetricDefinition[T]{
		baseMetricDefinition: baseMetricDefinition{name: name, header: header, composite: false},
		converter:            converter,
		callbacks:            make(map[uint32]func() T),
	}, nil
}

func (d *GenericMetricDefinition[T]) SetMetricID(id uint32) {
	d.metricID = id
}

func (d *GenericMetricDefinition[T]) AddCapturedValue(value T) error {
	payload := make([]byte, d.converter.size)
	if _, err := d.converter.encode(value, payload); err != nil {
		return err
	}
	d.mut.Lock()
	d.capturedValues = append(d.capturedValues, encodedMetricValue{timestamp: time.Now().UnixMilli(), payload: payload})
	d.mut.Unlock()
	return nil
}

func (d *GenericMetricDefinition[T]) AddCallback(cb func() T) uint32 {
	d.mut.Lock()
	defer d.mut.Unlock()
	id := d.nextCBID
	d.callbacks[id] = cb
	d.nextCBID++
	return id
}

func (d *GenericMetricDefinition[T]) RetrieveValues() ([]encodedMetricValue, error) {
	d.mut.Lock()
	defer d.mut.Unlock()
	for _, cb := range d.callbacks {
		value := cb()
		payload := make([]byte, d.converter.size)
		if _, err := d.converter.encode(value, payload); err != nil {
			return nil, err
		}
		d.capturedValues = append(d.capturedValues, encodedMetricValue{timestamp: time.Now().UnixMilli(), payload: payload})
	}
	values := make([]encodedMetricValue, len(d.capturedValues))
	copy(values, d.capturedValues)
	return values, nil
}

type compositeFieldEncoder struct {
	name      string
	converter scalarConverter
	getter    func(v reflect.Value) any
}

type CompositeMetricDefinition[T any] struct {
	baseMetricDefinition
	fieldEncoders []compositeFieldEncoder
	payloadSize   int
	callbacks     map[uint32]func() T
	nextCBID      uint32
}

func NewCompositeMetricDefinition[T any](name string) (*CompositeMetricDefinition[T], error) {
	t := reflect.TypeFor[T]()
	if t.Kind() != reflect.Struct {
		return nil, fmt.Errorf("composite metric %q requires a struct type, got %s", name, t.String())
	}
	header := make([]byte, 0)
	fieldEncoders := make([]compositeFieldEncoder, 0, t.NumField())
	totalPayloadSize := 0
	for i := 0; i < t.NumField(); i++ {
		f := t.Field(i)
		if !f.IsExported() {
			continue
		}
		converter, ok := lookupScalarConverter(f.Type)
		if !ok {
			return nil, fmt.Errorf("unsupported field type %s for composite metric %q field %q", f.Type.String(), name, f.Name)
		}
		fieldNameBytes := []byte(f.Name)
		nameLen := make([]byte, 4)
		binary.LittleEndian.PutUint32(nameLen, uint32(len(fieldNameBytes)))
		header = append(header, nameLen...)
		header = append(header, fieldNameBytes...)
		header = append(header, byte(converter.metricType))
		sizeBytes := make([]byte, 4)
		binary.LittleEndian.PutUint32(sizeBytes, converter.size)
		header = append(header, sizeBytes...)
		fieldIndex := i
		fieldEncoders = append(fieldEncoders, compositeFieldEncoder{
			name:      f.Name,
			converter: converter,
			getter: func(v reflect.Value) any {
				return v.Field(fieldIndex).Interface()
			},
		})
		totalPayloadSize += int(converter.size)
	}
	if len(fieldEncoders) == 0 {
		return nil, fmt.Errorf("composite metric %q has no exported fields", name)
	}
	return &CompositeMetricDefinition[T]{
		baseMetricDefinition: baseMetricDefinition{name: name, header: header, composite: true},
		fieldEncoders:        fieldEncoders,
		payloadSize:          totalPayloadSize,
		callbacks:            make(map[uint32]func() T),
	}, nil
}

func (d *CompositeMetricDefinition[T]) SetMetricID(id uint32) {
	d.metricID = id
}

func (d *CompositeMetricDefinition[T]) encodeCompositeValue(value T) ([]byte, error) {
	payload := make([]byte, d.payloadSize)
	rv := reflect.ValueOf(value)
	if rv.Kind() == reflect.Pointer {
		rv = rv.Elem()
	}
	if !rv.IsValid() || rv.Kind() != reflect.Struct {
		return nil, fmt.Errorf("expected struct value, got %T", value)
	}
	offset := 0
	for _, fe := range d.fieldEncoders {
		encodedLen, err := fe.converter.encode(fe.getter(rv), payload[offset:])
		if err != nil {
			return nil, fmt.Errorf("field %s encode failed: %w", fe.name, err)
		}
		offset += encodedLen
	}
	return payload, nil
}

func (d *CompositeMetricDefinition[T]) AddCapturedValue(value T) error {
	payload, err := d.encodeCompositeValue(value)
	if err != nil {
		return err
	}
	d.mut.Lock()
	d.capturedValues = append(d.capturedValues, encodedMetricValue{timestamp: time.Now().UnixMilli(), payload: payload})
	d.mut.Unlock()
	return nil
}

func (d *CompositeMetricDefinition[T]) AddCallback(cb func() T) uint32 {
	d.mut.Lock()
	defer d.mut.Unlock()
	id := d.nextCBID
	d.callbacks[id] = cb
	d.nextCBID++
	return id
}

func (d *CompositeMetricDefinition[T]) RetrieveValues() ([]encodedMetricValue, error) {
	d.mut.Lock()
	defer d.mut.Unlock()
	for _, cb := range d.callbacks {
		payload, err := d.encodeCompositeValue(cb())
		if err != nil {
			return nil, err
		}
		d.capturedValues = append(d.capturedValues, encodedMetricValue{timestamp: time.Now().UnixMilli(), payload: payload})
	}
	values := make([]encodedMetricValue, len(d.capturedValues))
	copy(values, d.capturedValues)
	return values, nil
}
