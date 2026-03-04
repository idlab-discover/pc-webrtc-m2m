using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using System.Buffers.Binary;

// TODO Split between reliable and unreliable metrics
public abstract class MetricServerConnectionBase 
{
    public uint ClientId { get; private set; }
    protected bool isConnected = false;
    protected bool isInited = false;
    protected bool isConnecting = false;
    private Dictionary<string, MetricDefinitionBase> metricDefinitions = new();
   
    private readonly object _lock = new();

    public void Init(uint clientId, bool connectImmediately)
    {
        ClientId = clientId;
        isInited = true;
    }

    public void Connect()
    {
        if(!isInited)
        {
            throw new InvalidOperationException("MetricServerConnectionBase must be inited before connecting.");
        }
        lock (_lock)
        {
            if (!isConnected && !isConnecting)
            {
                isConnecting = true;
                connectInternal();
                isConnecting = false;
            }
        }
        
    }
    protected abstract void connectInternal();

    public GenericMetricDefinition<T> RegisterPushMetric<T>(string metricName)
    {
        lock (_lock)
        {
            return registerOrGetMetric<T>(metricName);
        }
    }
    public CompositeMetricDefinition<T> RegisterCompositePushMetric<T>(string metricName) where T : struct
    {
        lock (_lock)
        {
            return registerOrGetCompositeMetric<T>(metricName);
        }
    }

    public uint RegisterPullMetric<T>(string metricName, MetricUpdateCallback<T> cb)
    {
        lock (_lock)
        {
            GenericMetricDefinition<T> metric = registerOrGetMetric<T>(metricName);
            uint callbackId = metric.AddCallback(cb);
            return callbackId;
        }
    }
    public uint RegisterCompositePullMetric<T>(string metricName, MetricUpdateCallback<T> cb) where T : struct
    {
        lock (_lock)
        {
            CompositeMetricDefinition<T> metric = registerOrGetCompositeMetric<T>(metricName);
            uint callbackId = metric.AddCallback(cb);
            return callbackId;
        }
    }
    private GenericMetricDefinition<T> registerOrGetMetric<T>(string metricName)
    {
        MetricDefinitionBase metricBase = null;
        GenericMetricDefinition<T> metric = null;
        if (!metricDefinitions.TryGetValue(metricName, out metricBase))
        {
            // Metric already exists, add callback to existing metric definition
            uint metricID = registerMetricDefinitionWithServer<T>(metricName, GenericMetricDefinition<T>.GetHeader() /*Header is required to know how to parse metric*/);
            metric = new GenericMetricDefinition<T>(metricID, metricName);
            metricDefinitions[metricName] = metric;
            // TODO Metric counter should be trieved from the metric server?
        }
        else
        {
            metric = metricBase as GenericMetricDefinition<T>;
            if (metric == null)
            {
                throw new InvalidOperationException($"Metric with name {metricName} already exists with a different type.");
            }
        }
        return metric;
    }
    private CompositeMetricDefinition<T> registerOrGetCompositeMetric<T>(string metricName) where T : struct
    {
        MetricDefinitionBase metricBase = null;
        CompositeMetricDefinition<T> metric = null;
        if (!metricDefinitions.TryGetValue(metricName, out metricBase))
        {
            uint metricID = registerCompositeMetricDefinitionWithServer<T>(metricName, CompositeMetricDefinition<T>.GetHeader());
            metric = new CompositeMetricDefinition<T>(metricID, metricName);
            metricDefinitions[metricName] = metric;
            // Call server to register metric definition and headers   
        }
        else
        {
            metric = metricBase as CompositeMetricDefinition<T>;
            if (metric == null)
            {
                throw new InvalidOperationException($"Metric with name {metricName} already exists with a different type.");
            }
        }
        return metric;
    }
    public void UnRegisterPullMetric(string metricName, uint callbackId)
    {
        lock (_lock)
        {
            if (metricDefinitions.TryGetValue(metricName, out MetricDefinitionBase metric))
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
        byte[] bytes;
        lock (_lock)
        {
            Dictionary<uint, List<GenericMetric>> allMetrics = new();
            int bufferSize = 0;
            foreach (var metric in metricDefinitions)
            {
                List<GenericMetric> lst = metric.Value.RetrieveMetrics(); 
                bufferSize += sizeof(uint) /*MetricID*/ + sizeof(int) /*Number of values for metric*/;
                foreach (var m in lst)
                {
                    bufferSize += m.Length;
                }
                allMetrics[metric.Value.MetricId] = lst;
            }
            // TODO Limit the max message size
            // so in case we lose 1 packet we can still be certain that all metrics in 1 packet will be received
            // Will have to make byte[] of max size and then probably span it to reduce it or something
            bytes = new byte[bufferSize];
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
                Debug.Log("Offset " + offset + " / " + bufferSize);
            }
            foreach (var metric in metricDefinitions)
            {
                metric.Value.ClearMetrics();
            }
        }
        writeMetrics(bytes);
    }
    protected abstract void writeMetrics(byte[] bytes);
    protected abstract uint registerMetricDefinitionWithServer<T>(string metricName, byte[] header);
    protected abstract uint registerCompositeMetricDefinitionWithServer<T>(string metricName, byte[] header) where T : struct;
}
