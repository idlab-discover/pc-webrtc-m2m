using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using UnityEngine;

// Used for multi camera setups with raw encoding

public class DecodedRawFrameSingle : DecodedRawFrameBase
{
    public uint CapturerID;
    public uint PointOffset;

    public DecodedRawFrameSingle(uint capturerID, uint frameNr, uint nPoints) : base(frameNr, nPoints)
    {
        CapturerID = capturerID;
    }

}

public class DecodedRawFrameMulti : RenderablePointCloud
{
    public uint NCapturers;
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
    public DecodedRawFrameMulti(uint nCapturers, uint frameNr, ulong targetTimestamp) : base(targetTimestamp, frameNr, 0)
    {
        NCapturers = nCapturers;
        Singles = new DecodedRawFrameSingle[NCapturers];
    }
    public DecodedRawFrameSingle AddSingle(uint capturerID, uint size) // Call when receiving either the decoded depth or decoded color frame for the first time for this full frame
    {
        DecodedRawFrameSingle single = new DecodedRawFrameSingle(capturerID, size, TotalPoints);
        Singles[(int)capturerID] = single;
        IncreaseBuffers(size);
        return single;
    }
    public void AddAndConvertSingle(DecodedRawFrameSingle single, RawConverter converter)
    {
        mut.WaitOne();
        Singles[(int)single.CapturerID] = single;
        single.PointOffset = TotalPoints;
        IncreaseBuffers(single.NPoints);
        mut.ReleaseMutex();
        converter.ConvertRawSingleFrame(this, single);
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
