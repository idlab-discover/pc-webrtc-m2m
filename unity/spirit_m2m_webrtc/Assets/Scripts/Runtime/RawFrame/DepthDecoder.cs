using FMODUnity;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DepthDecoder : IDisposable
{
    const string NAME = "DepthDecoderUnity";
    public bool IsValid { get; private set; }
    public readonly DepthCodecType CodecType;
    private IntPtr ptr;
    private bool disposedValue;

    private readonly uint clientID;
    private readonly uint capturerID;
    public DepthDecoder(DepthCodecType codecType, uint _clientID, uint _capturerID)
    {
        CodecType = codecType;
        clientID = _clientID;
        capturerID = _capturerID;
        Logger.LogStatusClientAndCapturer(NAME, Logger.Status.Creating, clientID, capturerID);

        ptr = RawInvoker.create_depth_decoder(codecType);

        if (ptr == IntPtr.Zero)
        {
            Logger.LogStatusClientAndCapturer(NAME, Logger.Status.Failed, clientID, capturerID);
            IsValid = false;
        }
        else
        {
            Logger.LogStatusClientAndCapturer(NAME, Logger.Status.Created, clientID, capturerID);
            IsValid = true;
        }

    }

    public IntPtr DecodeDepth(IntPtr data, uint width, uint height, uint frameNr)
    {
        Logger.LogPCFrameStatusLimited(NAME, Logger.Status.StartDecodingDepth, clientID, capturerID, frameNr);
        if (!IsValid)
        {
            return IntPtr.Zero;
        }
        IntPtr d = RawInvoker.decode_depth(ptr, data, width, height);
        Logger.LogPCFrameStatusLimited(NAME, Logger.Status.EndDecodingDepth, clientID, capturerID, frameNr);
        return d;
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
