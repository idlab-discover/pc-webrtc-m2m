using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ColorDecoder : IDisposable
{
    const string NAME = "ColorDecoderUnity";
    public bool IsValid { get; private set; }
    private IntPtr ptr;
    private bool disposedValue;

    private readonly uint clientID;
    private readonly uint capturerID;
    public ColorDecoder(ColorCodecType codecType, uint _clientID, uint _capturerID)
    {
        clientID = _clientID;
        capturerID = _capturerID;
        Logger.LogStatusClientAndCapturer(NAME, Logger.Status.Creating, clientID, capturerID);

        ptr = RawInvoker.create_color_decoder(codecType);

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

    public IntPtr DecodeColor(IntPtr data, ulong size, uint width, uint height, uint frameNr)
    {
        Logger.LogPCFrameStatusLimited(NAME, Logger.Status.StartDecodingColor, clientID, capturerID, frameNr);
        if (!IsValid)
        {
            return IntPtr.Zero;
        }
        IntPtr d = RawInvoker.decode_color(ptr, data, size, width, height);
        Logger.LogPCFrameStatusLimited(NAME, Logger.Status.EndDecodingColor, clientID, capturerID, frameNr);
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
            if (IsValid)
            {
                RawInvoker.free_color_decoder(ptr);
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
