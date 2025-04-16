using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.UIElements;

[StructLayout(LayoutKind.Explicit)]
public unsafe struct JPEGSettingsEx
{

    [FieldOffset(0)]
    public uint quality;
}
