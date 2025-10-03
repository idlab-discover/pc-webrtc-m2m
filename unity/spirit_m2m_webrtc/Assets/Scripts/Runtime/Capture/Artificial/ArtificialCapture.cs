using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;
using static UnityEngine.Rendering.VirtualTexturing.Debugging;





public class ArtificialCapture : SingleCapture
{
    public ArtificialCapture(uint fps, FrameMode frameMode, FrameCleanupSettingsEx frameCleanupSettings, ArtificialSettings captureSettings, bool startCaptureThread) : base(fps, frameMode, frameCleanupSettings, CaptureType.Artificial, new ArtificialCaptureHelper(captureSettings), startCaptureThread)
    {
    }

    public ArtificialCalibrationEx GetCalibrationStruct()
    {
        return ((ArtificialCaptureHelper)CaptureHelper).ArtificialCalibrationEx;
    }

  
}
