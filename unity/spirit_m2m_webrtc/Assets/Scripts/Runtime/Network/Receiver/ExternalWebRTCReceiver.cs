using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.UIElements;

public class ExternalWebRTCReceiver : NetworkReceiverBase
{
    protected override string NAME => "ExternalWebRTCReceiver";
    private IntPtr clientPtr;
    private Dictionary<string, IntPtr> internalTracks = new();
    public ExternalWebRTCReceiver(IntPtr clientPtr)
    {
        this.clientPtr = clientPtr;
        IsValid = clientPtr != IntPtr.Zero;
    }
    protected override void disposeInternal()
    {
        // TODO Dispose of clientPtr and tracks
        foreach(var t in internalTracks)
        {
            if(t.Value != IntPtr.Zero)
            {
                Logger.LogStatusWithMessage(NAME, Logger.Status.WebRTCStoppingTrack, $"trackID={t.Key}");
                WebRTCInvoker.stop_track(t.Value);
            }
        }
        WebRTCInvoker.free_client(clientPtr);
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
            Logger.LogStatusWithMessage(NAME, Logger.Status.ClientTrackNotFound, $"trackID={trackID}");
            return;
        }
        if(internalTrack == IntPtr.Zero)
        {
            Logger.LogStatusWithMessage(NAME, Logger.Status.IntPtrZero, $"func=pollVideoTrackInternal clientID={clientID}, trackID={trackID}");
            return;
        }
        Logger.LogStatusWithMessage(NAME, Logger.Status.DebugTest, $"func=pollVideoTrackInternal state=started clientID={clientID}, trackID={trackID}");
        while (keepWorking)
        {
            IntPtr frame = WebRTCInvoker.get_next_frame_for_track(internalTrack);
            if(frame == IntPtr.Zero)
            {
                Logger.LogStatusWithMessage(NAME, Logger.Status.DebugTest, $"func=pollVideoTrackInternal state=stopped clientID={clientID}, trackID={trackID}");
                keepWorking = false;
                continue;
            }
            
            ExternalWebRTCNetworkFrame networkFrame = new(frame);
            //Logger.LogStatusWithMessage(NAME, Logger.Status.DebugTest, $"func=pollVideoTrackInternal state=nFrame size={networkFrame.Size} clientID={clientID}, trackID={trackID}");
            cb(networkFrame);
        }
        Logger.LogStatusWithMessage(NAME, Logger.Status.DebugTest, $"func=pollVideoTrackInternal state=ended clientID={clientID}, trackID={trackID}");
    }

    public void AddTrack(RemoteTrackInfo trackInfo)
    {
       internalTracks.Add(trackInfo.trackID, WebRTCInvoker.add_track(clientPtr, trackInfo.trackID));
    }
    public void AddTracks(List<RemoteTrackInfo> trackInfos)
    {
        Logger.LogStatusWithMessage(NAME, Logger.Status.ReceiverAddVideoTracks, $"nTracks={trackInfos.Count}");
        string[] trackIDs = new string[trackInfos.Count];
        byte[] isVideo = new byte[trackInfos.Count];
        for (int i = 0; i < trackInfos.Count; i++)
        {
            trackIDs[i] = trackInfos[i].trackID;
            isVideo[i] = trackInfos[i].isVideo ? (byte)1 : (byte)0;
        }
        Logger.LogStatus(NAME, Logger.Status.DebugTest);
        IntPtr internalPtrs = IntPtr.Zero;
        try 
        {
            internalPtrs = WebRTCInvoker.add_tracks(clientPtr, trackIDs, isVideo, (uint)trackIDs.Length); // This will automatically cause the peer to subscribe to them
        }
        catch (Exception e)
        {
            Logger.LogStatusWithMessage(NAME, Logger.Status.DebugTest, $"Exception while logging track IDs: {e.Message}");
            return;
        }
        if(internalPtrs == IntPtr.Zero)
        {
            Logger.LogStatusWithMessage(NAME, Logger.Status.IntPtrZero, $"func=AddTracks validClientPtr={clientPtr != IntPtr.Zero}");
            return;
        }
        Logger.LogStatus(NAME, Logger.Status.DebugTest);
        for(int i = 0; i < trackInfos.Count; i++)
        {
            IntPtr trackPtr = Marshal.ReadIntPtr(internalPtrs, i * IntPtr.Size);
            internalTracks.Add(trackInfos[i].trackID, trackPtr);
            Logger.LogStatusWithMessage(NAME, Logger.Status.ReceiverAdddedInternalVideoTrack, $"i={i} trackID={trackInfos[i].trackID} validPtr={trackPtr != IntPtr.Zero}");
        }
        foreach(var t in trackInfos)
        {
            t.SetReceiver(this);
        }
        Logger.LogStatus(NAME, Logger.Status.ReceiverAddedVideoTracks);
    }
}
