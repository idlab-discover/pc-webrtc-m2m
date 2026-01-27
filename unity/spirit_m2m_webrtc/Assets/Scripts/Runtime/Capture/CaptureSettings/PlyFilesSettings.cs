using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;

[System.Serializable]
public class PlyFilesSettings
{
    public string directoryPath;

}

[StructLayout(LayoutKind.Explicit, Pack = 1)]
public unsafe struct PlyFilesSettingsEx
{
    [FieldOffset(0)]
    public fixed byte directoryPath[256];
}


