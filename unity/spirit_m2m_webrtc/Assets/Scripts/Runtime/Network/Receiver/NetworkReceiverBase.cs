using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using UnityEditor.PackageManager;
using UnityEngine;

public abstract class NetworkReceiverBase : IDisposable
{
    public bool IsValid { get; protected set; }
    private bool disposedValue;
    protected readonly object _lock = new();
    private List<Thread> videoWorkerThreads = new();
    private Thread audioWorkerThread;
    public delegate void OnStreamDataReceivedCb(IntPtr data, uint size);

    public void StartPollVideoTrack(uint clientID, uint capturerID, uint descriptionID, OnStreamDataReceivedCb cb)
    {
        Thread worker;
        lock (_lock)
        {
            worker = new Thread(() =>
            {
                pollVideoTrackInternal(clientID, capturerID, descriptionID, cb);
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

    protected abstract void pollVideoTrackInternal(uint clientID, uint capturerID, uint descriptionID, OnStreamDataReceivedCb cb);
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
