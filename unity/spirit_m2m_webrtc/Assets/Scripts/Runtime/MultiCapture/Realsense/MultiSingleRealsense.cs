using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MultiSingleRealsense : MultiCaptureSingleCam
{
    public MultiSingleRealsense(RealsenseSettings settings) : base(CaptureType.Realsense, new RealsenseCaptureHelper(settings))
    {
    }
}
