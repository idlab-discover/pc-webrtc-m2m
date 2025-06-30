using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using UnityEngine;

#region Help struct
[StructLayout(LayoutKind.Explicit, Size = 48)]
public unsafe struct KinectCamExtrinsicsEx
{
    [FieldOffset(0)]
    public fixed float rotation[9];
    [FieldOffset(36)]
    public fixed float translation[3];
}

[StructLayout(LayoutKind.Explicit, Size = 68)]
public unsafe struct KinectCamIntrinsicsEx
{
    [FieldOffset(0)]
    public uint type;
    [FieldOffset(4)]
    public uint parameterCount;
    [FieldOffset(8)]
    public fixed float parameters[15];
}
[StructLayout(LayoutKind.Explicit, Size = 128)]
public unsafe struct KinectCamCalibrationEx
{
    [FieldOffset(0)]
    public KinectCamExtrinsicsEx extrinsics;
    [FieldOffset(48)]
    public KinectCamIntrinsicsEx intrinsics;
    [FieldOffset(116)]
    public int resolutionWidth;
    [FieldOffset(120)]
    public int resolutionHeight;
    [FieldOffset(124)]
    public float metricRadius;
}
#endregion

[StructLayout(LayoutKind.Explicit)]
public unsafe struct KinectCalibrationEx
{
    [FieldOffset(0)]
    public KinectCamCalibrationEx depthCalibration;
    [FieldOffset(128)]
    public KinectCamCalibrationEx colorCalibration;
    [FieldOffset(256)]
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 16)]
    public KinectCamExtrinsicsEx[] extrinsics;
    [FieldOffset(1024)]
    public uint depthMode;
    [FieldOffset(1028)]
    public uint colorResolution;
    [FieldOffset(1032)]
    public fixed float trafo[16];
    [FieldOffset(1096)]
    public byte alignToDepth;
}


public class KinectCaptureHelper : CaptureHelper
{
    public KinectCalibrationEx KinectCalibration { get; private set; }
    
  
    public KinectCaptureHelper(PrerecordedKinectSettings set) : base(convertToSetToEx(set))
    {
    }

    private unsafe static GCHandle convertToSetToEx(PrerecordedKinectSettings set)
    {
        PrerecordedKinectSettingsEx setEx = new PrerecordedKinectSettingsEx() { alignToDepth = set.alignToDepth, minHeight = set.minHeight, maxHeight = set.maxHeight, radius = set.radius };
        Debug.Log("rad " + set.camFile);
        for (int i = 0; i < 4; i++)
        {
            for (int j = 0; j < 4; j++)
            {
                Debug.Log(i + " " + j + " " + set.trafo[i][j]);
                setEx.trafo[i * 4 + j] = set.trafo[i][j];
            }
        }

        for (int i = 0; i < set.camFile.Length && i < 255; i++)
        {
            setEx.camFile[i] = (byte)set.camFile[i];
        }

        setEx.camFile[Math.Min(255, set.camFile.Length)] = 0;
        return GCHandle.Alloc(setEx, GCHandleType.Pinned);
    }

    public override GCHandle copyCalibration(IntPtr cal)
    {
        KinectCalibration = Marshal.PtrToStructure<KinectCalibrationEx>(cal);
        Debug.Log("art size" + KinectCalibration.colorCalibration.resolutionHeight);
        bool AlignToDepth = KinectCalibration.alignToDepth != 0;
        Debug.Log("align: " + KinectCalibration.alignToDepth + " " + AlignToDepth);
        Debug.Log("color reso: " + KinectCalibration.colorCalibration.resolutionWidth);
        return GCHandle.Alloc(KinectCalibration, GCHandleType.Pinned);
    }

}
