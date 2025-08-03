using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public static class TrackInfoGatherer 
{
    public static Dictionary<string, CodecModeTrack> GatherTrackInfo(SessionInfo sessionInfo)
    {
        // Video tracks
        Dictionary<string, CodecModeTrack> tracks = new();
        if(sessionInfo == null || sessionInfo.supportedModes == null)
        {
            Debug.LogError("SessionInfo is null, cannot gather track info");
            return tracks;
        }
        foreach (var m in sessionInfo.supportedModes)
        {
            CodecModeBase cm = CodecModeRepository.GetAndCreateIfNotExists(m.modeName);
            foreach(var c in cm.CollectTrackInfo(sessionInfo, m))
            {
                if (tracks.ContainsKey(c.Key))
                {
                    Debug.LogWarning($"Track {c.Key} already exists, skipping");
                    continue;
                }
                tracks.Add(c.Key, c.Value);
            }
        }
        return tracks;
        // Get supported codecs from config
        // Get supported modes from config
        // Use modes + codecs to generate default track names
        // TODO take into account codec settings i.e. stuff like mdc-based encoding so maybe additional settings as well

        // Audio tracks
    }
}
