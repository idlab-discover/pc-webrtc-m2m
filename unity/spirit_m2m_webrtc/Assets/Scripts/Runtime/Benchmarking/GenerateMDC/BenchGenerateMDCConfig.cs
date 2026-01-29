using Newtonsoft.Json;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

[System.Serializable]
public class BenchGenerateMDCConfig
{
    public string outputPath = ""; // If Empty, do not safe
    public string subDirectoryPrefix = "mdc_video";
    public uint downsampleToNPoints = 0;
    public uint maxFrames = 300;
    public bool validateEncoding = false;
    public static BenchGenerateMDCConfig CreateFromJSON(string path)
    {
        return JsonConvert.DeserializeObject<BenchGenerateMDCConfig>(File.ReadAllText(path));
    }
}
