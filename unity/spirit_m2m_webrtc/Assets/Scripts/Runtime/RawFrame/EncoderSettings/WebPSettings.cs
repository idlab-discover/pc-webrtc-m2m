using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;

[StructLayout(LayoutKind.Explicit)]
public unsafe struct WebPSettingsEx
{

    [FieldOffset(0)]
    public uint quality;
    [FieldOffset(4)]
    public uint method;
}
