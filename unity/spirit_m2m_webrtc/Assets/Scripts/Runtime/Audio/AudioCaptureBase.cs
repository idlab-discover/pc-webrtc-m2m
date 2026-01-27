using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public abstract class AudioCaptureBase : IDisposable
{
    private bool disposedValue;

    protected abstract void Dispose(bool disposing);

    public void Dispose()
    {
        // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
        Dispose(disposing: true);
        disposedValue = true;
        GC.SuppressFinalize(this);
    }
}
