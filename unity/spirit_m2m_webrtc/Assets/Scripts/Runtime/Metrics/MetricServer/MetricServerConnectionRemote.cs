using Newtonsoft.Json;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Text;
using UnityEngine;

[Serializable]
public class MetricServerRegisterResponse
{
    public uint metricId { get; set; }
}


[Serializable]
public class MetricServerConnectionResponse
{
    public uint metricClientId { get; set; }
    public string readerConnectionType { get; set; }
    public string readerConnectionString { get; set; }
}

public class MetricServerConnectionRemote : MetricServerConnectionBase
{

    private HttpClient httpClient = new();
    private string serverUrl;
    private RemoteMetricSenderBase metricSender;

    public MetricServerConnectionRemote(string serverUrl) : base()
    {
        this.serverUrl = serverUrl;
    }
    protected override void connectInternal()
    {
        Debug.Log($"Connecting to metric server at {serverUrl}");
        var payload = new { };
        string jsonPayload = JsonConvert.SerializeObject(payload);
        var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");
        HttpResponseMessage response = httpClient.PostAsync($"http://{serverUrl}/connect?producerType=client", content).Result;
        if (response == null || !response.IsSuccessStatusCode) throw new Exception($"Failed to connect to metric server at {serverUrl}. Status Code: {response?.StatusCode}");
        string jsonResponse = response.Content.ReadAsStringAsync().Result;
        MetricServerConnectionResponse result = JsonConvert.DeserializeObject<MetricServerConnectionResponse>(jsonResponse);
        Debug.Log($"Received connection response from metric server. ClientId: {result.metricClientId}, ReaderConnectionType: {result.readerConnectionType}, ReaderConnectionString: {result.readerConnectionString}");
        ClientId = result.metricClientId;
        metricSender = RemoteMetricSenderFactory.CreateRemoteMetricSender(result.readerConnectionType);
        metricSender.Connect($"{this.serverUrl}{result.readerConnectionString}", result.metricClientId);
    }

    // TODO Also handle getting the field order from server
    protected override uint registerCompositeMetricDefinitionWithServer<T>(string metricName, byte[] header)
    {
        var payload = new { metricClientId = ClientId, metricName = metricName, header = header };
        string jsonPayload = JsonConvert.SerializeObject(payload);
        var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");
        HttpResponseMessage response = httpClient.PostAsync($"http://{serverUrl}/metric/register/composite", content).Result;
        if (response == null || !response.IsSuccessStatusCode) throw new Exception($"Failed to register metric {metricName} with server. Status Code: {response?.StatusCode}");
        string jsonResponse = response.Content.ReadAsStringAsync().Result;
        MetricServerRegisterResponse result = JsonConvert.DeserializeObject<MetricServerRegisterResponse>(jsonResponse);
        Debug.Log($"Registered composite metric {metricName} with server. Received MetricId: {result.metricId}");
        return result.metricId;
    }

    protected override uint registerMetricDefinitionWithServer<T>(string metricName, byte[] header)
    {
        var payload = new { metricClientId = ClientId, metricName = metricName, header = header };
        string jsonPayload = JsonConvert.SerializeObject(payload);
        var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");
        HttpResponseMessage response = httpClient.PostAsync($"http://{serverUrl}/metric/register/generic", content).Result;
        if(response == null || !response.IsSuccessStatusCode) throw new Exception($"Failed to register metric {metricName} with server. Status Code: {response?.StatusCode}");
        string jsonResponse = response.Content.ReadAsStringAsync().Result;
        MetricServerRegisterResponse result = JsonConvert.DeserializeObject<MetricServerRegisterResponse>(jsonResponse);
        Debug.Log($"Registered metric {metricName} with server. Received MetricId: {result.metricId}");
        return result.metricId;
    }

    protected override void writeMetrics(byte[] bytes)
    {
        metricSender.WriteMetrics(bytes);
        // throw new System.NotImplementedException();
    }
}
