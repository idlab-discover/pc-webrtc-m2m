using Pcx;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;

public class MDCPointCloudReceiverSingle
{
    private const string NAME = "MDCPointCloudReceiverSingle";

    private readonly object _lock = new();
    private readonly List<RemoteTrackInfo> tracksForCapturer;
    private readonly PointCloudBuffer playbackBuffer;
    private Dictionary<UInt32, MDCDecodedPointCloudSingle> inProgessFrames = new();
    private readonly uint clientID;


    public MDCPointCloudReceiverSingle(List<RemoteTrackInfo> tracksForCapturer, PointCloudBuffer playbackBuffer, uint clientID)
    {
        this.tracksForCapturer = tracksForCapturer;
        this.playbackBuffer = playbackBuffer;
        this.playbackBuffer.NActiveCapturers = 1; // TODO Make this changeable
        this.clientID = clientID;   
        foreach (var track in tracksForCapturer)
        {
           track.StartPollingTrack((frame) => {
               uint nDescriptions = 0;
               MDCFrameHeader header = new(frame);
               Logger.SetFlushAll();
               Logger.LogPCFrameStatusWithMessageLimited(NAME, Logger.Status.StartDecodingPC, clientID, header.FrameNr, header.ToString());
               DecodedMDCDescription description = new(header, true);
               Logger.LogPCFrameStatusWithMessageLimited(NAME, Logger.Status.EndDecodingPC, clientID, header.FrameNr, header.ToStringSmall());
               frame.Dispose(); // TODO Make it so it can also not be disposed
               Logger.LogPCFrameStatusWithMessageLimited(NAME, Logger.Status.EndDecodingPC, clientID, header.FrameNr, header.ToStringSmall());
               lock (_lock)
               {
                   foreach (var track in tracksForCapturer)
                   {
                       if (track.status == TrackStatus.Started)
                       {
                           nDescriptions++;
                       }
                   }
                   if (!inProgessFrames.TryGetValue(header.FrameNr, out MDCDecodedPointCloudSingle singleFrame))
                   {
                       DecodedPointCloudMulti m = playbackBuffer.GetMultiFrame(header.FrameNr, header.TotalNumberOfPoints);
                       singleFrame = new MDCDecodedPointCloudSingle(m, header.CapturerID, header.FrameNr, header.TotalNumberOfPoints, nDescriptions);
                       m.AddSingle(singleFrame);
                       inProgessFrames.Add(header.FrameNr, singleFrame);
                   }
                  
                   singleFrame.AddDescription(description);
                   description.Dispose();
                   if(singleFrame.IsCompleted && singleFrame.IsParentCompleted)
                   {
                       playbackBuffer.CompleteFrame(singleFrame.Parent, true);
                       Logger.LogPCFrameStatusLimited(NAME, Logger.Status.FrameEnqueued, clientID, header.FrameNr);
                   }

               }
           });
        }
    }


   
}
