using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MultiCaptureSinglePrerecKinect : MultiCaptureSingleCam
{
    public MultiCaptureSinglePrerecKinect(PrerecordedKinectSettings settings) : base(CaptureType.PrerecordedKinect, new KinectCaptureHelper(settings))
    {
    }
}
