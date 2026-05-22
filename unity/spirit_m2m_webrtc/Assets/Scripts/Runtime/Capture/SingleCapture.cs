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
    PlyFiles = 5
}

public abstract class SingleCapture : BaseCapture, ICapturePoll
{
   
    protected IntPtr capPtr { get; set; }
   
    protected SingleCapture(uint fps, FrameMode frameMode, FrameCleanupSettingsEx frameCleanupSettings, CaptureType capType, CaptureHelper captureHelper, bool startCaptureThread) : base(capType, captureHelper)
    {
        capPtr = Realsense2Invoker.create_new_capturer(fps, frameMode, frameCleanupSettings, capType, this.CaptureHelper.SettingsHandle.AddrOfPinnedObject());
        if (capPtr != IntPtr.Zero)
        {
            Realsense2Invoker.start_capturing(capPtr, startCaptureThread);
        } else {
            Debug.LogError("Failed to create capturer of type " + capType.ToString());
        }
            CaptureHelper.FreeSettingsEx();
    }

    protected override IntPtr getCalibrationFromCapturer()
    {
        return Realsense2Invoker.get_calibration(capPtr);
    }

    public override uint GetCalibrationSize()
    {
        return Realsense2Invoker.get_calibration_size(capPtr);
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
  

    #region IDispose code
    protected override void Dispose(bool disposing)
    {
        if (!disposedValue)
        {
            if (disposing)
            {
                if (capPtr != IntPtr.Zero)
                {
                    Debug.Log("freeing capture");
                    Realsense2Invoker.free_capturer(capPtr);
                    capPtr = IntPtr.Zero;
                }
                base.Dispose(true);
            }
            disposedValue = true;
        }
    }
   
    #endregion
}
