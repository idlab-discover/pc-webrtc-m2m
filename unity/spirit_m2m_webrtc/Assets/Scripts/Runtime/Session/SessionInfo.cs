using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.IO;
[System.Serializable]
public class SessionInfo
{
    public int clientID;
    public StartPosition[] startPositions;
    public Table table;
    public string sfuAddress;
    public int peerUDPPort;
    public float camClose;
    public float camFar;
    public uint camWidth;
    public uint camHeight;
    public uint camFPS;
    public bool useCam;
    public bool useMic;
    public AudioPlaybackParams audioPlayback;
    public static SessionInfo CreateFromJSON(string path)
    {
        return JsonUtility.FromJson<SessionInfo>(File.ReadAllText(path));
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