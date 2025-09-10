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
    private MDCEncodingQueue encodingQueue; // TODO move this to video caputre
    // TODO Add audio capture 
    private readonly object _lock = new();

    protected override FrameMode FrameMode => FrameMode.RealData;

    

    public override void Init(SessionInfo sessionInfo, LocalConnectedClient localClient)
    {   

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
            uint nPoints = Realsense2Invoker.get_point_cloud_size(frame);
            //Debug.Log($"Number of points: {nPoints}");
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
            int nSend = 0;
            if (desc.Header.FrameNr % 100 == 0)
            {
                Debug.Log($"{desc.Header.FrameNr} {desc.Header.DescriptionNr} {desc.Header.DataBufferSize}");
            }
            
            unsafe
            {
                fixed (byte* bufferPointer = desc.Bytes)
                {
                   nSend = LocalClient.SendVideoData(trackID, new IntPtr(bufferPointer), (uint)desc.Bytes.Length);
                }
            }

            if (nSend == -1)
            {
                keepWorking = false;
                Debug.Log("Stop capturing");
            }
            Logger.LogPCFrameStatusWithMessageLimited(NAME, Logger.Status.FrameAddedToSender, LocalClient.ClientID, desc.Header.FrameNr, desc.Header.ToStringSmall());
        }
        desc.Dispose();
    }
    protected override void cleanup()
    {
        base.cleanup();
        //encodingQueue.Dispose();
    }

}
