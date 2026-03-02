using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Maybe this is overkill; might have to investigate performance impact later
public class GenericMetricDefinition<T> : MetricDefinitionBase
{
    public static byte[] GetHeader()
    {
        return ByteConverterCache<T>.GetHeader();
    }
    // List of callbacks to be called when the metric is updated
    private Dictionary<uint, MetricUpdateCallback<T>> callbacks = new();
    private uint callbackIdCounter = 0;
    
    public GenericMetricDefinition(uint metricId, string name): base(metricId, name)
    {
        
    }
    public void AddCapturedValue(T value)
    {
        lock (_lock)
        {
            capturedValues.Add(new GenericMetricWithValue<T>(value));
        }
    }

    public override List<GenericMetric> RetrieveMetrics()
    {
        lock (_lock)
        {
            foreach (var a in callbacks)
            {
                capturedValues.Add(new GenericMetricWithValue<T>(a.Value()));
            }
            return capturedValues;
        }
    }
    // TODO Maybe lock
    public uint AddCallback(MetricUpdateCallback<T> cb)
    {
        uint retValue = callbackIdCounter;
        callbacks.Add(retValue, cb);
        callbackIdCounter++;
        return retValue;
    }
}
