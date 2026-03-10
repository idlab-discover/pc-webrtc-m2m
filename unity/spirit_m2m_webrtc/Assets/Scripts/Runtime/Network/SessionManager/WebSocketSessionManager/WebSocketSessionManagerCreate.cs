using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

[System.Serializable]
public class WebSocketSessionManagerCreate 
{
    public string address = "";
    public bool verifyAuthKey = false;
    public bool ignorePreferredClientID = false;
    public string defaultProviderConfigPath = "";
    public string provisionerType = "";
    public string provisionerConfigPath = "";
    public List<JObject> providersToCreate = new();

    public bool enableMetrics = false;
    public string metricsServerAddress = "";
    public string providerAsMetricsServer = "";
    public string metricsConfigPath = "";

    public static WebSocketSessionManagerCreate CreateFromJSON(string path)
    {
        return JsonConvert.DeserializeObject<WebSocketSessionManagerCreate>(File.ReadAllText(path));
    }
}


