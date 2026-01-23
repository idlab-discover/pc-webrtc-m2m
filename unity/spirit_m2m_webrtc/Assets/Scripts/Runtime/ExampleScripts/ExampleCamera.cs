using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ExampleCamera : MonoBehaviour
{
    private const string NAME = "ExampleCamera";
    public string CameraConfigPath = Application.dataPath + "/config/session_config.json";
    private SingleCapture capture;
    private bool keepWorking = true;
    private System.Threading.Thread pollThread;
    private bool startCaptureThread = true;
    private SimpleRenderablePointCloudBuffer playbackBuffer;

    [SerializeField]
    private PointCloudRenderer pointCloudRendererPrefab;

    void Start()
    {
        if(pointCloudRendererPrefab == null)
        {
            Debug.LogError("PointCloudRendererPrefab is not assigned");
            return;
        }
        SessionInfo sessionInfo = SessionInfo.CreateFromJSON(CameraConfigPath);
        Logger.Init(sessionInfo.loggerSettings);
        capture = CaptureFactory.CreateNewSingleCapture(sessionInfo, startCaptureThread);
        if (capture == null)
        {
            Debug.LogError("Failed to create MultiCaptureSetup");
            return;
        }
        playbackBuffer = new SimpleRenderablePointCloudBuffer(sessionInfo.camFPS);
        pointCloudRendererPrefab.Init(0); // CkientID can be whatever for just testing
        startPollThread();

    }

    // Update is called once per frame
    void Update()
    {
        RenderablePointCloud completedFrame = playbackBuffer.CheckForCompletedFrames();
        if (completedFrame != null)
        {
            Logger.LogPCFrameStatusWithMessageLimited(NAME, Logger.Status.FrameReadyToRender, 0, completedFrame.FrameNr, completedFrame.ToString());
            pointCloudRendererPrefab.SetPointCloud(completedFrame);
        }
    }

    void OnDestroy()
    {
        keepWorking = false;
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
            IntPtr frame = capture.PollNextPointCloud();
            if (frame != IntPtr.Zero)
            {
                SimplePointCloud pc = new SimplePointCloud(frame);
                playbackBuffer.EnqueueFrame(pc);
            }
            else
            {
                keepWorking = false;
            }
        }
        if (capture != null)
        {
            capture.Dispose();
            capture = null;
        }
    }
}