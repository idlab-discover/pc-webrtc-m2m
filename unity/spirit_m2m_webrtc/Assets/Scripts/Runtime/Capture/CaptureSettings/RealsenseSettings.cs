using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;



[System.Serializable]
public class RealsenseSettings
{
    public uint width = 848;
    public uint height = 480;
    public float minDist = 0.0f;
    public float maxDist = 1.5f;
    public bool alignToDepth = false;
}

[StructLayout(LayoutKind.Explicit)]
public unsafe struct RealsenseSettingsEx
{

    [FieldOffset(0)]
    public uint width;
    [FieldOffset(4)]
    public uint height;
    [FieldOffset(8)]
    public float minDist;
    [FieldOffset(12)]
    public float maxDist;
    [FieldOffset(16)]
    public bool alignToDepth;
}


