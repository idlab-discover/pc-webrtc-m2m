using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public abstract class MetricDefinitionBase
{
    public string Name { get; private set; }
    public uint MetricId { get; private set; }
    protected List<GenericMetric> capturedValues;
    protected List<GenericMetric> capturedValuesBacking0 = new();
    protected List<GenericMetric> capturedValuesBacking1 = new();
    protected bool usingBacking0 = true;
    protected readonly object _lock = new();

    public MetricDefinitionBase(uint metricId, string name)
    {
        MetricId = metricId;
        Name = name;
        capturedValues = capturedValuesBacking0;
    }

    public abstract List<GenericMetric> RetrieveMetrics();
    public void ClearMetrics() { lock (_lock) { capturedValues.Clear(); } }
}
