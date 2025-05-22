using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.UIElements;

public enum CaptureType
{
    Artificial = 0,
    Realsense = 1,
    PrerecordedRealsense = 2,
    Kinect = 3,
    PrerecordedKinect = 4,
}

public abstract class SingleCapture : IDisposable
{
    private bool disposedValue;
    public CaptureType Type { get; }
    protected IntPtr capPtr { get; set; }
    public IntPtr CalibrationPtr { get { if (calibrationHandle.IsAllocated) return calibrationHandle.AddrOfPinnedObject(); else return IntPtr.Zero; } }
    private GCHandle calibrationHandle;
    protected SingleCapture(uint fps, FrameMode frameMode, FrameCleanupSettingsEx frameCleanupSettings, CaptureType capType, GCHandle captureSettings)
    {
        capPtr = Realsense2Invoker.create_new_capturer(fps, frameMode, frameCleanupSettings, capType, captureSettings.AddrOfPinnedObject());
        if (capPtr != IntPtr.Zero)
        {
            Realsense2Invoker.start_capturing(capPtr);
        }
        captureSettings.Free();
        Type = capType;
    }

    #region Poll functions
    public IntPtr PollNextRawFrame()
    {
        if(capPtr == IntPtr.Zero)
        {
            return IntPtr.Zero;
        }
        return Realsense2Invoker.poll_next_raw_frame(capPtr);
    }

    public IntPtr PollNextPointCloud()
    {
        if (capPtr == IntPtr.Zero)
        {
            return IntPtr.Zero;
        }
        return Realsense2Invoker.poll_next_point_cloud(capPtr);
    }

    public IntPtr PollNextFrame()
    {
        if (capPtr == IntPtr.Zero)
        {
            return IntPtr.Zero;
        }
        return Realsense2Invoker.poll_next_frame(capPtr);
    }
    #endregion

    public void SetCapturerFrameCleanupSettings(FrameCleanupSettingsEx cleanup_settings)
    {
        if(capPtr == IntPtr.Zero)
        {
            return;
        }
        Realsense2Invoker.set_capturer_frame_cleanup_settings(capPtr, cleanup_settings);
    }
    public CapturerIntrinsics GetColorIntrinsics()
    {
        return Realsense2Invoker.get_color_intrinsics(capPtr);
    }
    public CapturerIntrinsics GetDepthIntrinsics()
    {
        return Realsense2Invoker.get_depth_intrinsics(capPtr);
    }
    public IntPtr GetCalibration()
    {
        if(calibrationHandle.IsAllocated)
        {
            return calibrationHandle.AddrOfPinnedObject();
        }

        IntPtr calPtr = Realsense2Invoker.get_calibration(capPtr);
        if(calPtr == IntPtr.Zero)
        {
            return IntPtr.Zero;
        }
        calibrationHandle = copyCalibration(calPtr);


        Realsense2Invoker.free_capturer_calibration(Type, calPtr); 
        return calibrationHandle.AddrOfPinnedObject();
    }
    protected abstract GCHandle copyCalibration(IntPtr cal);
    #region IDispose code
    protected virtual void Dispose(bool disposing)
    {
        if (!disposedValue)
        {
            if (disposing)
            {
                if(capPtr != IntPtr.Zero)
                {
                    Debug.Log("freeing capture");
                    Realsense2Invoker.free_capturer(capPtr);
                    capPtr = IntPtr.Zero;
                }
                if(calibrationHandle.IsAllocated)
                {
                    calibrationHandle.Free();
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
