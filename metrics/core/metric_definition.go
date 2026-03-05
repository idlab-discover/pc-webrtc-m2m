package core

import (
	"fmt"
	"metrics/core/logger"
	"unsafe"
)

type MetricType byte

const NameMetricDefinition = "MetricDefinition"

var (
	SignedNumeric        MetricType = 1
	UnsignedNumeric      MetricType = 2
	SignedNumericFloat   MetricType = 3
	UnsignedNumericFloat MetricType = 4
	String               MetricType = 5
	Custom               MetricType = 6
)

// Add converter functions? e.g., maybe one function to sum everything
type MetricDefinition struct {
	MetricId         uint32
	Name             string
	MaxNValues       uint
	IsComposite      bool
	NumberOfFields   uint32
	TotalValueSize   uint32
	ValueDefinitions []*MetricSingleValueDefinition

	// TODO
	SaveInMemory    bool
	StripTimestamps bool
}

type MetricSingleValueDefinition struct {
	FieldName       string
	ValueSize       uint32 /*Only used for fixed size types, like int32, float64 etc...*/
	HasVariableSize bool   /*Also if valueSize = 0, it has to be variable size*/
	MetricType      MetricType
}

// Atm we only handle fixed size types
type MetricValueCollection struct {
	MaxNValues        uint
	CurrentCapacity   uint
	NCurrentValues    uint
	RoundRobinCounter uint
	ValueBuffer       []byte
	ValueSize         uint32
	ValueSizes        []uint32 /*Only used for variables size types, like string etc...*/

	SaveInMemory    bool
	StripTimestamps bool
}

func NewMetricDefinition(metricId uint32, name string, maxValues uint, isComposite bool, header []byte, saveInMemory bool, stripTimestamps bool) *MetricDefinition {
	logger.LogWithMessage(NameMetricDefinition, logger.Creating, true, true, fmt.Sprintf("metricId=%d name=%s maxValues=%d isComposite=%t headerLength=%d", metricId, name, maxValues, isComposite, len(header)))
	numberOfFields := uint32(0)
	processedBytes := uint32(0)
	singleValueDefs := make([]*MetricSingleValueDefinition, 0)
	totalValueSize := uint32(0) /* without timestamp */
	for processedBytes < uint32(len(header)) {
		numberOfFields++
		fieldName := "single"
		if isComposite {
			fieldNameLength := *(*uint32)(unsafe.Pointer(&header[processedBytes]))
			processedBytes += 4
			fieldName = string(header[processedBytes : processedBytes+(uint32)(fieldNameLength)])
			processedBytes += uint32(fieldNameLength)
			println("FieldNameLength:", fieldNameLength, "FieldName:", fieldName, "offset after field name:", processedBytes)
		}

		typeByte := *(*byte)(unsafe.Pointer(&header[processedBytes]))
		metricType := MetricType(typeByte)
		processedBytes += 1
		valueSize := *(*uint32)(unsafe.Pointer(&header[processedBytes]))
		totalValueSize += valueSize
		processedBytes += 4
		println("Field:", fieldName, "Type:", metricType, "ValueSize:", valueSize)
		singleDef := &MetricSingleValueDefinition{
			FieldName:       fieldName,
			MetricType:      metricType,
			ValueSize:       valueSize,
			HasVariableSize: false,
		}
		singleValueDefs = append(singleValueDefs, singleDef)

	}
	return &MetricDefinition{
		MetricId:         metricId,
		Name:             name,
		MaxNValues:       maxValues,
		IsComposite:      isComposite,
		NumberOfFields:   numberOfFields,
		TotalValueSize:   totalValueSize,
		ValueDefinitions: singleValueDefs,
		SaveInMemory:     true,
		StripTimestamps:  false,
	}
}

func NewMetricValueCollection(definition *MetricDefinition) *MetricValueCollection {
	return &MetricValueCollection{
		MaxNValues:     definition.MaxNValues,
		NCurrentValues: 0,
		ValueBuffer:    make([]byte, (definition.TotalValueSize+8)*uint32(definition.MaxNValues)),
		ValueSizes:     make([]uint32, 0),
		ValueSize:      definition.TotalValueSize,
	}
}

func (c *MetricValueCollection) AddValue(value []byte) {
	if !c.SaveInMemory {
		return
	}
	copy(c.ValueBuffer[uint32(c.RoundRobinCounter)*(c.ValueSize+8):], value)
	c.RoundRobinCounter = (c.RoundRobinCounter + 1) % c.MaxNValues
	if c.NCurrentValues < c.MaxNValues {
		c.NCurrentValues++
	}
}
