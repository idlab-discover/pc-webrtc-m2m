using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public abstract class RenderablePointCloud 
{
    protected readonly object _lock = new();
    public ulong TargetTimestamp;
    public uint FrameNr;
    public uint TotalPoints;
    public uint Quality;
    public Vector3[] Points;
    public Color32[] Colors;

    public RenderablePointCloud(ulong targetTimestamp, uint frameNr, uint totalPoints)
    {
        TargetTimestamp = targetTimestamp;
        FrameNr = frameNr;
        TotalPoints = totalPoints;

        Points = new Vector3[totalPoints];
        Colors = new Color32[totalPoints];
    }

    public void IncreaseBuffers(uint size)
    {
        TotalPoints += size;
        Array.Resize(ref Points, Points.Length + (int)size);
        Array.Resize(ref Colors, Colors.Length + (int)size);
    }
}
