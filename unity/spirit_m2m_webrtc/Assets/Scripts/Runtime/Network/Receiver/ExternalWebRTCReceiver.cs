using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

public class ExternalWebRTCReceiver : NetworkReceiverBase
{
    private IntPtr clientPtr;
    private Dictionary<string, IntPtr> internalTracks = new();
    public ExternalWebRTCReceiver(IntPtr clientPtr)
    {
        this.clientPtr = clientPtr;
    }
    protected override void disposeInternal()
    {
        // TODO Dispose of clientPtr and tracks
    }

    protected override void pollAudioTrackInternal(uint clientID, OnStreamDataReceivedCb cb)
    {
        throw new System.NotImplementedException();
    }

    protected override void pollVideoTrackInternal(uint clientID, string trackID, OnStreamDataReceivedCb cb)
    {
        bool keepWorking = true;
        bool exists = internalTracks.TryGetValue(trackID, out IntPtr internalTrack);
        if(!exists)
        {
            Logger.LogStatusWithMessage("", Logger.Status.ClientTrackNotFound, $"trackID={trackID}");
            return;
        }
        while (keepWorking)
        {
            IntPtr frame = WebRTCInvoker.get_next_frame_for_track(internalTrack);
            if(frame == null)
            {
                keepWorking = false;
                continue;
            }
            NetworkFrameWebRTC networkFrame = new NetworkFrameWebRTC(frame);
            cb(networkFrame);
        }
    }

    public void AddTrack(ReceivingTrackInfo trackInfo)
    {
        // TODO Add track to internal dictionary
       // internalTrackIDs.Add(trackInfo.trackID, 0);
    }
}
