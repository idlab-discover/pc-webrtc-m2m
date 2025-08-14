using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public abstract class NetworkSenderBase : IDisposable
{
    private bool disposedValue;
    public bool IsValid { get; protected set; }
    private readonly object _lock = new();


    public abstract int SendVideoData(string trackID, IntPtr data, uint size);
    public abstract int SendAudioData(IntPtr data, uint size);

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
