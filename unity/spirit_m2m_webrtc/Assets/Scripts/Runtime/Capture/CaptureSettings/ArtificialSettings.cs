using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;


[System.Serializable]
public class ArtificialSettings
{
    public uint artificialSize = 15;
}


[StructLayout(LayoutKind.Explicit)]
public unsafe struct ArtificialSettingsEx : ICaptureSettings
{
    [FieldOffset(0)]
    public uint artificialSize;
}
