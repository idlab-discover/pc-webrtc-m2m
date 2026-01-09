using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;

using UnityEngine;



public abstract class NetworkReceiverBase : IDisposable
{
    protected abstract string NAME { get; }
    public bool IsValid { get; protected set; }
    private bool disposedValue;
    protected readonly object _lock = new();
    private List<Thread> videoWorkerThreads = new();
    private Thread audioWorkerThread;
    public delegate void OnStreamDataReceivedCb(NetworkFrame receivedFrame);

    public void StartPollVideoTrack(uint clientID, string trackID, OnStreamDataReceivedCb cb)
    {
        Logger.LogStatusWithMessage(NAME, Logger.Status.ReceiverStartPollVideoTrack, $"clientID={clientID}, trackID={trackID}");
        Thread worker;
        lock (_lock)
        {
            worker = new Thread(() =>
            {
                pollVideoTrackInternal(clientID, trackID, cb);
            });
            videoWorkerThreads.Add(worker); // TODO Change this into map or something to make it easier to enable/disable poll of specific tracks
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

    protected abstract void pollVideoTrackInternal(uint clientID, string trackID, OnStreamDataReceivedCb cb);
    protected abstract void pollAudioTrackInternal(uint clientID, OnStreamDataReceivedCb cb);

    protected abstract void disposeInternal();
    protected virtual void Dispose(bool disposing)
    {
        lock (_lock)
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
