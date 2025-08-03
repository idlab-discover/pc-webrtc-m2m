using Newtonsoft.Json;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

[System.Serializable]
public class RawReceiverTrackInfo
{
    public string depthCodecName;
    public string colorCodecName;
    // TODO maybe do something with width / height (need it for the raw converter stuff)
    // Also add rawconverter settings here, i.e. map depth to color, color to depth etc...

    public static RawReceiverTrackInfo CreateFromJSON(string path)
    {
        return JsonConvert.DeserializeObject<RawReceiverTrackInfo>(File.ReadAllText(path));
    }
}

