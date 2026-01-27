using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;

public class PlyFilesCaptureHelper : CaptureHelper
{
    public PlyFilesCaptureHelper(PlyFilesSettings settings) : base(convertToSetToEx(settings))
    {

    }
    private static GCHandle convertToSetToEx(PlyFilesSettings set)
    {
        PlyFilesSettingsEx setEx = new() { };
        unsafe
        {
            for (int i = 0; i < set.directoryPath.Length && i < 255; i++)
            {
                setEx.directoryPath[i] = (byte)set.directoryPath[i];
            }

            setEx.directoryPath[Math.Min(255, set.directoryPath.Length)] = 0;
        }
        
        return GCHandle.Alloc(setEx, GCHandleType.Pinned);
    }

    public override GCHandle copyCalibration(IntPtr cal)
    {
        throw new NotImplementedException();
    }
}
