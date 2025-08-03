using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Xml.Linq;
using UnityEngine;

public class RawFrameBuffer : RenderablePointCloudBuffer
{
    private DecodedRawFrameMulti previousFrame = null; // Used to potentially repair next frames

    private Dictionary<UInt32, DecodedRawFrameMulti> inProgessFrames = new();
    
    public ConcurrentQueue<DecodedRawFrameMulti> queue = new();
    
    
    public uint NActiveCapturers;
    
    private Mutex mut = new Mutex();
    public RawFrameBuffer(PlaybackBufferSettings settings, ulong timestampNextDeadline, uint fps) : base(settings, timestampNextDeadline, fps)
    {
            
    }

    /*public void AddDecodedColors(IntPtr decodedColor, uint captureID, uint frameNr, uint nPoints, ulong targetTimestamp)
    {
        DecodedRawFrameSingle dS = AddSingleToRawFrame(captureID, frameNr, nPoints, targetTimestamp);
        dS.DecodedColor = decodedColor;
        dS.ColorsCompleted = true;
    }

    public void AddDecodedDepth(IntPtr decodedDepth, uint captureID, uint frameNr, uint nPoints, ulong targetTimestamp)
    {
        DecodedRawFrameSingle dS = AddSingleToRawFrame(captureID, frameNr, nPoints, targetTimestamp);
        dS.DecodedDepth = decodedDepth;
        dS.DepthCompleted = true;
    }*/
    public void AddSingleAndConvert(DecodedRawFrameSingle s, RawConverter c)
    {
        mut.WaitOne();
        bool succes = inProgessFrames.TryGetValue(s.FrameNr, out DecodedRawFrameMulti d);
        if (!succes)
        {
            d = new DecodedRawFrameMulti(NActiveCapturers, s.FrameNr, 0);
            inProgessFrames.Add(s.FrameNr, d);
        }
        d.AddAndConvertSingle(s, c);
        mut.ReleaseMutex();

    }
    private DecodedRawFrameSingle AddSingleToRawFrame(uint captureID, uint frameNr, uint nPoints, ulong targetTimestamp)
    {
        bool succes = inProgessFrames.TryGetValue(frameNr, out DecodedRawFrameMulti d);
        if (!succes)
        {
            d = new DecodedRawFrameMulti(NActiveCapturers, frameNr, targetTimestamp);
            inProgessFrames.Add(frameNr, d);
        }
        return d.AddSingle(captureID, nPoints);
    }

    public override RenderablePointCloud CheckForCompletedFrames()
    {
        if(!queue.IsEmpty)
        {
            bool succes = queue.TryDequeue(out DecodedRawFrameMulti dec);
            if (succes)
            {
                if(EnqueueImmediately || (dec.TargetTimestamp >= TimestampNextDeadline))
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
        if(newestFrame.TargetTimestamp - previousFrame.TargetTimestamp > HoldPreviousFor)
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
