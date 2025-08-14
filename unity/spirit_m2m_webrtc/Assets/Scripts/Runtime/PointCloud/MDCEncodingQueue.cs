using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

using UnityEngine;


public class MDCEncodingQueue : IDisposable
{
    const string NAME = "MDCEncodingQueue";
    public delegate void mdcDescriptionDoneCallback(MDCDescription desc);
    private mdcDescriptionDoneCallback descriptionDoneCallback;
    public bool IsValid { get; private set; }
    private IntPtr ptr;
    private GCHandle descriptionDoneHandle;
    private GCHandle freePointCloudHandle;
    private bool disposedValue;

    public MDCEncodingQueue(SessionInfo sessionInfo)
    {
        Logger.LogStatus(NAME, Logger.Status.Creating);

        ptr = DracoInvoker.create_encoding_queue(2);

        if(ptr == IntPtr.Zero)
        {
            Logger.Log($"id={NAME} ts={Logger.Time} status={Logger.Status.Failed}");
            IsValid = false;
        } else
        {
            Logger.Log($"id={NAME} ts={Logger.Time} status={Logger.Status.Created}");
            IsValid = true;
        }
        if (IsValid)
        {
            setFreePointCloudCallback(OnFreePCCallback);
            setLocalDescriptionDoneCallback(OnDescriptionDoneCallback);
        }
    }

    // This adds the pc to the encoding queue
    public void EncodePointCloud(IntPtr pc)
    {
        // TODO check if callbacks are set, if not cancel and return error code
        if (!IsValid)
        {
            Debug.Log($"{NAME}: Ptr is invalid");
            return;
        }
        DracoInvoker.encode_pc(ptr, pc);
    }
    public void SetDescriptionDoneCallback(mdcDescriptionDoneCallback cb)
    {
        descriptionDoneCallback = cb;
    }
    // TODO check if handle already allocated, if so free it first
    private void setLocalDescriptionDoneCallback(DracoInvoker.descriptionDoneCallback cb)
    {
        if (!IsValid)
        {
            Debug.Log($"{NAME}: Ptr is invalid");
            return;
        }

        IntPtr f = Marshal.GetFunctionPointerForDelegate(cb);
        descriptionDoneHandle = GCHandle.Alloc(cb, GCHandleType.Normal);
        DracoInvoker.register_description_done_callback(ptr, cb);
    }

    private void setFreePointCloudCallback(DracoInvoker.freePCCallback cb)
    {
        if (!IsValid)
        {
            Debug.Log($"{NAME}: Ptr is invalid");
            return;
        }

        IntPtr f = Marshal.GetFunctionPointerForDelegate(cb);
        freePointCloudHandle = GCHandle.Alloc(cb, GCHandleType.Normal);
        DracoInvoker.register_free_pc_callback(ptr, cb);
    }

    private void OnDescriptionDoneCallback(IntPtr dsc, IntPtr rawDataPtr, UInt32 totalPointsInCloud, UInt32 dscSize, UInt32 capturerID, UInt32 frameNr, UInt32 dscNr, UInt64 timestamp)
    {
        MDCDescription desc = new(
            dsc,
            rawDataPtr,
            dscSize,
            timestamp,
            capturerID,
            frameNr,
            dscNr,
            (uint)FrameCodec.Draco,
            totalPointsInCloud
        );
        descriptionDoneCallback(desc);
    }

    private void OnFreePCCallback(IntPtr pc)
    {
        // TODO move this to capture free callback
        Realsense2Invoker.free_point_cloud(pc);
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
                DracoInvoker.free_encoding_queue(ptr);
                ptr = IntPtr.Zero;
                if (descriptionDoneHandle.IsAllocated)
                {
                    descriptionDoneHandle.Free();
                }
                if (freePointCloudHandle.IsAllocated)
                {
                    freePointCloudHandle.Free();
                }
                IsValid = false;
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
