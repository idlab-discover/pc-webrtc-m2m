using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.IO;
using System.Runtime.InteropServices;
using Newtonsoft.Json;
using JetBrains.Annotations;
using Newtonsoft.Json.Linq;

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

    public bool recordPosition;
    public PositionTrackerConfig positionTrackerConfig;
    public bool playbackPosition;
    public PositionPlaybackConfig positionPlaybackConfig;

    public FrameMode frameMode;
    public FrameCodec frameCodec;
    public List<ModeCodecPair> supportedModes; // Currently mdc or raw. In future maybe just point cloud aswell
    public List<string> supportedCapturers;
    public List<CapturerConfigPair> capturerConfigs;
    public bool useMultiCam = false;
    public uint activeCamIndex = 0;
    public string playerControllerName = "simple";
    public string capturerName = "artificial";
    public string artificalConfigPath = "config/camera/artificial.json";
    public string realsenseConfigPath = "config/camera/realsense.json";
    public string prerecordedKinectConfigPath = "config/camera/prerecKinect.json";
    public string plyFilesConfigPath = "config/camera/plyfiles.json";
    public string providersConfigPath = "config/providers/prov.json";
    //public ArtificialSettings artificialSettings;
    //public RealsenseSettings realsenseSettings;
    //public PrerecordedKinectSettings prerecKinectSettings;
    public PlaybackBufferSettings playbackBufferSettings = new();
    public SessionManagerSettings sessionManagerSettings;
    public FrameCleanupSettings frameCleanupSettings;
    public RawEncodingSettings rawEncodingSettings;
    public AudioPlaybackParams audioPlayback;
    public static SessionInfo CreateFromJSON(string path)
    {
        return JsonConvert.DeserializeObject<SessionInfo>(File.ReadAllText(path));
    }

}


[System.Serializable]
public class CapturerConfigPair
{
    public string capturerName;
    public string configPath;
}

[System.Serializable]
public class ModeCodecPair
{
    public string modeName; 
    public List<string> supportedCodecs;
    public JObject modeSettings; // e.g. mdc sampling percentages which are then used as track settings. Also limit PC to X points maybe
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
public class PlaybackBufferSettings
{
    public uint holdFramePreviousFor = 100;
    public bool usePreviousFrameData = false;
    public uint maxTimeBeforeIncompleteRender = 20;
    public bool enqueueImmediately = false;
    public bool renderIncompleteFrames = true;
}

[System.Serializable]
public class SessionManagerSettings
{
    public string type = "loopback";
    public string defaultName = "TestSession";
    public string configPath = "/config/session/loopback_0.json";
}

[System.Serializable]
public class LoggerSettings
{
    public string logPath = "";
    public bool appendTimestampToPath = true;
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

[System.Serializable]
public class MicSettings
{
    public bool usePrerecorded; // If true, deviceName links to the a file
    public string deviceName;
    public string audioProcessor; // FMOD, Unity, WWise
}

[System.Serializable]
public class PositionTrackerConfig
{
    public string outputPath;
    public string outputFileName = "position_tracker_info";
    public bool addTimestampToPath = true;
    public float recordInterval = 0.1f; // in seconds
    public string outputFormat = "json"; // JSON or binary
    public bool writeContinously = true; // If false, write only on stops
}

[System.Serializable]
public class PositionPlaybackConfig
{
    public string inputFile;
    public bool readContinously = false; // If false, read all at start
}