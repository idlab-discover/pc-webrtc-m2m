using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public abstract class DecodedRawFrameBase 
{
    public uint FrameNr;
    public uint NPoints;
    public ulong Timestamp;
    public IntPtr DecodedDepth;
    public IntPtr DecodedColor;

    public bool DepthCompleted;
    public bool ColorsCompleted;
    public uint ColorWidth;
    public uint ColorHeight;

    public bool IsCompleted { get { return DepthCompleted && ColorsCompleted; } }
    public Color32[] DecodedColors;

    public DecodedRawFrameBase(uint frameNr, uint nPoints)
    {
        FrameNr = frameNr;
        NPoints = nPoints;
    }

    public void InitRawColors(uint size)
    {
        DecodedColors = new Color32[size];
    }


}
