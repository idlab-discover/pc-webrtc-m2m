using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using UnityEngine;

public class DecodedRawFrame : MonoBehaviour
{
    public int FrameNr;
    public int NPoints;
    public ulong Timestamp;
    public Vector3[] Points;
    public Color32[] Colors;
    public bool PointsCompleted;
    public bool ColorsCompleted;
    public IntPtr DecodedDepth;
    public IntPtr DecodedColor;
    public bool IsCompleted { get { return PointsCompleted && ColorsCompleted; } }
    private Mutex mut = new Mutex();
    public DecodedRawFrame(int frameNr, int nPoints, ulong timestamp)
    {
        FrameNr = frameNr;
        NPoints = nPoints;
        Points = new Vector3[nPoints];
        Colors = new Color32[nPoints];
        Timestamp = timestamp;

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
