using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;

[StructLayout(LayoutKind.Explicit)]
public unsafe struct FrameCleanupSettingsEx
{

    [FieldOffset(0)]
    public uint blackoutBlockSize;
    [FieldOffset(4)]
    public bool shouldApplyDepthFilter;
    [FieldOffset(5)]
    public bool shouldCleanupDepth;
    [FieldOffset(6)]
    public bool shouldBlackout;
}
