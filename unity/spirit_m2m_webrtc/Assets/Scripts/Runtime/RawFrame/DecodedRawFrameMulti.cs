using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using UnityEngine;

// Used for multi camera setups with raw encoding

public class DecodedRawFrameSingle
{
    public uint PointOffset;
    public uint NPoints;
    public ulong Timestamp;
    public IntPtr DecodedDepth;
    public IntPtr DecodedColor;

    public bool PointsCompleted;
    public bool ColorsCompleted;

    public bool IsCompleted { get { return PointsCompleted && ColorsCompleted; } }
    public Color32[] DecodedColors;
    public DecodedRawFrameSingle(uint nPoints, uint pointOffset)
    {
        NPoints = nPoints;
        PointOffset = pointOffset;
    }

    public void InitRawColors(uint size)
    {
        DecodedColors = new Color32[size];
    }
}

public class DecodedRawFrameMulti
{
    public ulong TargetTimestamp;
    public uint FrameNr;
    public uint NCapturers;
    public uint TotalPoints;
    public Vector3[] Points;
    public Color32[] Colors;
    public DecodedRawFrameSingle[] Singles;
   
    public bool IsCompleted { get {
            bool c1 = Singles.All((s) => {
                if(s == null)
                {
                    return false;
                } 
                return s.IsCompleted;
            });
            return c1; 
    } }
    private Mutex mut = new Mutex();
    public DecodedRawFrameMulti(uint nCapturers, uint frameNr, ulong targetTimestamp)
    {
        FrameNr = frameNr;
        Points = new Vector3[0];
        Colors = new Color32[0];
        NCapturers = nCapturers;
        Singles = new DecodedRawFrameSingle[NCapturers];
        TargetTimestamp = targetTimestamp;
    }
    public DecodedRawFrameSingle AddSingle(uint capturerID, uint size) // Call when receiving either the decoded depth or decoded color frame for the first time for this full frame
    {
        DecodedRawFrameSingle single = new DecodedRawFrameSingle(size, TotalPoints);
        Singles[(int)capturerID] = single;
        IncreaseBuffers(size);
        return single;
    }
    public DecodedRawFrameSingle GetSingle(uint capturerID)
    {
        return Singles[(int)capturerID];
    }
    
    public void IncreaseBuffers(uint size)
    {
        TotalPoints += size;
        Array.Resize(ref Points, (int)TotalPoints);
        Array.Resize(ref Colors, (int)TotalPoints);
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
