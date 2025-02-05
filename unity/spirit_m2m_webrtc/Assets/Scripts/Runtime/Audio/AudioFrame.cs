using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AudioFrame 
{
    public UInt32 FrameNr { get; private set; }
    public UInt64 Timestamp { get; private set; }
    public float[] AudioData { get; private set; }

    public AudioFrame(UInt32 frameNr, UInt64 timestamp, float[] audioData)
    {
        FrameNr = frameNr;
        Timestamp = timestamp;
        AudioData = audioData;
    }
}
