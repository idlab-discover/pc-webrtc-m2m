using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Buffers.Binary;

public delegate T MetricUpdateCallback<T>();
// TODO Should this be a GameObject or just generic class?
struct CompositeMetricTestStruct
{
    public int IntValue;
    public uint UIntValue;
}
public class MetricController : MonoBehaviour
{
    private float currentTime = 0f;
    private float timeInterval = 1f; // Time interval in seconds for updating metrics
    //private Dictionary<string, GenericMetricDefinition> metricDefinitions = new();
    private MetricServerConnectionBase metricServerConnection;
    private GenericMetricDefinition<int> testMetricDefinition;
    private CompositeMetricDefinition<CompositeMetricTestStruct> compositeMetricDefinition;
    void Start()
    {
        ByteConverterInit.Init();
        metricServerConnection = new MetricServerConnectionLocal();
        metricServerConnection.RegisterPullMetric<int>("TestMetric", GetTestMetric);
        testMetricDefinition = metricServerConnection.RegisterPushMetric<int>("TestMetric2");
        compositeMetricDefinition = metricServerConnection.RegisterCompositePushMetric<CompositeMetricTestStruct>("CompositeTestMetric");
    }

    // Update is called once per frame
    void Update()
    {
        currentTime += Time.deltaTime;
        if (currentTime >= timeInterval)
        {
            Debug.Log("Updating metrics");
            testMetricDefinition?.AddCapturedValue(UnityEngine.Random.Range(0, 100));
            testMetricDefinition?.AddCapturedValue(UnityEngine.Random.Range(0, 100));
            compositeMetricDefinition?.AddCapturedValue(new CompositeMetricTestStruct
            {
                IntValue = UnityEngine.Random.Range(0, 100),
                UIntValue = (uint)UnityEngine.Random.Range(0, 100)
            });
            metricServerConnection.UpdateMetrics();
            currentTime -= timeInterval;
        }
    }
    int GetTestMetric()
    {
        return UnityEngine.Random.Range(0, 100);
    }

}