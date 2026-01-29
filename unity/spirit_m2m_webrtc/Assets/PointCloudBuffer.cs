using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Xml.Linq;
using UnityEngine;

public class PointCloudBuffer : RenderablePointCloudBuffer
{
    private readonly object _lock = new();
    private DecodedRawFrameMulti previousFrame = null; // Used to potentially repair next frames

    public Dictionary<UInt32, DecodedPointCloudMulti> inProgessFrames = new();

    public ConcurrentQueue<DecodedPointCloudMulti> queue = new();


    public uint NActiveCapturers;

    private Mutex mut = new Mutex();
    public PointCloudBuffer(PlaybackBufferSettings settings, ulong timestampNextDeadline, uint fps) : base(settings, timestampNextDeadline, fps)
    {
        
    }
    public DecodedPointCloudMulti GetMultiFrame(uint frameNr, uint nPoints)
    {
        lock(_lock)
        {
            if (!inProgessFrames.TryGetValue(frameNr, out DecodedPointCloudMulti d))
            {
                // If the frame is not in the inProgressFrames, create a new one
                d = new DecodedPointCloudMulti(NActiveCapturers, 0, frameNr, nPoints);
                inProgessFrames.Add(frameNr, d);
            }
            else 
            {
                d.IncreaseBuffers(nPoints);
            }
            return d;
        }
      

    }

    public override RenderablePointCloud CheckForCompletedFrames()
    {

        if (!queue.IsEmpty)
        {
            bool succes = queue.TryDequeue(out DecodedPointCloudMulti dec);
            if (succes)
            {
                if (EnqueueImmediately || (dec.TargetTimestamp >= TimestampNextDeadline))
                {
                    SetNextDeadline();
                    return dec;
                }
            }
            return null;
        }
        
        // TODO Rework this without lock so other threads can still add frames
        if (RenderIncompleteFrames)
        {
            // If a frame is fully received it will already be put in the queue so check for frames beyond the deadline
            DecodedPointCloudMulti foundFrame = null;
            lock (_lock)
            {

                // Check if there is an incomplete frame that we can still render
                foreach (var kvp in inProgessFrames.OrderByDescending(kvp => kvp.Key))
                {
                    if ((kvp.Value.TargetTimestamp >= (TimestampNextDeadline + MaxTimeBeforeIncompleteRender)))
                    {
                        foundFrame = kvp.Value;
                    }
                }
            }
            if (foundFrame != null)
            {
                Debug.Log("FOUND A FRAME");
                CompleteFrame(foundFrame, false);
                SetNextDeadline();
                return foundFrame;
            }
        }
    
        return null;
    }

    public void SetNextDeadline()
    {
        TimestampNextDeadline += (1000 / FPS);
    }

    

    public void CompleteFrame(DecodedPointCloudMulti newestFrame, bool addToQueue)
    {
        lock(_lock)
        {
            List<uint> framesToDelete = inProgessFrames.Keys.Where(k => k <= newestFrame.FrameNr).ToList();
            foreach (var frame in framesToDelete)
            {
                inProgessFrames.Remove(frame);
            }
        }
        
        if (addToQueue)
        {
            queue.Enqueue(newestFrame);
        }

    }
}
