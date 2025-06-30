using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;

public class RealsenseCaptureHelper : CaptureHelper
{
    public RealsenseCaptureHelper(RealsenseSettings settings) : base(convertToSetToEx(settings))
    {

    }
    private static GCHandle convertToSetToEx(RealsenseSettings set)
    {
        return GCHandle.Alloc(new RealsenseSettingsEx() { width = set.width, height = set.height, alignToDepth = set.alignToDepth, maxDist = set.maxDist, minDist = set.minDist }, GCHandleType.Pinned);
    }

    public override GCHandle copyCalibration(IntPtr cal)
    {
        throw new NotImplementedException();
    }
}
