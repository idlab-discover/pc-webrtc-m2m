using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MultiSingleArtificial : MultiCaptureSingleCam
{
    public MultiSingleArtificial(ArtificialSettings settings) : base(CaptureType.Artificial, new ArtificialCaptureHelper(settings))
    {
    }
}
