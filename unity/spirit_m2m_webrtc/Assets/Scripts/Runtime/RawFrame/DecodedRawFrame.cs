using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using UnityEngine;

public class DecodedRawFrame
{
    public int FrameNr;
    public int NPoints;
    public ulong Timestamp;
    public List<Vector3> Points;
    public List<Color32> Colors;
    public bool[] PointStatus; // 1 = point is valid
    public bool PointsCompleted;
    public bool ColorsCompleted;
    public bool IsCompleted { get { return PointsCompleted && ColorsCompleted; } }
    private Mutex mut = new Mutex();
    public DecodedRawFrame(int frameNr, int nPoints, ulong timestamp)
    {
        FrameNr = frameNr;
        NPoints = nPoints;
        Points = new(nPoints);
        Colors = new(nPoints);
        PointStatus = new bool[nPoints];
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
