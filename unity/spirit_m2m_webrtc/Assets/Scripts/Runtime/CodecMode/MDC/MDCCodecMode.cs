using Newtonsoft.Json.Linq;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.VisualScripting;
using UnityEngine;


[System.Serializable]
public class MDCTrackSettings
{
    public uint samplingPercentage;
}

[System.Serializable]
public class MDCCodecModeSettings
{
    public List<uint> samplingPercentages;
    public uint maxCompletePointCount;
}

[CodecModeRegister("mdc")]
public class MDCCodecMode : CodecModeBase
{
    protected override string NAME => "MDCCodecMode";

    protected override string MODE_NAME => "mdc";

    public override Dictionary<string, CodecModeTrack> CollectTrackInfo(SessionInfo sessionInfo, ModeCodecPair modeCodec)
    {
        
        Dictionary<string, CodecModeTrack> tracks = new();
        if (modeCodec.modeName.ToLower() != MODE_NAME)
        {
            return tracks;
        }
        var settings = modeCodec.modeSettings.ToObject<MDCCodecModeSettings>();
        List<uint> rates = new(settings.samplingPercentages);
        rates = rates.OrderByDescending(n => n).ToList();
        Logger.LogStatusWithMessage(NAME, Logger.Status.GatheringTrackInfo, $"samplingPercentages=[{string.Join('|', rates)}] maxCompletePointCount={settings.maxCompletePointCount}");
        for (int i = 0; i < rates.Count; i++)
        {
            var track = new CodecModeTrack
            {
                mode=  MODE_NAME,
                trackID = $"{MODE_NAME}_0_{i}",
                supportedCodecs = new List<string>(modeCodec.supportedCodecs),
                trackSettings = JObject.FromObject(new MDCTrackSettings { samplingPercentage = rates[i] })
            };
            Logger.LogStatusWithMessage(NAME, Logger.Status.NewTrackDiscovered, $"trackID={track.trackID} supportedCodecs=[{string.Join('|', track.supportedCodecs)}] samplingPercentage={rates[i]}");
            tracks.Add(track.trackID, track);
        }
        return tracks;
    }
    protected override void Dispose(bool disposing)
    {
        throw new System.NotImplementedException();
    }

   
}
