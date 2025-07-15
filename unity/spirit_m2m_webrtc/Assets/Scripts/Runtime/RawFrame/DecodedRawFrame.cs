using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using UnityEngine;

public class DecodedRawFrame : DecodedRawFrameBase
{

    public Vector3[] Points;
    public Color32[] Colors;

    private Mutex mut = new Mutex();
    public DecodedRawFrame(uint frameNr, uint nPoints, ulong timestamp) : base(frameNr, nPoints)
    {
        Points = new Vector3[nPoints];
        Colors = new Color32[nPoints];
    }

    public void LockClass()
    {
        mut.WaitOne();
    }
    public void UnlockClass()
    {
        mut.ReleaseMutex();
    }
    
}
