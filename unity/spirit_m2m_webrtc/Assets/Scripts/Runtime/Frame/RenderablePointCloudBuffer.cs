using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public abstract class RenderablePointCloudBuffer
{
    public uint HoldPreviousFor;
    public ulong TimestampNextDeadline;
    public bool UsePreviousFrameData;
    public uint FPS;
    public uint MaxTimeBeforeIncompleteRender;
    public bool EnqueueImmediately;
    public bool RenderIncompleteFrames;

    public RenderablePointCloudBuffer(PlaybackBufferSettings settings, ulong timestampNextDeadline, uint fps)
    {
        HoldPreviousFor = settings.holdFramePreviousFor;
        UsePreviousFrameData = settings.usePreviousFrameData;
        FPS = fps;
        MaxTimeBeforeIncompleteRender = settings.maxTimeBeforeIncompleteRender;
        EnqueueImmediately = settings.enqueueImmediately;
        RenderIncompleteFrames = settings.renderIncompleteFrames;
    }

    public abstract RenderablePointCloud CheckForCompletedFrames();
}
