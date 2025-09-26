using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public abstract class NetworkFrame : IDisposable
{
    private bool disposedValue;
    public IntPtr DataPtr { get; protected set; }
    public uint Size { get; protected set; }

    protected virtual void Dispose(bool disposing)
    {
        if (!disposedValue)
        {
            if (disposing)
            {
                disposeInternal();
            }

            // TODO: free unmanaged resources (unmanaged objects) and override finalizer
            // TODO: set large fields to null
            disposedValue = true;
        }
    }

    protected abstract void disposeInternal();

    public void Dispose()
    {
        // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }
}