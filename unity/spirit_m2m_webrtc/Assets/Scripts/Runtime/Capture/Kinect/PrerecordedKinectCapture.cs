using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.UIElements;



public class PrerecordedKinectCapture : SingleCapture
{

    public KinectCalibrationEx kinectCalibrationEx;

    public PrerecordedKinectCapture(uint fps, FrameMode frameMode, FrameCleanupSettingsEx frameCleanupSettings, PrerecordedKinectSettings captureSettings, bool startCaptureThread) : base(fps, frameMode, frameCleanupSettings, CaptureType.PrerecordedKinect, new KinectCaptureHelper(captureSettings), startCaptureThread)
    {

    }

  
}
