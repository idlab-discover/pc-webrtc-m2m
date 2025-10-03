using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;

public class RealsenseCapture : SingleCapture
{
    public RealsenseCapture(uint fps, FrameMode frameMode, FrameCleanupSettingsEx frameCleanupSettings, RealsenseSettings captureSettings, bool startCaptureThread) : base(fps, frameMode, frameCleanupSettings, CaptureType.Realsense, new RealsenseCaptureHelper(captureSettings), startCaptureThread)
    {
    }

   
}
