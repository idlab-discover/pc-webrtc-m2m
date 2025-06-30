using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;

[StructLayout(LayoutKind.Explicit)]
public unsafe struct ArtificialCalibrationEx
{
    [FieldOffset(0)]
    public uint sideSize;
}


public class ArtificialCaptureHelper : CaptureHelper
{
    public ArtificialCalibrationEx ArtificialCalibrationEx { get; private set; }
    public ArtificialCaptureHelper(ArtificialSettings settings) : base(convertToSetToEx(settings))
    {

    }
    public override GCHandle copyCalibration(IntPtr cal)
    {
        ArtificialCalibrationEx = Marshal.PtrToStructure<ArtificialCalibrationEx>(cal);
        Debug.Log("side size" + ArtificialCalibrationEx.sideSize);
        return GCHandle.Alloc(ArtificialCalibrationEx, GCHandleType.Pinned);
    }

    private static GCHandle convertToSetToEx(ArtificialSettings set)
    {
        return GCHandle.Alloc(new ArtificialSettingsEx() { artificialSize = set.artificialSize }, GCHandleType.Pinned);
    }

  

}
