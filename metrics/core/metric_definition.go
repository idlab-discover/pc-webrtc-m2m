package core

type MetricType uint

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
	Name            string
	MetricType      MetricType
	MaxNValues      uint
	ValueSize       uint /*Only used for fixed size types, like int32, float64 etc...*/
	HasVariableSize bool /*Also if valueSize = 0, it has to be variable size*/
	IsComposite     bool
}

// Atm we only handle fixed size types
type MetricValueCollection struct {
	MaxNValues        uint
	CurrentCapacity   uint
	NCurrentValues    uint
	RoundRobinCounter uint
	ValueBuffer       []byte
	ValueSize         uint
	ValueSizes        []uint /*Only used for variables size types, like string etc...*/
}

func NewMetricDefinition(name string, metricType MetricType, hasVariableSize bool, isComposite bool) *MetricDefinition {
	return &MetricDefinition{
		Name:            name,
		MetricType:      metricType,
		HasVariableSize: hasVariableSize,
		IsComposite:     isComposite,
	}
}

func NewMetricValueCollection(definition *MetricDefinition) *MetricValueCollection {
	return &MetricValueCollection{
		MaxNValues:     definition.MaxNValues,
		NCurrentValues: 0,
		ValueBuffer:    make([]byte, (definition.ValueSize+8 /*for timestamp*/)*definition.MaxNValues),
		ValueSizes:     make([]uint, 0),
	}
}

func (c *MetricValueCollection) AddValue(value []byte) {
	copy(c.ValueBuffer[c.RoundRobinCounter*(c.ValueSize+8):], value)
	c.RoundRobinCounter = (c.RoundRobinCounter + 1) % c.MaxNValues
	if c.NCurrentValues < c.MaxNValues {
		c.NCurrentValues++
	}
}
