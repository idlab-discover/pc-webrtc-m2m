using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;

public class EncodedMDCDescription : IDisposable
{
    private IntPtr descriptionPtr;
    public MDCFrameHeader Header;

    private bool hasCopied = false;
    private byte[] rawData = null;
    private bool disposedValue;

    public EncodedMDCDescription(
        IntPtr _descriptionPtr,
        IntPtr dataBuffer,
        uint dataBufferSize,
        ulong timestamp,
        uint capturerID,
        uint frameNr,
        uint descriptionNr,
        uint codecType,
        uint totalNumberOfPoints
        )
    {
        descriptionPtr= _descriptionPtr;
        Header = new MDCFrameHeader(
            dataBuffer,
            dataBufferSize,
            timestamp,
            capturerID,
            frameNr,
            descriptionNr,
            codecType,
            totalNumberOfPoints);
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
        byte[] frameHeader = Header.Bytes;
        rawData = new byte[Header.DataBufferSize + MDCFrameHeader.HEADER_SIZE];
        System.Buffer.BlockCopy(frameHeader, 0, rawData, 0, frameHeader.Length);
        Marshal.Copy(Header.DataBuffer, rawData, frameHeader.Length, (int)Header.DataBufferSize);
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
