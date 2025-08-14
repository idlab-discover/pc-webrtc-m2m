using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;

public class MDCDescription : IDisposable
{
    private IntPtr descriptionPtr;
    public IntPtr DataBuffer { get; }
    public uint DataBufferSize { get; }
    public ulong Timestamp { get; }
    public uint CapturerID { get; }
    public uint FrameNr { get;  }
    public uint DescriptionNr { get;  }
    public uint CodecType { get; }
    public uint NumberOfPoints { get;  }

    private bool hasCopied = false;
    private byte[] rawData = null;
    private bool disposedValue;

    public MDCDescription(
        IntPtr _descriptionPtr,
        IntPtr dataBuffer,
        uint dataBufferSize,
        ulong timestamp,
        uint capturerID,
        uint frameNr,
        uint descriptionNr,
        uint codecType,
        uint numberOfPoints)
    {
        descriptionPtr= _descriptionPtr;
        DataBuffer = dataBuffer;
        DataBufferSize = dataBufferSize;
        Timestamp = timestamp;
        CapturerID = capturerID;
        FrameNr = frameNr;
        DescriptionNr = descriptionNr;
        CodecType = codecType;
        NumberOfPoints = numberOfPoints;
    }

    public byte[] Bytes { 
        get 
        {
            if(hasCopied)
            {
                    return rawData;
            }
            copyToBuffer();
            return rawData;
        }   
    }
    private void copyToBuffer()
    {
        byte[] frameHeader = new byte[24];
        var timestampField = BitConverter.GetBytes(Timestamp);
        timestampField.CopyTo(frameHeader, 0);
        var capturerIDField = BitConverter.GetBytes(CapturerID);
        capturerIDField.CopyTo(frameHeader, 8);
        var frameNrField = BitConverter.GetBytes(FrameNr);
        frameNrField.CopyTo(frameHeader, 12);
        var codecType = BitConverter.GetBytes(CodecType);
        codecType.CopyTo(frameHeader, 16);
        var nPointsFrameField = BitConverter.GetBytes(NumberOfPoints);
        nPointsFrameField.CopyTo(frameHeader, 20);
        rawData = new byte[frameHeader.Length + DataBufferSize];
        System.Buffer.BlockCopy(frameHeader, 0, rawData, 0, frameHeader.Length);
        Marshal.Copy(DataBuffer, rawData, frameHeader.Length, (int)DataBufferSize);
        hasCopied = true;
    }
    protected virtual void Dispose(bool disposing)
    {
        if (!disposedValue)
        {
            if (disposing)
            {
                DracoInvoker.free_description(descriptionPtr);
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
