using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public abstract class PipelineRemotePointcloudBase : PipelineRemoteBase
{
    protected PointCloudBuffer playbackBuffer;
    [SerializeField]
    private PointCloudRenderer pointCloudRendererPrefab;

    public override void Init(SessionInfo sessionInfo, RemoteConnectedClient client)
    {
        base.Init(sessionInfo, client);
        this.playbackBuffer = new PointCloudBuffer(sessionInfo.playbackBufferSettings, 0, 30);
        pointCloudRendererPrefab.Init(client.ClientID);
    }

    private void Update()
    {
        RenderablePointCloud completedFrame = playbackBuffer.CheckForCompletedFrames();
        if (completedFrame != null)
        {
            Logger.LogPCFrameStatusWithMessageLimited(NAME, Logger.Status.EndEncodingPC, RemoteClient.ClientID, completedFrame.FrameNr, completedFrame.ToString());
            pointCloudRendererPrefab.SetPointCloud(completedFrame);
        } 
    }
}
