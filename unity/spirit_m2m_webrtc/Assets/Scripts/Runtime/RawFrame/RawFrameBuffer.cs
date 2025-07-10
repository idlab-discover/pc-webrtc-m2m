using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class RawFrameBuffer 
{
    private DecodedRawFrameMulti previousFrame; // Used to potentially repair next frames
    public uint holdPreviousFor;

    private Dictionary<UInt32, DecodedRawFrameMulti> inProgessFrames;
    public ulong TimestampNextDeadline;
    public ConcurrentQueue<DecodedRawFrameMulti> queue;
    public bool UsePreviousFrameData;
    public uint FPS;
    public uint MaxTimeBeforeIncompleteRender;
    public uint NActiveCapturers;

    public DecodedRawFrameMulti CheckForCompletedFrames()
    {
        if(!queue.IsEmpty)
        {
            DecodedRawFrameMulti dec;
            bool succes = queue.TryDequeue(out dec);
            if(succes)
            {
                if(dec.TargetTimestamp >= TimestampNextDeadline)
                {
                    SetNextDeadline();
                    return dec;
                }
            }
            return null;
        }
        // If a frame is fully received it will already be put in the queue so check for frames beyond the deadline
        DecodedRawFrameMulti foundFrame = null;
        foreach (var kvp in inProgessFrames.OrderByDescending(kvp => kvp.Key))
        {
            if(kvp.Value.TargetTimestamp >= (TimestampNextDeadline + MaxTimeBeforeIncompleteRender))
            {
                foundFrame = kvp.Value;
            }
        }
        if(foundFrame != null)
        {
            completeFrame(foundFrame, false);
            SetNextDeadline();
            return foundFrame;
        }
        return null;
    }

    public void SetNextDeadline()
    {
        TimestampNextDeadline += (1000 / FPS);
    }

    private void enhanceWithOldData(DecodedRawFrameMulti newestFrame) // Used when certains capturers were dropped or sent at a lower frame rate
    {
        if(previousFrame == null)
        {
            return;
        }
        if(newestFrame.TargetTimestamp - previousFrame.TargetTimestamp > holdPreviousFor)
        {
            previousFrame = null; // TODO Check if needed
            return;
        }
        for(uint i=0; i<previousFrame.NCapturers; i++)
        {
            DecodedRawFrameSingle sNew = newestFrame.GetSingle(i);
            if(sNew ==  null)
            {
                DecodedRawFrameSingle sOld = previousFrame.GetSingle(i);
                if(sOld != null)
                {
                    sNew = newestFrame.AddSingle(i, sOld.NPoints);
                    Array.Copy(previousFrame.Points, sOld.PointOffset, newestFrame.Points, sNew.PointOffset, sOld.NPoints);
                    Array.Copy(previousFrame.Colors, sOld.PointOffset, newestFrame.Colors, sNew.PointOffset, sOld.NPoints);
                }
            }
        }
    }

    private void completeFrame(DecodedRawFrameMulti newestFrame, bool addToQueue)
    {
        List<uint> framesToDelete = inProgessFrames.Keys.Where(k => k <= newestFrame.FrameNr).ToList();
        foreach(var frame in framesToDelete)
        {
            inProgessFrames.Remove(frame);
        }
        if(UsePreviousFrameData)
        {
            previousFrame = newestFrame;
        }
        if(addToQueue)
        {
            queue.Enqueue(newestFrame);
        }
       
    }
}
