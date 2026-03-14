using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public abstract class RenderablePointCloudPipeline : IDisposable
{
    protected readonly RenderablePointCloudBuffer playbackBuffer;
    protected uint clientID;
    protected bool disposedValue;

    public RenderablePointCloudPipeline(RenderablePointCloudBuffer playbackBuffer, uint clientID)
    {
        this.playbackBuffer = playbackBuffer;
        this.clientID = clientID;
        
    }

    public abstract void OnVideoTrackAdded(NetworkStreamerBase streamer, uint capturerID, uint descriptionID, CaptureType capType, string trackInfo);
    public abstract void OnVideoTrackRemoved(uint capturerID, uint descriptionID);

    public RenderablePointCloud CheckForCompletedFrames()
    {
        return playbackBuffer.CheckForCompletedFrames();
    }

    protected abstract void Dispose(bool disposing);
    public void Dispose()
    {
        // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }
}
