using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;

public class RealsenseCapture : SingleCapture
{
    public RealsenseCapture(uint fps, FrameMode frameMode, FrameCleanupSettingsEx frameCleanupSettings, RealsenseSettings captureSettings) : base(fps, frameMode, frameCleanupSettings, CaptureType.Realsense, convertToSetToEx(captureSettings))
    {
    }

    private static GCHandle convertToSetToEx(RealsenseSettings set)
    {
        return GCHandle.Alloc(new RealsenseSettingsEx() { width = set.width, height = set.height, alignToDepth = set.alignToDepth, maxDist = set.maxDist, minDist = set.minDist }, GCHandleType.Pinned);
    }

    protected override GCHandle copyCalibration(IntPtr cal)
    {
        throw new NotImplementedException();
    }
}
