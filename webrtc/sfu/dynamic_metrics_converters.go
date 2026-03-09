package main

import (
	"encoding/binary"
	"fmt"
	"math"
	"reflect"
	"sync"
)

type MetricValueType byte

const (
	MetricTypeSignedNumeric        MetricValueType = 1
	MetricTypeUnsignedNumeric      MetricValueType = 2
	MetricTypeSignedNumericFloat   MetricValueType = 3
	MetricTypeUnsignedNumericFloat MetricValueType = 4
	MetricTypeString               MetricValueType = 5
	MetricTypeCustom               MetricValueType = 6
)

type scalarConverter struct {
	metricType MetricValueType
	size       uint32
	encode     func(value any, dst []byte) (int, error)
}

var scalarConverterRegistry sync.Map // map[reflect.Type]scalarConverter

func init() {
	registerBuiltinScalarConverters()
}

func registerBuiltinScalarConverters() {
	registerScalarConverter(reflect.TypeFor[int8](), scalarConverter{metricType: MetricTypeSignedNumeric, size: 1, encode: func(value any, dst []byte) (int, error) {
		v, ok := value.(int8)
		if !ok {
			return 0, fmt.Errorf("expected int8, got %T", value)
		}
		dst[0] = byte(v)
		return 1, nil
	}})
	registerScalarConverter(reflect.TypeFor[int16](), scalarConverter{metricType: MetricTypeSignedNumeric, size: 2, encode: func(value any, dst []byte) (int, error) {
		v, ok := value.(int16)
		if !ok {
			return 0, fmt.Errorf("expected int16, got %T", value)
		}
		binary.LittleEndian.PutUint16(dst, uint16(v))
		return 2, nil
	}})
	registerScalarConverter(reflect.TypeFor[int32](), scalarConverter{metricType: MetricTypeSignedNumeric, size: 4, encode: func(value any, dst []byte) (int, error) {
		v, ok := value.(int32)
		if !ok {
			return 0, fmt.Errorf("expected int32, got %T", value)
		}
		binary.LittleEndian.PutUint32(dst, uint32(v))
		return 4, nil
	}})
	registerScalarConverter(reflect.TypeFor[int64](), scalarConverter{metricType: MetricTypeSignedNumeric, size: 8, encode: func(value any, dst []byte) (int, error) {
		v, ok := value.(int64)
		if !ok {
			return 0, fmt.Errorf("expected int64, got %T", value)
		}
		binary.LittleEndian.PutUint64(dst, uint64(v))
		return 8, nil
	}})
	registerScalarConverter(reflect.TypeFor[int](), scalarConverter{metricType: MetricTypeSignedNumeric, size: uint32(intSizeBytes()), encode: func(value any, dst []byte) (int, error) {
		v, ok := value.(int)
		if !ok {
			return 0, fmt.Errorf("expected int, got %T", value)
		}
		sz := intSizeBytes()
		if sz == 8 {
			binary.LittleEndian.PutUint64(dst, uint64(int64(v)))
		} else {
			binary.LittleEndian.PutUint32(dst, uint32(int32(v)))
		}
		return sz, nil
	}})

	registerScalarConverter(reflect.TypeFor[uint8](), scalarConverter{metricType: MetricTypeUnsignedNumeric, size: 1, encode: func(value any, dst []byte) (int, error) {
		v, ok := value.(uint8)
		if !ok {
			return 0, fmt.Errorf("expected uint8, got %T", value)
		}
		dst[0] = v
		return 1, nil
	}})
	registerScalarConverter(reflect.TypeFor[uint16](), scalarConverter{metricType: MetricTypeUnsignedNumeric, size: 2, encode: func(value any, dst []byte) (int, error) {
		v, ok := value.(uint16)
		if !ok {
			return 0, fmt.Errorf("expected uint16, got %T", value)
		}
		binary.LittleEndian.PutUint16(dst, v)
		return 2, nil
	}})
	registerScalarConverter(reflect.TypeFor[uint32](), scalarConverter{metricType: MetricTypeUnsignedNumeric, size: 4, encode: func(value any, dst []byte) (int, error) {
		v, ok := value.(uint32)
		if !ok {
			return 0, fmt.Errorf("expected uint32, got %T", value)
		}
		binary.LittleEndian.PutUint32(dst, v)
		return 4, nil
	}})
	registerScalarConverter(reflect.TypeFor[uint64](), scalarConverter{metricType: MetricTypeUnsignedNumeric, size: 8, encode: func(value any, dst []byte) (int, error) {
		v, ok := value.(uint64)
		if !ok {
			return 0, fmt.Errorf("expected uint64, got %T", value)
		}
		binary.LittleEndian.PutUint64(dst, v)
		return 8, nil
	}})
	registerScalarConverter(reflect.TypeFor[uint](), scalarConverter{metricType: MetricTypeUnsignedNumeric, size: uint32(intSizeBytes()), encode: func(value any, dst []byte) (int, error) {
		v, ok := value.(uint)
		if !ok {
			return 0, fmt.Errorf("expected uint, got %T", value)
		}
		sz := intSizeBytes()
		if sz == 8 {
			binary.LittleEndian.PutUint64(dst, uint64(v))
		} else {
			binary.LittleEndian.PutUint32(dst, uint32(v))
		}
		return sz, nil
	}})

	registerScalarConverter(reflect.TypeFor[float32](), scalarConverter{metricType: MetricTypeSignedNumericFloat, size: 4, encode: func(value any, dst []byte) (int, error) {
		v, ok := value.(float32)
		if !ok {
			return 0, fmt.Errorf("expected float32, got %T", value)
		}
		binary.LittleEndian.PutUint32(dst, math.Float32bits(v))
		return 4, nil
	}})
	registerScalarConverter(reflect.TypeFor[float64](), scalarConverter{metricType: MetricTypeSignedNumericFloat, size: 8, encode: func(value any, dst []byte) (int, error) {
		v, ok := value.(float64)
		if !ok {
			return 0, fmt.Errorf("expected float64, got %T", value)
		}
		binary.LittleEndian.PutUint64(dst, math.Float64bits(v))
		return 8, nil
	}})
}

func registerScalarConverter(t reflect.Type, converter scalarConverter) {
	scalarConverterRegistry.Store(t, converter)
}

func lookupScalarConverter(t reflect.Type) (scalarConverter, bool) {
	v, ok := scalarConverterRegistry.Load(t)
	if !ok {
		return scalarConverter{}, false
	}
	converter, ok := v.(scalarConverter)
	return converter, ok
}

func buildGenericHeaderForType(t reflect.Type) ([]byte, int, scalarConverter, error) {
	if t == nil {
		return nil, 0, scalarConverter{}, fmt.Errorf("unsupported metric type: <nil>")
	}
	converter, ok := lookupScalarConverter(t)
	if !ok {
		return nil, 0, scalarConverter{}, fmt.Errorf("unsupported metric type: %s", t.String())
	}
	header := make([]byte, 5)
	header[0] = byte(converter.metricType)
	binary.LittleEndian.PutUint32(header[1:], converter.size)
	return header, int(converter.size), converter, nil
}

func intSizeBytes() int {
	var n int
	return int(reflect.TypeOf(n).Size())
}
