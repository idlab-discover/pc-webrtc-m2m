using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

public class LoopbackReceiver : NetworkReceiverBase
{
    protected override string NAME => "LoopbackReceiver";
    public readonly uint ClientID;
    private readonly LoopbackProvider provider;
    private bool keepWorking = true;
    private readonly LoopbackProvider.LoopbackTrack audioTrack = new();
    private readonly Dictionary<string, LoopbackProvider.LoopbackTrack> videoTracks = new();
    public LoopbackReceiver(string providerID, uint clientID)
    {
        ConnectionProviderBase c = ConnectionProviderRepository.GetProvider(providerID);
        if(c == null )
        {
            // Error logging
            IsValid = false;
            return;
        }
        ClientID = clientID;
        provider = (LoopbackProvider)c; // Can maybe implement safe casting here in the future
        provider.AddReceiver(this);
        IsValid = true;
    }

   

    public void SetAudioTrackData(IntPtr data, uint size)
    {
        lock (_lock)
        {
            audioTrack.SetFrame(data, size);
        }
    }
    public void SetVideoTrackData(string trackID, IntPtr data, uint size)
    {
        lock (_lock)
        {
            bool succes = videoTracks.TryGetValue(trackID, out var t);
            Debug.Log($"LoopbackReceiver: Setting video track data for trackID {trackID}, success: {succes}");
            if (!succes)
            {
                return;
            }
            t.SetFrame(data, size);
        }
    }
    public void StopListening()
    {
        lock (_lock)
        {
            keepWorking = false;
            audioTrack.StopTrack();
            foreach (var (_, track) in videoTracks)
            {
                track.StopTrack();
            }
        }

    }
    protected override void pollAudioTrackInternal(uint clientID, OnStreamDataReceivedCb cb)
    {
        while (keepWorking)
        {
            lock (audioTrack._lock)
            {
                while (!audioTrack.NextFrameReady)
                {
                    Monitor.Wait(audioTrack._lock);
                }
                if (keepWorking)
                {
                    cb(audioTrack.NextFrame);
                }
            }
        }
    }

    protected override void pollVideoTrackInternal(uint clientID, string trackID, OnStreamDataReceivedCb cb)
    {
        LoopbackProvider.LoopbackTrack t;
        lock (_lock)
        {
            if (videoTracks.ContainsKey(trackID))
            {
                return;
            }
            t = new();
            Debug.Log($"LoopbackReceiver: Created new LoopbackTrack for trackID {trackID}");
            videoTracks.Add(trackID, t);
        }
        while (keepWorking)
        {
            lock (t._lock)
            {
                while (!t.NextFrameReady)
                {
                    
                    Monitor.Wait(t._lock);
                }
                
                t.NextFrameReady = false; // Reset the flag
                if (keepWorking)
                {
                    cb(t.NextFrame);
                }
            }
        }
    }

    protected override void disposeInternal()
    {
        StopListening();
        provider.RemoveReceiver(ClientID);
    }

}
