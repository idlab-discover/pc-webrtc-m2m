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
    private Dictionary<UInt32, long> _frameFirstReceivedAt = new(); // Wall-clock ms when each frame was first buffered

    public ConcurrentQueue<DecodedPointCloudMulti> queue = new();

    private uint latestCompletedFrameNr = 0;
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
                _frameFirstReceivedAt[frameNr] = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
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
                lock(_lock)
                {
                    if (EnqueueImmediately || (DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() >= (long)TimestampNextDeadline))
                    {
                        SetNextDeadline();
                        latestCompletedFrameNr = dec.FrameNr;
                        return dec;
                    }    
                }
                
            }
            return null;
        }
        
        if (RenderIncompleteFrames)
        {
  
                // Find the oldest incomplete frame that has been held for at least MaxTimeBeforeIncompleteRender ms
                DecodedPointCloudMulti foundFrame = null;
                long now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                lock (_lock)
                {
                    foreach (var kvp in inProgessFrames.OrderBy(kvp => kvp.Key))
                    {
                        if (_frameFirstReceivedAt.TryGetValue(kvp.Key, out long receivedAt) &&
                            (now - receivedAt) >= MaxTimeBeforeIncompleteRender)
                        {
                            foundFrame = kvp.Value;
                            break; // Take the oldest eligible frame
                        }
                    }
                
                    if (foundFrame != null)
                    {
                        //Debug.Log($"[JitterBuffer] Rendering incomplete frame {foundFrame.FrameNr}");
                        completeFrameInternal(foundFrame, false);
                        SetNextDeadline();
                        latestCompletedFrameNr = foundFrame.FrameNr;
                        return foundFrame;
                    }
                }
        }
        return null;
    }

    public void SetNextDeadline()
    {
        TimestampNextDeadline += (1000 / FPS);
    }

    public bool ShouldDecodeOrAddFrame(uint frameNr)
    {
        lock (_lock)
        {
            if (frameNr <= latestCompletedFrameNr && latestCompletedFrameNr != 0)
            {
                return false;
            }
            return true;
        }
    }

    public void CompleteFrame(DecodedPointCloudMulti newestFrame, bool addToQueue)
    {
        lock(_lock)
        {
            completeFrameInternal(newestFrame, addToQueue);
        }

    }
    private void completeFrameInternal(DecodedPointCloudMulti newestFrame, bool addToQueue)
    {
        

        List<uint> framesToDelete = inProgessFrames.Keys.Where(k => k <= newestFrame.FrameNr).ToList();
        foreach (var frame in framesToDelete)
        {
            inProgessFrames.Remove(frame);
            _frameFirstReceivedAt.Remove(frame);
        }
        
        
        if (addToQueue)
        {
            queue.Enqueue(newestFrame);
        }
    }
}

