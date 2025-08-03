using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

[System.Serializable]
public class PreferredProvidersConfig
{
    public string defaultProvider;
    public ProviderConfig[] providers;

    public static PreferredProvidersConfig CreateFromJSON(string path)
    {
        try
        {
            return JsonConvert.DeserializeObject<PreferredProvidersConfig>(File.ReadAllText(path));
        }
        catch
        {
            Debug.LogError($"Failed to load PreferredProvidersConfig from {path}");
            return null;
        }
    }

}

[System.Serializable]
public class ProviderConfig {
    public string key;
    public string type;
    public JObject providerSettings;
    public ProviderTrackInfo[] sendingTracks;
}

[System.Serializable]
public class ProviderTrackInfo
{
    public string trackID;
}

