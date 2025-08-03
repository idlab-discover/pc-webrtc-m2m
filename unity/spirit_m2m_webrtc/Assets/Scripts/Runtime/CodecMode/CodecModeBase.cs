using Newtonsoft.Json.Linq;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class CodecModeTrack
{
    public string mode;
    public string trackID;
    public List<string> supportedCodecs;
    public JObject trackSettings;
}

public abstract class CodecModeBase : IDisposable
{
    protected abstract string NAME { get; }
    protected abstract string MODE_NAME { get; }
    public CodecModeBase() { }
    public abstract Dictionary<string, CodecModeTrack> CollectTrackInfo(SessionInfo sessionInfo, ModeCodecPair modeCodec);
    protected abstract void Dispose(bool disposing);
    public void Dispose()
    {
        // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }
}
