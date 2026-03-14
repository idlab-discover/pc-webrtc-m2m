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
    public bool UseLocalConnection = true;
    public string RemoteAddress = "";
    private float currentTime = 0f;
    private float timeInterval = 1f; // Time interval in seconds for updating metrics
    //private Dictionary<string, GenericMetricDefinition> metricDefinitions = new();
    public static MetricServerConnectionBase MetricServerConnection; // TODO Probably need to change this or something
    private GenericMetricDefinition<int> testMetricDefinition;
    private CompositeMetricDefinition<CompositeMetricTestStruct> compositeMetricDefinition;
    public void Init()
    {
        ByteConverterInit.Init();
        if (UseLocalConnection)
        {
            MetricServerConnection = new MetricServerConnectionLocal();
        }
        else
        {
            MetricServerConnection = new MetricServerConnectionRemote(RemoteAddress);
        }
        MetricServerConnection.Init(true);
        MetricServerConnection.RegisterPullMetric<int>("TestMetric", GetTestMetric);
        testMetricDefinition = MetricServerConnection.RegisterPushMetric<int>("TestMetric2");
        compositeMetricDefinition = MetricServerConnection.RegisterCompositePushMetric<CompositeMetricTestStruct>("CompositeTestMetric");

        // Add system usage component
        gameObject.AddComponent<SystemUsage>().Init();

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
            MetricServerConnection.UpdateMetrics();
            currentTime -= timeInterval;
        }
    }
    int GetTestMetric()
    {
        return UnityEngine.Random.Range(0, 100);
    }

}