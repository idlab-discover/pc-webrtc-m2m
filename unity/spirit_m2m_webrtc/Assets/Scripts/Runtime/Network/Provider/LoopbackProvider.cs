using Newtonsoft.Json.Linq;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using UnityEditor.PackageManager;
using UnityEngine;
using UnityEngine.Rendering.Universal;


[ConnectionProviderRegister("Loopback")]
public class LoopbackProvider : ConnectionProviderBase
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
    
    private readonly object _lock = new object();
    private Dictionary<uint, LoopbackReceiver> receivers; // In general you dont want to do this but we need it to implement the loopback mechanism

    protected override string NAME => throw new NotImplementedException();

    public LoopbackProvider(string ID, JObject jsonSettings) : base(ID, jsonSettings) { 
        
    }

    public override bool IsReady() => true;

    public void AddReceiver(LoopbackReceiver receiver)
    {
        lock(_lock)
        {
            receivers[receiver.ClientID] = receiver;
        }
    }
    public void RemoveReceiver(uint clientID)
    {
        lock(_lock)
        {
            receivers.Remove(clientID);
        }
    }
    public void SendVideoToAllClients(IntPtr data, uint size, uint capturerID, uint descriptionID)
    {
        lock (_lock)
        {
            foreach (var r in receivers.Values)
            {
                r.SetVideoTrackData(data, size, capturerID, descriptionID);
            }
        }
    }
    public void SendAudioToAllClients(IntPtr data, uint size)
    {
        lock (_lock)
        {
            foreach (var r in receivers.Values)
            {
                r.SetAudioTrackData(data, size);
            }
        }
    }

    protected override void Dispose(bool disposedValue) { }

    protected override void connectInternal()
    {
        IsConnected = true;
    }

    protected override void disconnectInternal()
    {
        IsConnected= false;
    }
}
