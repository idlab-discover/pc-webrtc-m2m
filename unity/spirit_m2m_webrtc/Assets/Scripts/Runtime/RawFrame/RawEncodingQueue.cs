using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;

using UnityEngine;


public class RawEncodingQueue : IDisposable
{
    const string NAME = "RawEncodingQueue";
    public bool IsValid { get; private set; }
    private IntPtr ptr;
    private GCHandle colorDoneHandle;
    private GCHandle depthDoneHandle;
    private GCHandle freeFrameHandle;
    private bool disposedValue;

    public RawEncodingQueue(SessionInfo sessionInfo, uint camWidth, uint camHeight, uint numberOfCapturers)
    {
        Logger.Log($"id={NAME} ts={Logger.Time} status={Logger.Status.Creating}");

        var cCodec = ColorCodecHelper.GetCodecSettings(sessionInfo.rawEncodingSettings);
        var dCodec = DepthCodecHelper.GetCodecSettings(sessionInfo.rawEncodingSettings);

        ptr = RawInvoker.create_encoding_queue(camWidth, camHeight,
              sessionInfo.rawEncodingSettings.maxQueueSize, sessionInfo.rawEncodingSettings.nWorkers, numberOfCapturers,
              cCodec.CodecType, cCodec.SettingsPtr, dCodec.CodecType, dCodec.SettingsPtr
        );

        if(ptr == IntPtr.Zero)
        {
            Logger.Log($"id={NAME} ts={Logger.Time} status={Logger.Status.Failed}");
            IsValid = false;
        } else
        {
            Logger.Log($"id={NAME} ts={Logger.Time} status={Logger.Status.Created}");
            IsValid = true;
        }
        
    }

    // This adds the frame to the encoding queue
    public void EncodeFrame(IntPtr frame)
    {
        if (!IsValid)
        {
            Debug.Log($"{NAME}: Ptr is invalid");
            return;
        }
        EncodeRawFrame(Realsense2Invoker.convert_frame_to_raw_frame(frame));
    }

    public void SetColorDoneCallback(RawInvoker.colorDoneCallback cb)
    {
        if (!IsValid)
        {
            Debug.Log($"{NAME}: Ptr is invalid");
            return;
        }

        IntPtr f = Marshal.GetFunctionPointerForDelegate(cb);
        colorDoneHandle = GCHandle.Alloc(cb, GCHandleType.Normal);
        RawInvoker.register_color_done_callback(ptr, f);
    }

    public void SetDepthDoneCallback(RawInvoker.depthDoneCallback cb)
    {
        if (!IsValid)
        {
            Debug.Log($"{NAME}: Ptr is invalid");
            return;
        }

        IntPtr f = Marshal.GetFunctionPointerForDelegate(cb);
        depthDoneHandle = GCHandle.Alloc(cb, GCHandleType.Normal);
        RawInvoker.register_depth_done_callback(ptr, f);

    }

    public void SetFreeFrameCallback(RawInvoker.freeFrameCallback cb)
    {
        if (!IsValid)
        {
            Debug.Log($"{NAME}: Ptr is invalid");
            return;
        }

        IntPtr f = Marshal.GetFunctionPointerForDelegate(cb);
        depthDoneHandle = GCHandle.Alloc(cb, GCHandleType.Normal);
        RawInvoker.register_free_frame_callback(ptr, f);
    }

    // This adds the frame to the encoding queue
    public void EncodeRawFrame(IntPtr rawFrame)
    {
        if (!IsValid)
        {
            Debug.Log($"{NAME}: Ptr is invalid");
            return;
        }

        RawInvoker.encode_frame(ptr, rawFrame);
    }

    protected virtual void Dispose(bool disposing)
    {
        Logger.Log($"id={NAME} ts={Logger.Time} status={Logger.Status.Disposing}");
        if (!disposedValue)
        {
            if (disposing)
            {
                // TODO: dispose managed state (managed objects)
            }
            if(IsValid)
            {
                RawInvoker.free_encoding_queue(ptr);
                ptr = IntPtr.Zero;
                if (colorDoneHandle.IsAllocated)
                {
                    colorDoneHandle.Free();
                }
                if (depthDoneHandle.IsAllocated)
                {
                    depthDoneHandle.Free();
                }
                if (freeFrameHandle.IsAllocated)
                {
                    freeFrameHandle.Free();
                }
            }
            
            disposedValue = true;
        }
        Logger.Log($"id={NAME} ts={Logger.Time} status={Logger.Status.Disposed}");
    }

    public void Dispose()
    {
        // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }
}
