using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using System.Buffers.Binary;

// TODO Split between reliable and unreliable metrics
public abstract class MetricServerConnectionBase : MonoBehaviour
{
    private Dictionary<string, GenericMetricDefinition> metricDefinitions = new();
    private uint metricIdCounter = 0;
    private readonly object _lock = new();
    public void RegisterPushMetric(string metricName)
    {
        lock (_lock)
        {

        }
    }

    public uint RegisterPullMetric(string metricName, MetricUpdateCallback cb)
    {
        lock (_lock)
        {
            GenericMetricDefinition metric = null;
            if (!metricDefinitions.TryGetValue(metricName, out metric))
            {
                // Metric already exists, add callback to existing metric definition
                metric = new GenericMetricDefinition(metricIdCounter++, metricName);
                // TODO Metric counter should be trieved from the metric server?
            }
            uint callbackId = metric.AddCallback(cb);
            return callbackId;
        }
    }
    public void UnRegisterPullMetric(string metricName, uint callbackId)
    {
        lock (_lock)
        {
            if (metricDefinitions.TryGetValue(metricName, out GenericMetricDefinition metric))
            {
                // remove callback

                /* if(metric.)
                 {

                 }*/
            }

        }
    }
    public void UpdateMetrics()
    {
        lock (_lock)
        {
            Dictionary<uint, List<GenericMetric>> allMetrics = new();
            int bufferSize = 0;
            foreach (var metric in metricDefinitions)
            {
                List<GenericMetric> lst = metric.Value.RetrieveMetrics();
                foreach (var m in lst)
                {
                    bufferSize += m.Length;
                }
                allMetrics[metric.Value.MetricId] = lst;
            }
            byte[] bytes = new byte[bufferSize];
            Span<byte> buffer = bytes.AsSpan();
            int offset = 0;
            foreach (var lst in allMetrics)
            {
                BinaryPrimitives.WriteUInt32LittleEndian(buffer.Slice(offset), lst.Key);
                offset += sizeof(uint);
                BinaryPrimitives.WriteInt32LittleEndian(buffer.Slice(offset), lst.Value.Count);
                offset += sizeof(int);
                foreach (var m in lst.Value)
                {
                    offset += m.CopyToBuffer(buffer.Slice(offset));
                }
            }
            foreach (var metric in metricDefinitions)
            {
                metric.Value.ClearMetrics();
            }
        }
    }
}
