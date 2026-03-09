package main

import (
	"metrics/core"
)

type MetricsServerHelper struct {
	MetricsServer   *core.MetricsServer
	MetricsProvider *core.MetricProducerConnection
	Dynamic         *DynamicMetricsConnection
}

func NewMetricsServerHelper(metricsServerConfigPath string) *MetricsServerHelper {
	metricsServer := core.NewMetricsServer(metricsServerConfigPath)
	metricsProvider := metricsServer.AddLocalProducer("sfu")
	return &MetricsServerHelper{
		MetricsServer:   metricsServer,
		MetricsProvider: metricsProvider,
		Dynamic:         NewDynamicMetricsConnection(metricsServer, metricsProvider),
	}
}

func RegisterPushMetricWithHelper[T any](h *MetricsServerHelper, metricName string) (*GenericMetricDefinition[T], error) {
	return RegisterPushMetric[T](h.Dynamic, metricName)
}

func RegisterPullMetricWithHelper[T any](h *MetricsServerHelper, metricName string, cb func() T) (uint32, *GenericMetricDefinition[T], error) {
	return RegisterPullMetric[T](h.Dynamic, metricName, cb)
}

func RegisterCompositePushMetricWithHelper[T any](h *MetricsServerHelper, metricName string) (*CompositeMetricDefinition[T], error) {
	return RegisterCompositePushMetric[T](h.Dynamic, metricName)
}

func RegisterCompositePullMetricWithHelper[T any](h *MetricsServerHelper, metricName string, cb func() T) (uint32, *CompositeMetricDefinition[T], error) {
	return RegisterCompositePullMetric[T](h.Dynamic, metricName, cb)
}

func (h *MetricsServerHelper) UpdateMetrics() error {
	return h.Dynamic.UpdateMetrics()
}
