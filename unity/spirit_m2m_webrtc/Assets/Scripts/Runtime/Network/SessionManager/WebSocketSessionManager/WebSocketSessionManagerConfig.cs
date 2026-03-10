using Newtonsoft.Json;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

[System.Serializable]
public class WebSocketSessionManagerConfig 
{
    public string managerIP = "";
    public uint preferredClientID = 0;
    public string selectedCodecMode = "mdc";
    public bool startNewManager = false;
    public string managerProvisionerIP = "";
    public string managerProvisionerConfigPath = "";
    
    public static WebSocketSessionManagerConfig CreateFromJSON(string path)
    {
        return JsonConvert.DeserializeObject<WebSocketSessionManagerConfig>(File.ReadAllText(path));
    }
}
