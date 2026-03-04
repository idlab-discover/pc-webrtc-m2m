using Newtonsoft.Json;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using UnityEngine;

[Serializable]
public class MetricServerRegisterResponse
{
    public uint metricId { get; set; }
}

public class MetricServerConnectionRemote : MetricServerConnectionBase
{

    private HttpClient httpClient = new();
    private string serverUrl;

    public MetricServerConnectionRemote(string serverUrl) : base()
    {
        this.serverUrl = serverUrl;
    }
    protected override void connectInternal()
    {
        var payload = new { clientId = ClientId };
        string jsonPayload = JsonConvert.SerializeObject(payload);
        var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");
        HttpResponseMessage response = httpClient.PostAsync($"{serverUrl}/connect", content).Result;
    }

    // TODO Also handle getting the field order from server
    protected override uint registerCompositeMetricDefinitionWithServer<T>(string metricName, byte[] header)
    {
        var payload = new { clientId = ClientId, header = header };
        string jsonPayload = JsonConvert.SerializeObject(payload);
        var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");
        HttpResponseMessage response = httpClient.PostAsync($"{serverUrl}/metric/register/composite", content).Result;
        if (response == null || !response.IsSuccessStatusCode) throw new Exception($"Failed to register metric {metricName} with server. Status Code: {response?.StatusCode}");
        string jsonResponse = response.Content.ReadAsStringAsync().Result;
        MetricServerRegisterResponse result = JsonConvert.DeserializeObject<MetricServerRegisterResponse>(jsonResponse);
        return result.metricId;
    }

    protected override uint registerMetricDefinitionWithServer<T>(string metricName, byte[] header)
    {
        var payload = new { clientId = ClientId, header = header };
        string jsonPayload = JsonConvert.SerializeObject(payload);
        var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");
        HttpResponseMessage response = httpClient.PostAsync($"{serverUrl}/metric/register/generic", content).Result;
        if(response == null || !response.IsSuccessStatusCode) throw new Exception($"Failed to register metric {metricName} with server. Status Code: {response?.StatusCode}");
        string jsonResponse = response.Content.ReadAsStringAsync().Result;
        MetricServerRegisterResponse result = JsonConvert.DeserializeObject<MetricServerRegisterResponse>(jsonResponse);
        return result.metricId;
    }

    protected override void writeMetrics(byte[] bytes)
    {
        throw new System.NotImplementedException();
    }
}
