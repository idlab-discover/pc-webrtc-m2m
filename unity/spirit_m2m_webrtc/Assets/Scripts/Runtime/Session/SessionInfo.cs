using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.IO;
using System.Runtime.InteropServices;
using Newtonsoft.Json;
[System.Serializable]
public class SessionInfo
{
    public int clientID;
    public StartPosition[] startPositions;
    public Table table;
    public StartPosition pushBack;
    public string sfuAddress;
    public int peerUDPPort;


    public uint camFPS;
    public bool useMic;

    public LoggerSettings loggerSettings;

    public FrameMode frameMode;
    public FrameCodec frameCodec;
    public bool useMultiCam = false;
    public uint activeCamIndex = 0;
    public string capturerName = "artificial";
    public string artificalConfigPath = "config/camera/artificial.json";
    public string realsenseConfigPath = "config/camera/realsense.json";
    public string prerecordedKinectConfigPath = "config/camera/prerecKinect.json";
    //public ArtificialSettings artificialSettings;
    //public RealsenseSettings realsenseSettings;
    //public PrerecordedKinectSettings prerecKinectSettings;
    public FrameCleanupSettings frameCleanupSettings;
    public RawEncodingSettings rawEncodingSettings;
    public AudioPlaybackParams audioPlayback;
    public static SessionInfo CreateFromJSON(string path)
    {
        return JsonConvert.DeserializeObject<SessionInfo>(File.ReadAllText(path));
    }

}

[System.Serializable]
public class StartPosition
{
    public float x;
    public float y;
    public float z;
}
[System.Serializable]
public class Table
{
    public StartPosition position;
    public StartPosition scale;
}

[System.Serializable]
public class AudioPlaybackParams
{
    public bool useAudio;
    public uint maxQueueSize;
    public uint targetSamples;
    public uint audioDelay;
    public float minPlaybackFrequency;
    public float maxPlaybackFrequency;
    public float latencyPlaybackMultiplier;
    public uint defaultPlaybackFrequency;
    public bool ignoreTargetDelay;
    public bool ignoreJitter;
    public bool doNotWaitForPc;
    public uint sampleSizeInBytes;

    public string codecName;
    public uint dspSize;
    public bool forceStart;
}

[System.Serializable]
public class RawEncodingSettings
{
    public string colorCodecName = "jpeg";
    public string depthCodecName = "vle";
    public uint maxQueueSize = 2; // In number of frames
    public uint nWorkers = 3; // In number of threads
    public JPEGSettings jpegSettings;
    public WebPSettings webPSettings;
}

[System.Serializable]
public class FrameCleanupSettings
{
    public uint blackoutBlockSize;
    public bool shouldApplyDepthFilter;
    public bool shouldCleanupDepth;
    public bool shouldBlackout;
}





[System.Serializable]
public class JPEGSettings
{
    public uint quality = 75;
}

[System.Serializable]
public class WebPSettings
{
    public uint quality = 75;
    public uint method = 0; // From 0 (fastest / worst compression) to 6 (slow / best compression)

}

[System.Serializable]
public class LoggerSettings
{
    public string logPath = "";
    public uint flushInterval = 100; // in ms
    public LoggerSettingsPointCloud pointCloud;
    public LoggerSettingsAudio audio;
}

[System.Serializable]
public class LoggerSettingsPointCloud
{
    public bool limitLogging = true; // Limits it to everyNFrames
    public uint everyNFrames = 10;
}

[System.Serializable]
public class LoggerSettingsAudio
{
    public bool limitLogging = true; // Limits it to everyNFrames
    public uint everyNFrames = 100;
}