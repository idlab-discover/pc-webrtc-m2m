using Pcx;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;

public class MDCPointCloudReceiverSingle
{

    private readonly object _lock = new();
    private readonly List<RemoteTrackInfo> tracksForCapturer;
    private readonly PointCloudBuffer playbackBuffer;
    private Dictionary<UInt32, MDCDecodedPointCloudSingle> inProgessFrames = new();



    public MDCPointCloudReceiverSingle(List<RemoteTrackInfo> tracksForCapturer, PointCloudBuffer playbackBuffer)
    {
        this.tracksForCapturer = tracksForCapturer;
        this.playbackBuffer = playbackBuffer;
        this.playbackBuffer.NActiveCapturers = 1; // TODO Make this changeable
        foreach (var track in tracksForCapturer)
        {
           track.StartPollingTrack((frame) => {
               uint nDescriptions = 0;
               MDCFrameHeader header = new(frame);
               Debug.Log("received frame with header:" + header.ToString());
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
                   DecodedMDCDescription description = new(header, true);
                   frame.Dispose(); // TODO Make it so it can also not be disposed
                   singleFrame.AddDescription(description);

                   description.Dispose();

               }
           });
        }
    }


   
}
