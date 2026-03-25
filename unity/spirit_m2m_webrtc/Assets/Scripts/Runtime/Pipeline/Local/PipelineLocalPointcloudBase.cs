using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public abstract class PipelineLocalPointcloudBase : PipelineLocalBase
{
    protected MultiCaptureSetup capture;
    private System.Threading.Thread pollThread;
    protected bool keepWorking = true;
    protected abstract FrameMode FrameMode { get; }
    protected bool startCaptureThread = true;
    public override void Init(SessionInfo sessionInfo, LocalConnectedClient localClient)
    {
        base.Init(sessionInfo, localClient);
        capture = CaptureFactory.CreateNewMultiCapture(sessionInfo, startCaptureThread);
        if (capture == null)
        {
            Debug.LogError("Failed to create MultiCaptureSetup");
            return;
        }
        
    }
    protected override void cleanup()
    {
        base.cleanup();
        keepWorking = false;
        // TODO maybe dispose of camera here once thread is stopped?
    }
    
    protected void startPollThread()
    {
        pollThread = new System.Threading.Thread(pollFrames);
        pollThread.Start();
    }
    private void pollFrames()
    {
        while (keepWorking)
        {
            Debug.Log("Polling frames...");
            pollFramesInternal();
        }
        if (capture != null)
        {
            capture.Dispose();
            capture = null;
        }
    }
    protected abstract void pollFramesInternal();
}
