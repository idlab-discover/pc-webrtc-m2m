using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using Unity.VisualScripting;
using UnityEngine;

public abstract class NetworkStreamerBase : IDisposable
{
    protected abstract class NetworkStreamerClientBase {
        private NetworkStreamerBase parent;
        private List<Thread> videoWorkerThreads = new();
        private Thread audioWorkerThread;
        private readonly object _lock = new();

        public uint ClientID;

        public NetworkStreamerClientBase(uint clientID)
        {
            ClientID = clientID;
        }

        public void StartPollVideoTrack(uint clientID, uint capturerID, uint descriptionID, OnStreamDataReceivedCb cb)
        {
            Thread worker;
            lock (_lock)
            {
                worker = new Thread(() =>
                {
                    pollVideoTrackInternal(clientID, capturerID, descriptionID, cb);
                });
                videoWorkerThreads.Add(worker);
            }
            worker.Start();
        }
        
        public void StartPollAudioTrack(uint clientID, OnStreamDataReceivedCb cb)
        {
            audioWorkerThread = new Thread(() =>
            {
                pollAudioTrackInternal(clientID, cb);
            });
            audioWorkerThread.Start();
        }
        protected abstract void pollVideoTrackInternal(uint clientID, uint capturerID, uint descriptionID, OnStreamDataReceivedCb cb);
        protected abstract void pollAudioTrackInternal(uint clientID, OnStreamDataReceivedCb cb);
    }

    protected Dictionary<uint, NetworkStreamerClientBase> clients;
    private bool disposedValue;
    protected readonly object _lock = new();
    public delegate void OnStreamDataReceivedCb(IntPtr data, uint size);

    public abstract bool IsReady();
    // Callback into byte array
    public void AddNewClient(uint clientID)
    {
        lock(_lock)
        {
            bool succes = clients.ContainsKey(clientID);
            if (succes)
            {
                clients.Remove(clientID);
            }
            clients.Add(clientID, createNewClient(clientID));
        }

    }
    public void RemoveNewClient(uint clientID)
    {
        lock(_lock)
        {
            clients.Remove(clientID);
        }
        
    }
    public void StartPollVideoTrack(uint clientID, uint capturerID, uint descriptionID, OnStreamDataReceivedCb cb) {
        lock(_lock)
        {
            bool succes = clients.TryGetValue(clientID, out NetworkStreamerClientBase client);
            if (!succes)
            {
                Debug.Log("[ERROR]: Add client before polling");
            }
            client.StartPollVideoTrack(clientID, capturerID, descriptionID, cb);
        }
        
    }
    public void StartPollAudioTrack(uint clientID, OnStreamDataReceivedCb cb)
    {
        lock (_lock)
        {
            bool succes = clients.TryGetValue(clientID, out NetworkStreamerClientBase client);
            if (!succes)
            {
                Debug.Log("[ERROR]: Add client before polling");
            }
            client.StartPollAudioTrack(clientID, cb);
        }
    }
    
    protected abstract NetworkStreamerClientBase createNewClient(uint clientID);

    public abstract int SendVideoData(IntPtr data, uint size, uint capturerID, uint descriptionID);
    public abstract int SendAudioData(IntPtr data, uint size);

    protected abstract void disposeInternal();
    protected virtual void Dispose(bool disposing)
    {
        lock(_lock)
        {
            if (!disposedValue)
            {
                disposeInternal();
                
                disposedValue = true;
            }
        }
        
    }

    

    public void Dispose()
    {
        // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }
}
