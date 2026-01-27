using Newtonsoft.Json;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

[Serializable]
public class BenchDownsamplePCConfig
{
    public string outputPath = ""; // If Empty, do not safe
    public string filePrefix = "";
    public uint maxFrames = 1;
    public uint nPointsPerFrame = 100000;
    public static BenchDownsamplePCConfig CreateFromJSON(string path)
    {
        return JsonConvert.DeserializeObject<BenchDownsamplePCConfig>(File.ReadAllText(path));
    }
}
