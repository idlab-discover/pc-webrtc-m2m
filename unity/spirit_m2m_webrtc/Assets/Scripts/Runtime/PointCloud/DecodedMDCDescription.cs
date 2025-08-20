using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DecodedMDCDescription : IDisposable
{
    private IntPtr decodedDesc;
    private MDCFrameHeader tempHeader;
    private bool disposeFrameAfterDecode = false;
    private bool disposedValue = false;
    public uint NumberOfPoints { 
        get 
        {
            if (decodedDesc == IntPtr.Zero) return 0;
            return DracoInvoker.get_n_points(decodedDesc);
        } 
    }
    public IntPtr PointPtr
    {
        get
        {
            if (decodedDesc == IntPtr.Zero) return IntPtr.Zero;
            return DracoInvoker.get_point_array(decodedDesc);
        }
    }
    public IntPtr ColorPtr
    {
        get {
            if(decodedDesc == IntPtr.Zero) return IntPtr.Zero;
            return DracoInvoker.get_color_array(decodedDesc);
        }
    }

  

    public DecodedMDCDescription(MDCFrameHeader header, bool decodeImmediately)
    {
        tempHeader = header;
        if (decodeImmediately)
        {
            DecodeDescription(header);
        }
            
    }
    public void DecodeDescription(MDCFrameHeader header)
    {
        decodedDesc = DracoInvoker.decode_pc(header.DataBuffer, header.DataBufferSize);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!disposedValue)
        {
            if (disposing)
            {
                // TODO: dispose managed state (managed objects)
            }

            // TODO: free unmanaged resources (unmanaged objects) and override finalizer
            // TODO: set large fields to null
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
