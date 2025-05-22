using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;

[System.Serializable]
public class PrerecordedKinecteSettings
{
    public bool alignToDepth = false;
    public float minHeight = 0.02f;
    public float maxHeight = 2.2f;
    public float radius = 1.25f;
    public float[][] trafo;
    public string camFile;

}

[StructLayout(LayoutKind.Explicit, Pack = 1)]
public unsafe struct PrerecordedKinecteSettingsEx
{

    [FieldOffset(0)]
    public bool alignToDepth;
    [FieldOffset(1)]
    public float minHeight;
    [FieldOffset(5)]
    public float maxHeight;
    [FieldOffset(9)]
    public float radius;
    [FieldOffset(13)]
    public fixed float trafo[16];
    [FieldOffset(77)]
    public fixed byte camFile[256];
}


