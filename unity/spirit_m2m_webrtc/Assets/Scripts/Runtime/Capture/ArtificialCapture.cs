using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;
using static UnityEngine.Rendering.VirtualTexturing.Debugging;


[StructLayout(LayoutKind.Explicit)]
public unsafe struct ArtificialCalibrationEx
{
    [FieldOffset(0)]
    public uint sideSize;
}



public class ArtificialCapture : SingleCapture
{
    public ArtificialCapture(uint fps, FrameMode frameMode, FrameCleanupSettingsEx frameCleanupSettings, ArtificialSettings captureSettings) : base(fps, frameMode, frameCleanupSettings, CaptureType.Artificial, convertToSetToEx(captureSettings))
    {
    }

    private static GCHandle convertToSetToEx(ArtificialSettings set)
    {
        return GCHandle.Alloc(new ArtificialSettingsEx() { artificialSize = set.artificialSize }, GCHandleType.Pinned);
    }

    protected override GCHandle copyCalibration(IntPtr cal)
    {
        ArtificialCalibrationEx artificialCalibrationEx = Marshal.PtrToStructure<ArtificialCalibrationEx>(cal);
        Debug.Log("side size" + artificialCalibrationEx.sideSize);
        return GCHandle.Alloc(artificialCalibrationEx, GCHandleType.Pinned);
    }
}
