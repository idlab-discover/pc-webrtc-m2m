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
    
    // Metrics
    private CompositeMetricDefinition<CompMDCRemoteFrameReceived> mdcRemoteFrameReceivedMetric;
    private CompositeMetricDefinition<CompMDCDecodingDone> mdcDecodingDoneMetric;
    private CompositeMetricDefinition<CompMDCLateReceived> mdcLateReceivedMetric;

    public MDCPointCloudReceiverSingle(List<RemoteTrackInfo> tracksForCapturer, PointCloudBuffer playbackBuffer, uint clientID)
    {
        this.tracksForCapturer = tracksForCapturer;
        this.playbackBuffer = playbackBuffer;
        this.playbackBuffer.NActiveCapturers = 1; // TODO Make this changeable
        this.clientID = clientID;   
        mdcRemoteFrameReceivedMetric = MetricController.MetricServerConnection.RegisterCompositePushMetric<CompMDCRemoteFrameReceived>("MDCRemoteFrameReceived");
        mdcDecodingDoneMetric = MetricController.MetricServerConnection.RegisterCompositePushMetric<CompMDCDecodingDone>("MDCDecodingDone");
        mdcLateReceivedMetric = MetricController.MetricServerConnection.RegisterCompositePushMetric<CompMDCLateReceived>("MDCLateReceived");

        foreach (var track in tracksForCapturer)
        {
           track.StartPollingTrack((frame) => {
               uint nDescriptions = 0;
               MDCFrameHeader header = new(frame);
               bool shouldDecode = playbackBuffer.ShouldDecodeFrame(header.FrameNr);
               if(!shouldDecode)
               {
                    mdcLateReceivedMetric?.AddCapturedValue(new CompMDCLateReceived
                    {
                        lateReceivedTimestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                        clientID = clientID,
                        capturerID = header.CapturerID,
                        frameNr = header.FrameNr,
                        descriptionID = header.DescriptionNr
                    });
                   frame.Dispose();
                   return;
               }
               mdcRemoteFrameReceivedMetric?.AddCapturedValue(new CompMDCRemoteFrameReceived
               {
                   receivingTimestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                   clientID = clientID,
                   capturerID = header.CapturerID,
                   frameNr = header.FrameNr,
                   descriptionID = header.DescriptionNr,
                   dataBufferSize = header.DataBufferSize,
                   totalNumberOfPoints = header.TotalNumberOfPoints
               });
               Logger.LogPCFrameStatusWithMessageLimited(NAME, Logger.Status.StartDecodingPC, clientID, header.FrameNr, header.ToString());
               DecodedMDCDescription description = new(header, true);
               
               // TODO Consider moving this into the decoder? That way it also works when insta decode is off
               mdcDecodingDoneMetric?.AddCapturedValue(new CompMDCDecodingDone
               {
                   decodingDoneTimestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                   clientID = clientID,
                   capturerID = header.CapturerID,
                   frameNr = header.FrameNr,
                   descriptionID = header.DescriptionNr
               });

               // TODO Make it so it can also not be disposed
               frame.Dispose();
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
                   DecodedPointCloudMulti m = playbackBuffer.GetMultiFrame(header.FrameNr, header.TotalNumberOfPoints);
                   MDCDecodedPointCloudSingle singleFrame = (MDCDecodedPointCloudSingle)m.GetSingle(header.FrameNr);
                   if (singleFrame == null)
                   {
                       singleFrame = new MDCDecodedPointCloudSingle(m, header.CapturerID, header.FrameNr, header.TotalNumberOfPoints, nDescriptions);
                       m.AddSingle(singleFrame);
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
