using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public abstract class RemoteMetricSenderBase 
{
    protected readonly object _lock = new();
    public bool IsConnected { get; protected set; }
    public abstract void Connect(string serverAddress, uint metricClientId); /*This is the Id returned from the metric server, different from clientId*/
    public abstract void WriteMetrics(byte[] bytes);
    
}
