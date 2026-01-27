using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MultiSinglePlyFiles : MultiCaptureSingleCam
{
    public MultiSinglePlyFiles(PlyFilesSettings settings) : base(CaptureType.PlyFiles, new PlyFilesCaptureHelper(settings))
    {
    }
}
