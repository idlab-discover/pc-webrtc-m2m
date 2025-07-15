using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using UnityEngine.Rendering.Universal;


[StreamerRegister("Loopback")]
public class LoopbackStreamer : NetworkStreamerBase
{
    public struct LoopbackFrame
    {
        public IntPtr NextPtr;
        public uint NextSize;
    }
    public class LoopbackTrack
    {
        public LoopbackFrame NextFrame;
        public readonly object _lock = new object();
        public bool NextFrameReady;
        public void SetFrame(IntPtr data, uint size)
        {
            lock(_lock) { 
                NextFrame = new LoopbackFrame{ NextPtr = data, NextSize = size };
            }
        }
        public void StopTrack()
        {
            lock(_lock)
            {
                Monitor.Pulse(_lock);
            }
        }
    }
    protected class LoopbackStreamerClient : NetworkStreamerClientBase
    {
        public readonly object _lock = new object();
        private bool keepWorking = true;
        private readonly LoopbackTrack audioTrack = new();
        private readonly Dictionary<(uint, uint), LoopbackTrack> videoTracks = new();

        public LoopbackStreamerClient(uint clientID) : base(clientID)
        {
        }
        public void SetAudioTrackData(IntPtr data, uint size)
        {
            lock(_lock)
            {
               audioTrack.SetFrame(data, size);
            }
        }
        public void SetVideoTrackData(IntPtr data, uint size, uint capturerID, uint descriptionID)
        {
            lock(_lock)
            {
                bool succes = videoTracks.TryGetValue((capturerID, descriptionID), out var t);
                if(!succes)
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
                foreach(var (_, track) in videoTracks)
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
                    while(!audioTrack.NextFrameReady)
                    {
                        Monitor.Wait(audioTrack._lock);
                    }
                    if(keepWorking)
                    {
                        cb(audioTrack.NextFrame.NextPtr, audioTrack.NextFrame.NextSize);
                    }
                }
            }
        }

        protected override void pollVideoTrackInternal(uint clientID, uint capturerID, uint descriptionID, OnStreamDataReceivedCb cb)
        {
            LoopbackTrack t;
            lock (_lock)
            {
                if(videoTracks.ContainsKey((capturerID, descriptionID)))
                {
                    return;
                }
                t = new();
                videoTracks.Add((capturerID, descriptionID), t);
            }
            while(keepWorking)
            {
                lock (t._lock)
                {
                    while (!t.NextFrameReady)
                    {
                        Monitor.Wait(t._lock);
                    }
                    if (keepWorking)
                    {
                        cb(t.NextFrame.NextPtr, t.NextFrame.NextSize);
                    }
                }
            }
        }
    }
    public override bool IsReady()
    {
        return true;
    }

    public override int SendAudioData(IntPtr data, uint size)
    {
        lock(_lock)
        {
            foreach(var (_, c) in clients)
            {
               ((LoopbackStreamerClient) c).SetAudioTrackData(data, size);
            }
        }
        return (int)size;
    }

    public override int SendVideoData(IntPtr data, uint size, uint capturerID, uint descriptionID)
    {
        lock (_lock)
        {
            foreach (var (_, c) in clients)
            {
                ((LoopbackStreamerClient)c).SetVideoTrackData(data, size, capturerID, descriptionID);
            }
        }
        return (int)size;
    }

    protected override NetworkStreamerClientBase createNewClient(uint clientID)
    {
        return new LoopbackStreamerClient(clientID);
    }

    protected override void disposeInternal()
    {
        foreach (var (_, c) in clients)
        {
            ((LoopbackStreamerClient) c).StopListening();
        }
    }
}
