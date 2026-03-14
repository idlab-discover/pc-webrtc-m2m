using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CompositeMetricDefinition<T> : MetricDefinitionBase where T : struct
{
    public static byte[] GetHeader() { return CompositeMetricWithValue<T>.Header; }
    private Dictionary<uint, MetricUpdateCallback<T>> callbacks = new();
    private uint callbackIdCounter = 0;
    public CompositeMetricDefinition(uint metricId, string name) : base(metricId, name)
    {
    }
    // List of callbacks to be called when the metric is updated
   

    public void AddCapturedValue(T value)
    {
        lock (_lock)
        {
            capturedValues.Add(new CompositeMetricWithValue<T>(value));
        }
    }

    public override List<GenericMetric> RetrieveMetrics()
    {
        lock (_lock)
        {
            foreach (var a in callbacks)
            {
                capturedValues.Add(new CompositeMetricWithValue<T>(a.Value()));
            }
            var temp = capturedValues;
            capturedValues = usingBacking0 ? capturedValuesBacking1 : capturedValuesBacking0;
            usingBacking0 = !usingBacking0;
            capturedValues.Clear();
            return temp;
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
