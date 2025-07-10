using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DepthDecoder : IDisposable
{
    const string NAME = "DepthDecoderUnity";
    public bool IsValid { get; private set; }
    private IntPtr ptr;
    private bool disposedValue;

    public readonly uint ClientID;
    public DepthDecoder(DepthCodecType codecType, uint clientID)
    {
        ClientID = clientID;
        Logger.LogStatusClient(NAME, Logger.Status.Creating, ClientID);

        ptr = RawInvoker.create_depth_decoder(codecType);

        if (ptr == IntPtr.Zero)
        {
            Logger.LogStatusClient(NAME, Logger.Status.Failed, ClientID);
            IsValid = false;
        }
        else
        {
            Logger.LogStatusClient(NAME, Logger.Status.Created, ClientID);
            IsValid = true;
        }

    }

    public IntPtr DecodeDepth(IntPtr data, uint width, uint height)
    {
        if (!IsValid)
        {
            return IntPtr.Zero;
        }
        return RawInvoker.decode_depth(ptr, data, width, height);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!disposedValue)
        {
            if (disposing)
            {
                // TODO: dispose managed state (managed objects)
            }
            if(IsValid)
            {
                RawInvoker.free_depth_decoder(ptr);
            }
            disposedValue = true;
        }
    }



    public void Dispose()
    {
        // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }
}
