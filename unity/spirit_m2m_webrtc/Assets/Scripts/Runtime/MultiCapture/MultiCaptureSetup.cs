using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.UIElements;



public class MultiCaptureSetup : IDisposable
{
    public uint NumberOfCapturers { get { return (uint)capturers.Count; } }
    public CaptureType CapType { get; private set; }
    protected bool disposedValue;
    private IntPtr multiCapPtr;
    private List<MultiCaptureSingleCam> capturers;
    private GCHandle frameReadyCbHandle;
    private bool isUsingCaptureThread = false;


    public MultiCaptureSetup(uint fps, FrameMode frameMode, FrameCleanupSettingsEx frameCleanupSettings, List<MultiCaptureSingleCam> capturers, bool startCaptureThread)
    {
        this.isUsingCaptureThread = startCaptureThread;
        this.capturers = capturers;
        CapType = capturers[0].CaptureType;
        IntPtr[] settingPtrs = new IntPtr[capturers.Count];
        for(int i = 0; i < capturers.Count; i++)
        {
            settingPtrs[i] = capturers[i].GetHelperSettingsPtr();
        }
        GCHandle settingPtrsHandle = GCHandle.Alloc(settingPtrs, GCHandleType.Pinned);
        multiCapPtr = Realsense2Invoker.create_new_multi_capturer(fps, frameMode, frameCleanupSettings, CapType, (uint)this.capturers.Count, settingPtrs);
        if (multiCapPtr != IntPtr.Zero)
        {
            Realsense2Invoker.start_capturing_multi(multiCapPtr, isUsingCaptureThread);
        } else
        {
            Debug.LogError("Failed to create MultiCaptureSetup");
            throw new Exception("Failed to create MultiCaptureSetup");
        }
            for (int i = 0; i < capturers.Count; i++)
            {
                capturers[i].Init(multiCapPtr, (uint)i);
                capturers[i].FreeHelperSettingsHandle();
            }
        settingPtrsHandle.Free();
    }

    public IntPtr PollNextPointCloud()
    {
        return Realsense2Invoker.poll_next_combined_point_cloud(multiCapPtr);
    }
    public IntPtr GetSingleCombinedPointCloud()
    {
        return Realsense2Invoker.get_single_combined_point_cloud(multiCapPtr);
    }
    public void SetFrameReadyCallback(Realsense2Invoker.frameReadyCallback cb)
    {
        IntPtr f = Marshal.GetFunctionPointerForDelegate(cb);
        frameReadyCbHandle = GCHandle.Alloc(cb, GCHandleType.Normal);
        for(uint i = 0; i < capturers.Count; i++)
        {
            Realsense2Invoker.register_frame_ready_callback_for_capturer(multiCapPtr, i, f);
        }
        
    }
   
    public IntPtr GetCalibrationForCapturer(uint capturerID)
    {
        return Realsense2Invoker.get_calibration_for_capturer(multiCapPtr, capturerID);
    }

    #region IDispose code
    protected virtual void Dispose(bool disposing)
    {
        if (!disposedValue)
        {
            if (disposing)
            {
                if(multiCapPtr != IntPtr.Zero)
                {
                    Realsense2Invoker.free_multi_capturer(multiCapPtr);
                    foreach(var capturer in capturers)
                    {
                        capturer.Dispose();
                    }
                    multiCapPtr = IntPtr.Zero;
                }
                if(frameReadyCbHandle.IsAllocated)
                {
                    frameReadyCbHandle.Free();
                }
                

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

    #endregion
}
