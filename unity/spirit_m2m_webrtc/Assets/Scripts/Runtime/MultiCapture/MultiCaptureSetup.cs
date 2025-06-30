using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.UIElements;



public class MultiCaptureSetup : IDisposable
{

    protected bool disposedValue;
    private IntPtr multiCapPtr;
    private List<MultiCaptureSingleCam> capturers;

   
    public MultiCaptureSetup(uint fps, FrameMode frameMode, FrameCleanupSettingsEx frameCleanupSettings, List<MultiCaptureSingleCam> capturers)
    {
        this.capturers = capturers;
        CaptureType capType = capturers[0].CaptureType;
        IntPtr[] settingPtrs = new IntPtr[capturers.Count];
        for(int i = 0; i < capturers.Count; i++)
        {
            settingPtrs[i] = capturers[i].GetHelperSettingsPtr();
        }
        GCHandle settingPtrsHandle = GCHandle.Alloc(settingPtrs, GCHandleType.Pinned);
        multiCapPtr = Realsense2Invoker.create_new_multi_capturer(fps, frameMode, frameCleanupSettings, capType, (uint)this.capturers.Count, settingPtrs);
        if (multiCapPtr != IntPtr.Zero)
        {
            Realsense2Invoker.start_capturing_multi(multiCapPtr);
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
