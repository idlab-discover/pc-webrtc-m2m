using Newtonsoft.Json;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

public static class CaptureFactory
{
    private static FrameCleanupSettingsEx makeFrameCleanupEx(FrameCleanupSettings frameCleanupSettings)
    {
        return new FrameCleanupSettingsEx
        {
            blackoutBlockSize = frameCleanupSettings.blackoutBlockSize,
            shouldApplyDepthFilter = frameCleanupSettings.shouldApplyDepthFilter,
            shouldBlackout = frameCleanupSettings.shouldBlackout,
            shouldCleanupDepth = frameCleanupSettings.shouldCleanupDepth
        };
    }
    public static SingleCapture CreateNewSingleCapture(SessionInfo sessionInfo, bool startCaptureThread=true)
    {
        FrameCleanupSettingsEx frameCleanupSettings = makeFrameCleanupEx(sessionInfo.frameCleanupSettings);
        switch (sessionInfo.capturerName.ToLower())
        {
            case "artificial":
                {
                    string fullPath = Application.dataPath + "/" + sessionInfo.artificalConfigPath;
                    var set = JsonConvert.DeserializeObject<ArtificialSettings[]>(File.ReadAllText(fullPath));
                    if(set != null && set.Length > sessionInfo.activeCamIndex) {
                        return new ArtificialCapture(sessionInfo.camFPS, sessionInfo.frameMode, frameCleanupSettings, set[sessionInfo.activeCamIndex], startCaptureThread);
                    }
                    break;
                }
            case "realsense":
                {
                    string fullPath = Application.dataPath + "/" + sessionInfo.realsenseConfigPath;
                    var set = JsonConvert.DeserializeObject<RealsenseSettings[]>(File.ReadAllText(fullPath));
                    if (set != null && set.Length > sessionInfo.activeCamIndex)
                    {
                        return new RealsenseCapture(sessionInfo.camFPS, sessionInfo.frameMode, frameCleanupSettings, set[sessionInfo.activeCamIndex], startCaptureThread);
                    }
                    break;
                }
            case "prerec_kinect":
                {
                    string fullPath = Application.dataPath + "/" + sessionInfo.prerecordedKinectConfigPath;
                    var set = JsonConvert.DeserializeObject<PrerecordedKinectSettings[]>(File.ReadAllText(fullPath));
                    if (set != null && set.Length > sessionInfo.activeCamIndex)
                    {
                        return new PrerecordedKinectCapture(sessionInfo.camFPS, sessionInfo.frameMode, frameCleanupSettings, set[sessionInfo.activeCamIndex], startCaptureThread);
                    }
                    break;
                }
        }
        Debug.LogError("Could not create single capturer");
        return null;
    }

    public static MultiCaptureSetup CreateNewMultiCapture(SessionInfo sessionInfo, bool startCaptureThread)
    {
        FrameCleanupSettingsEx frameCleanupSettings = makeFrameCleanupEx(sessionInfo.frameCleanupSettings);
        List<MultiCaptureSingleCam> captures = new List<MultiCaptureSingleCam>();
       
        switch (sessionInfo.capturerName.ToLower())
        {
            case "artificial":
                {
                    string fullPath = Application.dataPath + "/" + sessionInfo.artificalConfigPath;
                    var set = JsonConvert.DeserializeObject<ArtificialSettings[]>(File.ReadAllText(fullPath));
                    for (int i = 0; i < set.Length; i++)
                    {
                        var f = set[i];
                        if (f != null)
                        {
                            captures.Add(new MultiSingleArtificial(f));
                        }
                    }
                    break;
                }
            case "realsense":
                {
                    string fullPath = Application.dataPath + "/" + sessionInfo.realsenseConfigPath;
                    var set = JsonConvert.DeserializeObject<RealsenseSettings[]>(File.ReadAllText(fullPath));
                    for (int i = 0; i < set.Length; i++)
                    {
                        var f = set[i];
                        if (f != null)
                        {
                            captures.Add(new MultiSingleRealsense(f));
                        }
                    }
                    break;
                }
            case "prerec_kinect":
                {
                    string fullPath = Application.dataPath + "/" + sessionInfo.prerecordedKinectConfigPath;
                    var set = JsonConvert.DeserializeObject<PrerecordedKinectSettings[]>(File.ReadAllText(fullPath));
                    for (int i = 0; i < set.Length; i++)
                    {
                        var f = set[i];
                        if (f != null)
                        {
                            captures.Add(new MultiCaptureSinglePrerecKinect(f));
                        }
                    }
                    break;
                }
        }
        if(captures.Count == 0)
        {
            Debug.LogError("Could not create multi capturer");
            return null;
        }
        return new MultiCaptureSetup(sessionInfo.camFPS, sessionInfo.frameMode, frameCleanupSettings, captures, startCaptureThread);
    }
}
