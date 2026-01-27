using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;

[StructLayout(LayoutKind.Explicit)]
public unsafe struct PlyFilesCalibrationEx
{
}


public class PlyFilesCapturer : SingleCapture
{
    public PlyFilesCapturer(uint fps, FrameMode frameMode, FrameCleanupSettingsEx frameCleanupSettings, PlyFilesSettings captureSettings, bool startCaptureThread) : base(fps, frameMode, frameCleanupSettings, CaptureType.PlyFiles, new PlyFilesCaptureHelper(captureSettings), startCaptureThread)
    {
    }

    public PlyFilesCalibrationEx GetCalibrationStruct()
    {
        throw new NotImplementedException();
    }


}
