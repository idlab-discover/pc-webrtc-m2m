using Newtonsoft.Json.Linq;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;

using UnityEngine;



[ConnectionProviderRegister("Loopback")]
public class LoopbackProvider : ConnectionProviderBase, ISenderSupported, IReceiverSupported
{
    protected override string NAME => "LoopbackProvider";
   
    public class LoopbackFrame : NetworkFrame
    {
        public LoopbackFrame(IntPtr dataPtr, uint size)
        {
            DataPtr = dataPtr;
            Size = size;
        }

        protected override void disposeInternal()
        {
            
        }
    }
    public class LoopbackTrack
    {
        public LoopbackFrame NextFrame;
        public readonly object _lock = new object();
        public bool NextFrameReady;
        public void SetFrame(IntPtr data, uint size)
        {
            lock(_lock) {
                NextFrame = new LoopbackFrame(data, size);
                NextFrameReady = true;  
                Monitor.Pulse(_lock);
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
    private Dictionary<string, List<(uint, string)>> subscribedClients = new();
    public uint LocalClientID;

    public LoopbackProvider(LocalConnectedClient localClient, ClientAddedToProviderMessage pMsg) : base(localClient, pMsg) { 
        
    }

    public override bool IsReady() => true;

    public void AddLocalTracks(List<string> trackIDs)
    {
        lock(_lock)
        {
            foreach (string trackID in trackIDs)
            {
                subscribedClients[trackID] = new List<(uint, string)>();
            }
        }
    }
    public void SubscribeClientToTrack(uint clientID, string trackID)
    {
        lock(_lock)
        {
            string originID = $"cl{LocalClientID}_{trackID.Substring(trackID.IndexOf("_") + 1)}";
            if (subscribedClients.TryGetValue(originID, out var clients))
            {
                clients.Add((clientID, trackID));
            }
        }
    }

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
            List<(uint, string)> clients = subscribedClients[trackID];
            foreach ((uint id, string remoteTrackID) client in clients)
            {
                UnityEngine.Debug.Log($"LoopbackProvider: Sending video data for trackID {trackID} to clientID {client.id}");
                if (receivers.TryGetValue(client.id, out var receiver))
                {
                    receiver.SetVideoTrackData(client.remoteTrackID, data, size);
                }
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

    public NetworkReceiverBase GetReceiver(RemoteTrackInfo track, uint clientID)
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
    public NetworkReceiverBase GetReceiverForTrackList(List<RemoteTrackInfo> tracks, uint clientID)
    {
        return GetReceiver(null, clientID); // We dont need the track anyway for loopback
    }
}
