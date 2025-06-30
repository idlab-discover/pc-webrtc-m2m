using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;

public abstract class CaptureHelper 
{
    public GCHandle SettingsHandle { get; }

    public CaptureHelper(GCHandle settingsHandle)
    {
        SettingsHandle = settingsHandle;
    }

    public abstract GCHandle copyCalibration(IntPtr cal);

    public void FreeSettingsEx()
    {
        if(SettingsHandle.IsAllocated)
        {
            SettingsHandle.Free();
        }
    }
}
