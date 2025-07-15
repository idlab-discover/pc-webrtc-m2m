using Newtonsoft.Json;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

[System.Serializable]
public class LoopbackSessionManagerInfo 
{
    public LoopbackUserInfo[] loopbackUsers;
    public static LoopbackSessionManagerInfo CreateFromJSON(string path)
    {
        return JsonConvert.DeserializeObject<LoopbackSessionManagerInfo>(File.ReadAllText(path));
    }
}

[System.Serializable]
public class LoopbackUserInfo
{
    public uint clientID;
    public LoopbackTrackInfo[] loopbackTracks;
}

[System.Serializable]
public class LoopbackTrackInfo
{
    public uint capturerID;
    public uint trackID;

}