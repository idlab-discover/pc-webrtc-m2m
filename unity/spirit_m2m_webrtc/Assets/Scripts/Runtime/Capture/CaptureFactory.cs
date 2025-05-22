using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public static class CaptureFactory
{
    public static SingleCapture CreateNewSingleCapture(SessionInfo sessionInfo)
    {
        FrameCleanupSettingsEx frameCleanupSettings = new FrameCleanupSettingsEx
        {
            blackoutBlockSize = sessionInfo.frameCleanupSettings.blackoutBlockSize,
            shouldApplyDepthFilter = sessionInfo.frameCleanupSettings.shouldApplyDepthFilter,
            shouldBlackout = sessionInfo.frameCleanupSettings.shouldBlackout,
            shouldCleanupDepth = sessionInfo.frameCleanupSettings.shouldCleanupDepth
        };
        switch (sessionInfo.capturerName.ToLower())
        {
            case "artificial":
                {
                    var set = sessionInfo.artificialSettings;
                    return new ArtificialCapture(sessionInfo.camFPS, sessionInfo.frameMode, frameCleanupSettings, set);
                }
            case "realsense":
                {
                    var set = sessionInfo.realsenseSettings;
                    return new RealsenseCapture(sessionInfo.camFPS, sessionInfo.frameMode, frameCleanupSettings, set);
                }
            case "prerec_kinect":
                {
                    var set = sessionInfo.prerecKinectSettings;
                    return new PrerecordedKinectCapture(sessionInfo.camFPS, sessionInfo.frameMode, frameCleanupSettings, set);
                }
        }
        return null;
    }
    }
