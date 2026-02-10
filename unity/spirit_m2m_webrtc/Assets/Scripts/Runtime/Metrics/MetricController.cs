using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Buffers.Binary;


public delegate GenericMetric MetricUpdateCallback();
// TODO Should this be a GameObject or just generic class?
public class MetricController : MonoBehaviour
{
    private float currentTime = 0f;
    private float timeInterval = 1f; // Time interval in seconds for updating metrics
    //private Dictionary<string, GenericMetricDefinition> metricDefinitions = new();
    private MetricServerConnectionBase metricServerConnection;
    void Start()
    {

    }

    // Update is called once per frame
    void Update()
    {
        currentTime += Time.deltaTime;
        if (currentTime >= timeInterval)
        {
            metricServerConnection.UpdateMetrics();
            currentTime -= timeInterval;
        }
    }

}