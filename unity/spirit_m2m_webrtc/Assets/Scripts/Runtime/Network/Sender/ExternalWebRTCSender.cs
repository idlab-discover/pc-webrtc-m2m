using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ExternalWebRTCSender : NetworkSenderBase
{
    private IntPtr connectionPtr;
    private uint trackCounter = 0;
    private Dictionary<string, uint> videoTrackToID = new();
    private Dictionary<string, uint> audioTrackToID = new();
    public ExternalWebRTCSender(IntPtr connectionPtr, List<TrackSimple> videoTracks, List<TrackSimple> audioTracks)
    {
        this.connectionPtr = connectionPtr;
        foreach (var track in videoTracks)
        {
            videoTrackToID[track.trackID] = trackCounter;
            trackCounter++;
        }
        foreach (var track in audioTracks)
        {
            audioTrackToID[track.trackID] = trackCounter;
            trackCounter++;
        }
        IsValid = true;
    }
    public override int SendAudioData(uint frameNr, IntPtr data, uint size)
    {
        throw new NotImplementedException();
    }

    public override int SendVideoData(string trackID, uint frameNr, IntPtr data, uint size)
    {
        if (!IsValid)
            return -1;
        if(!videoTrackToID.TryGetValue(trackID, out uint internalID))
        {
            return -1;
        }
        return WebRTCInvoker.send_track_frame(connectionPtr, data, size, internalID, frameNr); 
    }

    private string convertTracksToString(Dictionary<string, uint> tracks)
    {
        if (tracks == null || tracks.Count == 0)
            return string.Empty;

        var entries = new List<string>(tracks.Count);
        foreach (var kvp in tracks)
        {
            entries.Add($"{kvp.Key}:{kvp.Value}");
        }
        return string.Join(";", entries);
    }

    public string VideoTracksString => convertTracksToString(videoTrackToID);
    public string AudioTracksString => convertTracksToString(audioTrackToID);
    protected override void disposeInternal()
    {
        
    }
}
