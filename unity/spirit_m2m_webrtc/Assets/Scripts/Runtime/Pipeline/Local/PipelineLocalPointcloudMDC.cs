using AOT;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;

[PipelineLocalRegister("mdc")]
public class PipelineLocalPointcloudMDC : PipelineLocalPointcloudBase
{
    protected override string NAME => "PipelineLocalPointcloudMDC";
    private MDCEncodingQueue encodingQueue; // TODO move this to video capture
    // TODO Add audio capture 
    private readonly object _lock = new();

    protected override FrameMode FrameMode => FrameMode.RealData;

    // Metrics
    private CompositeMetricDefinition<CompMDCEncodingDone> mdcEncodingDoneMetric;

    public override void Init(SessionInfo sessionInfo, LocalConnectedClient localClient)
    {   
        mdcEncodingDoneMetric = MetricController.MetricServerConnection.RegisterCompositePushMetric<CompMDCEncodingDone>("MDCEncodingDone");
        sessionInfo.frameMode = FrameMode.RealData; // TODO fix this in the future
        base.Init(sessionInfo, localClient);
        encodingQueue = new MDCEncodingQueue(sessionInfo);
        encodingQueue.SetDescriptionDoneCallback(OnDescriptionDoneCallback);
        startPollThread();
    }

    protected override void pollFramesInternal()
    {
        IntPtr frame = capture.PollNextPointCloud();

        if (frame != IntPtr.Zero)
        {
            // TODO Maybe add metrics from here as well, like number of points, timestamp, etc.
           // uint nPoints = Realsense2Invoker.get_point_cloud_size(frame);
            //Debug.Log($"Number of points: {nPoints}");
            //Debug.Log($"Got point cloud frame from capture, ts: {DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}");
            encodingQueue.EncodePointCloud(frame);
        } 
        else
        {
            keepWorking = false;
            encodingQueue.Dispose(); // TODO make cleanup cleaner
        }
    }

    // TODO move this to mdc encoder queue class
    private void OnDescriptionDoneCallback(EncodedMDCDescription desc)
    {
        string trackID = $"cl{LocalClient.ClientID}_mdc_0_{desc.Header.DescriptionNr}";
        
        if (keepWorking)
        {
            Logger.LogPCFrameStatusWithMessageLimited(NAME, Logger.Status.EndEncodingPC, LocalClient.ClientID, desc.Header.FrameNr, desc.Header.ToString());
            mdcEncodingDoneMetric?.AddCapturedValue(new CompMDCEncodingDone
            {
                capturingTimestamp = (long)desc.Header.Timestamp,
                encodingDoneTimestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                capturerID = LocalClient.ClientID,
                frameNr = desc.Header.FrameNr,
                descriptionID = desc.Header.DescriptionNr,
                dataBufferSize = (uint)desc.Bytes.Length,
                totalNumberOfPoints = desc.Header.TotalNumberOfPoints
            });
            int nSend = 0;
            if (desc.Header.FrameNr % 100 == 0)
            {
                Debug.Log($"{desc.Header.FrameNr} {desc.Header.DescriptionNr} {desc.Header.DataBufferSize}");
            }
            
            unsafe
            {
                fixed (byte* bufferPointer = desc.Bytes)
                {
                   nSend = LocalClient.SendVideoData(trackID, desc.Header.FrameNr, new IntPtr(bufferPointer), (uint)desc.Bytes.Length);
                }
            }

            if (nSend == -1)
            {
                keepWorking = false;
                Debug.Log("Stop capturing");
            }
            Logger.LogPCFrameStatusWithMessageLimited(NAME, Logger.Status.FrameAddedToSender, LocalClient.ClientID, desc.Header.FrameNr, desc.Header.ToStringSmall()); // TODO This is currently still using capturerID, change this to reflect it better
        }
        desc.Dispose();
    }
    protected override void cleanup()
    {
        base.cleanup();
        //encodingQueue.Dispose();
    }

}
