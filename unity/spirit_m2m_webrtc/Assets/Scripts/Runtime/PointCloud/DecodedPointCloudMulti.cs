using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class DecodedPointCloudMulti : RenderablePointCloud
{
    public uint ActualPoints {  get; private set; }
    public uint NCapturers;
    public Dictionary<uint, DecodedPointCloudSingle> Singles = new();

    public bool IsCompleted
    {
        get
        {
            bool c1 = Singles.Values.All((s) => {
                if (s == null)
                {
                    return false;
                }
                return s.IsCompleted;
            });
            /*int completedCount = Singles.Values.Sum((s) => {
                return s.IsCompleted ? 1 : 0;
            }); ;*/
            return c1;
        }
    }
    public DecodedPointCloudMulti(uint nCapturers, ulong targetTimestamp, uint frameNr, uint totalPoints) : base(targetTimestamp, frameNr, totalPoints)
    {
//        Debug.Log($"NCapturers: {nCapturers}");
        NCapturers = nCapturers;
        ActualPoints = 0;
    }
    public void IncreaseBufferSafe(uint size)
    {
        lock (_lock)
        {
            IncreaseBuffers(size);
        }
    }
    public void AddSingle(DecodedPointCloudSingle single)
    {
        lock (_lock)
        {
            //Debug.Log("[MULTI] Added single from capturerID: " + single.CapturerID + " frameNr: " + single.FrameNr + " nPoints: " + single.NPoints + " with nCapturers: " + NCapturers);
            Singles[single.CapturerID] = single;
        }
    }
    public DecodedPointCloudSingle GetSingle(uint capturerID)
    {
        lock (_lock)
        {
            if (Singles.TryGetValue(capturerID, out DecodedPointCloudSingle s))
            {
                return s;
            }
            return null;
        }
    }
    public void AddPoints(IntPtr posPtr, IntPtr colPtr, uint size)
    {
        lock (_lock) { 
            unsafe
            {
                float* pointsUnsafePtr = (float*)posPtr;
                byte* colorsUnsafePtr = (byte*)colPtr;
                for (int i = 0; i < size; i++)
                {
                    if (pointsUnsafePtr[(i * 3)] == 0 && pointsUnsafePtr[(i * 3) + 1] == 0 && pointsUnsafePtr[(i * 3) + 2] == 0)
                    {
                        continue;
                    }
                    // TODO performance testing
                    Points[(int)ActualPoints] = new Vector3(pointsUnsafePtr[(i * 3)] * -1, pointsUnsafePtr[(i * 3) + 1] * -1, pointsUnsafePtr[(i * 3) + 2] * -1);
                    Colors[(int)ActualPoints] = new Color32(colorsUnsafePtr[(i * 3)], colorsUnsafePtr[(i * 3) + 1], colorsUnsafePtr[(i * 3) + 2], 255);
                    ActualPoints++;
                }
            }
            //Debug.Log("[MULTI] Added " + FrameNr + " total: " + ActualPoints);
            TotalPoints = ActualPoints;
        }
    }
}
