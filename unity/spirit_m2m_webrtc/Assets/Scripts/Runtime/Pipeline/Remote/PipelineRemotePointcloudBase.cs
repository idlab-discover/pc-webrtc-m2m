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
        Logger.LogStatusClient(NAME, Logger.Status.Initializing, client.ClientID);
        base.Init(sessionInfo, client);
        this.playbackBuffer = new PointCloudBuffer(sessionInfo.playbackBufferSettings, 0, 30);
        pointCloudRendererPrefab.Init(client.ClientID);
        Logger.LogStatusClient(NAME, Logger.Status.Initialized, client.ClientID);
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
