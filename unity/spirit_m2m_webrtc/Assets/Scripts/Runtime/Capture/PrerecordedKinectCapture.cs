using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.UIElements;

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
    public bool alignToDepth;
}


public class PrerecordedKinectCapture : SingleCapture
{
    public PrerecordedKinectCapture(uint fps, FrameMode frameMode, FrameCleanupSettingsEx frameCleanupSettings, PrerecordedKinecteSettings captureSettings) : base(fps, frameMode, frameCleanupSettings, CaptureType.PrerecordedKinect, convertToSetToEx(captureSettings))
    {
    }

    private unsafe static GCHandle convertToSetToEx(PrerecordedKinecteSettings set)
    {
        PrerecordedKinecteSettingsEx setEx = new PrerecordedKinecteSettingsEx() { alignToDepth = set.alignToDepth, minHeight = set.minHeight, maxHeight = set.maxHeight, radius = set.radius };
        Debug.Log("rad " + set.camFile);
        for (int i = 0; i < 4; i++)
        {
            for(int j = 0; j < 4; j++)
            {
                Debug.Log(i + " " + j + " " + set.trafo[i][j]);
                setEx.trafo[i*4 + j] = set.trafo[i][j];
            }
        }

        for (int i = 0; i < set.camFile.Length && i < 255; i++)
        {
            setEx.camFile[i] = (byte)set.camFile[i];
        }
      
        setEx.camFile[Math.Min(255, set.camFile.Length)] = 0;
        return GCHandle.Alloc(setEx, GCHandleType.Pinned);
    }

    protected override GCHandle copyCalibration(IntPtr cal)
    {
        KinectCalibrationEx kinectCalibrationEx = Marshal.PtrToStructure<KinectCalibrationEx>(cal);
        Debug.Log("art size" + kinectCalibrationEx.colorCalibration.resolutionHeight);
        unsafe
        {
            for (int i = 0; i < 4; i++)
            {
                for (int j = 0; j < 4; j++)
                {
                    Debug.Log(i + " " + j + " " + kinectCalibrationEx.trafo[i * 4 + j]);
                }
            }
        }
       
        return GCHandle.Alloc(kinectCalibrationEx, GCHandleType.Pinned);
    }
}
