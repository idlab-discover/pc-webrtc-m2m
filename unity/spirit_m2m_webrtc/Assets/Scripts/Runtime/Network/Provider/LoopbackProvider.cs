using Newtonsoft.Json.Linq;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;



[ConnectionProviderRegister("Loopback")]
public class LoopbackProvider : ConnectionProviderBase, ISenderSupported, IReceiverSupported
{
    protected override string NAME => "LoopbackProvider";
   
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
    private LoopbackSender sender;
    private Dictionary<uint, LoopbackReceiver> receivers = new(); // In general you dont want to do this but we need it to implement the loopback mechanism



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
    public void SendVideoToAllClients(string trackID, IntPtr data, uint size)
    {
        lock (_lock)
        {
            foreach (var r in receivers.Values)
            {
                r.SetVideoTrackData(trackID, data, size);
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

    public NetworkSenderBase GetSender(ReceivingTrackInfo track)
    {
        if(sender == null)
        {
            sender = new LoopbackSender(this.ID);
        }
        return sender;
    }

    public NetworkReceiverBase GetReceiver(ReceivingTrackInfo track, uint clientID)
    {
        if(receivers.TryGetValue(clientID, out var receiver))
        {
            return receiver;
        }
        else
        {
            receiver = new LoopbackReceiver(this.ID, clientID);
            lock(_lock)
            {
                receivers[clientID] = receiver;
            }
            return receiver;
        }
    }
}
