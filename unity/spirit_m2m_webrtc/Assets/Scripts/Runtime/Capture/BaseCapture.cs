using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;

public abstract class BaseCapture : IDisposable
{
    protected bool disposedValue;
    public CaptureType CaptureType { get; }
    protected CaptureHelper CaptureHelper { get; }
    public IntPtr CalibrationPtr { get { if (calibrationHandle.IsAllocated) return calibrationHandle.AddrOfPinnedObject(); else return IntPtr.Zero; } }
    private GCHandle calibrationHandle;

    public BaseCapture(CaptureType captureType, CaptureHelper captureHelper)
    {
        CaptureHelper = captureHelper;
        CaptureType = captureType;
    }

    protected abstract IntPtr getCalibrationFromCapturer();

    public IntPtr GetCalibration()
    {
        if (calibrationHandle.IsAllocated)
        {
            return calibrationHandle.AddrOfPinnedObject();
        }

        IntPtr calPtr = getCalibrationFromCapturer();
        if (calPtr == IntPtr.Zero)
        {
            return IntPtr.Zero;
        }
        calibrationHandle = CaptureHelper.copyCalibration(calPtr);


        Realsense2Invoker.free_capturer_calibration(CaptureType, calPtr);
        return calibrationHandle.AddrOfPinnedObject();
    }

  
    #region IDisposible
    protected virtual void Dispose(bool disposing)
    {
        if (!disposedValue)
        {
            if (disposing)
            {
                if (calibrationHandle.IsAllocated)
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
