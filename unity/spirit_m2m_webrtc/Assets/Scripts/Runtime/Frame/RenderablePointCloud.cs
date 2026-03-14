using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;

public abstract class RenderablePointCloud : IDisposable
{
    protected readonly object _lock = new();
    public ulong TargetTimestamp;
    public uint FrameNr;
    public uint TotalPoints;
    public uint Quality;
    // TODO Make borrowing pool
    public NativeArray<Vector3> Points;
    public NativeArray<Color32> Colors;
  //  public Vector3[] Points;
   // public Color32[] Colors;
    private bool disposedValue;
    
    public RenderablePointCloud(ulong targetTimestamp, uint frameNr, uint totalPoints)
    {
        TargetTimestamp = targetTimestamp;
        FrameNr = frameNr;
        TotalPoints = totalPoints;

        Points = new NativeArray<Vector3>((int)totalPoints, Allocator.Persistent);
        Colors = new NativeArray<Color32>((int)totalPoints, Allocator.Persistent);
       // Points = new Vector3[totalPoints];
      //  Colors = new Color32[totalPoints];
    }
    ~RenderablePointCloud()
    {
        Debug.Log("[TEST] RenderablePointCloud Finalizer called");
    }
    public void IncreaseBuffers(uint size)
    {
        TotalPoints += size;
        
        NativeArray<Vector3> newPoints = new NativeArray<Vector3>((int)TotalPoints, Allocator.Persistent);
        NativeArray<Color32> newColors = new NativeArray<Color32>((int)TotalPoints, Allocator.Persistent);
        NativeArray<Vector3>.Copy(Points, newPoints, Points.Length);
        NativeArray<Color32>.Copy(Colors, newColors, Colors.Length);
        Points.Dispose();
        Colors.Dispose();
        Points = newPoints;
        Colors = newColors;
        //Array.Resize(ref Points, Points.Length + (int)size);
        // Array.Resize(ref Colors, Colors.Length + (int)size);
    }
    public override string ToString()
    {
        return $"targetTimestamp={TargetTimestamp} totalPoints={TotalPoints} Quality={Quality}";
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!disposedValue)
        {
            if (disposing)
            {
                disposeInternal();
                if (Points.IsCreated)
                {
                    Points.Dispose();
                }
                if (Colors.IsCreated)
                {
                    Colors.Dispose();
                }
            }

            // TODO: free unmanaged resources (unmanaged objects) and override finalizer
            // TODO: set large fields to null
            disposedValue = true;
        }
    }

    protected virtual void disposeInternal()
    {
        
    }

    public void Dispose()
    {
        // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }
}
