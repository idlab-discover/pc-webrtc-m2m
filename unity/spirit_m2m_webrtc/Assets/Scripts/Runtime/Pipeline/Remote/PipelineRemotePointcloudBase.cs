using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public abstract class PipelineRemotePointcloudBase : PipelineRemoteBase
{
    protected PointCloudBuffer playbackBuffer;
    [SerializeField]
    private PointCloudRenderer pointCloudRendererPrefab;
     // Metrics
    private CompositeMetricDefinition<CompMDCReadyToRender> mdcEncodingDoneMetric;

    public override void Init(SessionInfo sessionInfo, RemoteConnectedClient client)
    {
        Logger.LogStatusClient(NAME, Logger.Status.Initializing, client.ClientID);
        base.Init(sessionInfo, client);
        this.playbackBuffer = new PointCloudBuffer(sessionInfo.playbackBufferSettings, 0, 15);
        pointCloudRendererPrefab.Init(client.ClientID);
        Logger.LogStatusClient(NAME, Logger.Status.Initialized, client.ClientID);
        mdcEncodingDoneMetric = MetricController.MetricServerConnection.RegisterCompositePushMetric<CompMDCReadyToRender>("MDCReadyToRender");
    }

    private void Update()
    {
        RenderablePointCloud completedFrame = playbackBuffer.CheckForCompletedFrames();
        if (completedFrame != null)
        {
            Logger.LogPCFrameStatusWithMessageLimited(NAME, Logger.Status.FrameReadyToRender, RemoteClient.ClientID, completedFrame.FrameNr, completedFrame.ToString());
            pointCloudRendererPrefab.SetPointCloud(completedFrame);
            mdcEncodingDoneMetric?.AddCapturedValue(new CompMDCReadyToRender
            {
                readyToRenderTimestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                clientID = RemoteClient.ClientID,
                frameNr = completedFrame.FrameNr,
                totalNumberOfPoints = completedFrame.TotalPoints,
                qualityLevel = completedFrame.Quality
            });
            completedFrame.Dispose();
        } 
    }
}
