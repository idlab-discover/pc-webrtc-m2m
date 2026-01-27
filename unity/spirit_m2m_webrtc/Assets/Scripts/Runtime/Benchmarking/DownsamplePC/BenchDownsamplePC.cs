using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using Unity.VisualScripting;
using UnityEngine;

public class BenchDownsamplePC : PipelineLocalPointcloudBase
{
    private BenchDownsamplePCConfig benchConfig;
    public string ConfigPath;
    protected override string NAME => "BenchDownsamplePC";
    private bool pollNextFrame = false;
    private SessionInfo sessionInfo;
    private uint frameCounter = 0;
    // TODO Add audio capture 
    private readonly object _lock = new();

    protected override FrameMode FrameMode => FrameMode.RealData;

    void Start()
    {
        sessionInfo = SessionInfo.CreateFromJSON(Application.dataPath + "/config/session_config.json");
        Logger.Init(sessionInfo.loggerSettings);
        benchConfig = BenchDownsamplePCConfig.CreateFromJSON(ConfigPath);
        startCaptureThread = false; // We want to control when the capture starts
        Init(sessionInfo, new LocalConnectedClient(0, ""));
    }

    public override void Init(SessionInfo sessionInfo, LocalConnectedClient localClient)
    {
        sessionInfo.frameMode = FrameMode.RealData; // TODO fix this in the future
        base.Init(sessionInfo, localClient);
        startPollThread();
    }

    protected override void pollFramesInternal()
    { 
        if (!keepWorking)
        {
            return;
        }
        IntPtr pc = capture.GetSingleCombinedPointCloud();
        if (pc != IntPtr.Zero)
        {
            Realsense2Invoker.downsample_pc_random(pc, benchConfig.nPointsPerFrame, false);
            string fileName = $"{benchConfig.outputPath}/{(!string.IsNullOrEmpty(benchConfig.filePrefix) ? benchConfig.filePrefix + "_" : "")}{benchConfig.nPointsPerFrame}_{frameCounter:D4}.ply";
            Realsense2Invoker.save_pc_to_ply(fileName, pc);
            Debug.Log($"Saved point frame to {fileName}");
            frameCounter++;
            Realsense2Invoker.free_point_cloud(pc);
        }
        else
        {
            keepWorking = false;
        }
        if(frameCounter >= benchConfig.maxFrames)
        {
            keepWorking = false;
        }

    }

   

    protected override void cleanup()
    {
        base.cleanup();
        lock (_lock)
        {
            keepWorking = false;
        }
    }
}
