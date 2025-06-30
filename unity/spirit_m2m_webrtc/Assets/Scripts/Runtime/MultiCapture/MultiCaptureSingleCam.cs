using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

public class MultiCaptureSingleCam : BaseCapture, ICapturePoll
{
    private IntPtr multiCamPtr;
    private uint capturerIndex;

    public MultiCaptureSingleCam(CaptureType captureType, CaptureHelper captureHelper ) : base(captureType, captureHelper)
    {
        
    }

    public void Init(IntPtr multiCamPtr, uint capturerIndex)
    {
        this.multiCamPtr = multiCamPtr;
        this.capturerIndex = capturerIndex;
    }

    protected override IntPtr getCalibrationFromCapturer()
    {
        if (multiCamPtr == IntPtr.Zero) return IntPtr.Zero;
        return Realsense2Invoker.get_calibration_for_capturer(multiCamPtr, capturerIndex);
    }

    #region Poll functions
    public IntPtr PollNextRawFrame()
    {
        if (multiCamPtr == IntPtr.Zero)
        {
            return IntPtr.Zero;
        }
        return Realsense2Invoker.poll_next_raw_frame_for_capturer(multiCamPtr, capturerIndex);
    }

    public IntPtr PollNextPointCloud()
    {
        if (multiCamPtr == IntPtr.Zero)
        {
            return IntPtr.Zero;
        }
        return Realsense2Invoker.poll_next_frame_for_capturer(multiCamPtr, capturerIndex);
    }

    public IntPtr PollNextFrame()
    {
        if (multiCamPtr == IntPtr.Zero)
        {
            return IntPtr.Zero;
        }
        return Realsense2Invoker.poll_next_frame_for_capturer(multiCamPtr, capturerIndex);
    }
    #endregion 


    public IntPtr GetHelperSettingsPtr()
    {
        return CaptureHelper.SettingsHandle.AddrOfPinnedObject();
    }
    public void FreeHelperSettingsHandle()
    {
        CaptureHelper.FreeSettingsEx();
    }

    #region IDispose code
    protected override void Dispose(bool disposing)
    {
        if (!disposedValue)
        {
            if (disposing)
            {
                if (multiCamPtr != IntPtr.Zero)
                {
                    multiCamPtr = IntPtr.Zero;
                }
                base.Dispose(true);
            }
            disposedValue = true;
        }
    }

    #endregion
}
