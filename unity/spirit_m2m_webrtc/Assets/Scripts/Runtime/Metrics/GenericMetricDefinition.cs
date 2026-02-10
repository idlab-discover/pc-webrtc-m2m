using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Maybe this is overkill; might have to investigate performance impact later
public class GenericMetricDefinition
{
    public string Name { get; private set; }
    public uint MetricId { get; private set; }
    private List<GenericMetric> capturedValues = new();
    // List of callbacks to be called when the metric is updated
    private Dictionary<uint, MetricUpdateCallback> callbacks = new();
    private uint callbackIdCounter = 0;
    public GenericMetricDefinition(uint metricId, string name)
    {
        MetricId = metricId;
        Name = name;
    }

    public List<GenericMetric> RetrieveMetrics() { 
        foreach(var a in callbacks)
        {
            capturedValues.Add(a.Value());
        }
        return capturedValues;
    }
    public void ClearMetrics() { capturedValues.Clear(); }
    public uint AddCallback(MetricUpdateCallback cb)
    {
        uint retValue = callbackIdCounter;
        callbacks.Add(retValue, cb);
        callbackIdCounter++;
        return retValue;
    }
}
