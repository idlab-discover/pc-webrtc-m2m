//--------------------------------------------------------------------
//
// This is a Unity behavior script that demonstrates how to record
// continuously and play back the same data while keeping a specified
// latency between the two. This is achieved by delaying the start of
// playback until the specified number of milliseconds has been
// recorded. At runtime the playback speed will be slightly altered
// to compensate for any drift in either play or record drivers.
//
// Add this script to a Game Object in a Unity scene and play the
// Editor. Recording will start with the scene and playback will
// start after the defined latency.
//
// This document assumes familiarity with Unity scripting. See
// https://unity3d.com/learn/tutorials/topics/scripting for resources
// on learning Unity scripting.
//
//--------------------------------------------------------------------

using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;

public class ScriptUsageRecordMicrophone : MonoBehaviour
{
    private uint LATENCY_MS = 50;
    private uint DRIFT_MS = 1;

    private uint samplesRecorded, samplesPlayed = 0;
    private int nativeRate, nativeChannels = 0;
    private uint recSoundLength = 0;
    uint lastPlayPos = 0;
    uint lastRecordPos = 0;
    private uint driftThreshold = 0;
    private uint desiredLatency = 0;
    private uint adjustLatency = 0;
    private int actualLatency = 0;

    private FMOD.CREATESOUNDEXINFO exInfo = new FMOD.CREATESOUNDEXINFO();

    private FMOD.Sound recSound;
    private FMOD.Channel channel;

    static FMOD.RESULT PlaybackDSPReadCallback(IntPtr sound, IntPtr inbuffer,  uint length)
    {
        Debug.Log("test");
        return FMOD.RESULT.OK;
    }

    // Start is called before the first frame update
    void Start()
    {
        /*
            Determine latency in samples.
        */
        FMODUnity.RuntimeManager.CoreSystem.getRecordDriverInfo(0, out _, 0, out _, out nativeRate, out _, out nativeChannels, out _);

        driftThreshold = (uint)(nativeRate * DRIFT_MS) / 1000;
        desiredLatency = (uint)(nativeRate * LATENCY_MS) / 1000;
        adjustLatency = desiredLatency;
        actualLatency = (int)desiredLatency;
        Debug.Log($"NATIVE RATE {actualLatency}");
        /*
            Create user sound to record into, then start recording.
        */
        exInfo.cbsize = Marshal.SizeOf(typeof(FMOD.CREATESOUNDEXINFO));
        exInfo.numchannels = nativeChannels;
        exInfo.format = FMOD.SOUND_FORMAT.PCMFLOAT;
        exInfo.defaultfrequency = nativeRate;
        exInfo.length = (uint)(nativeRate * sizeof(short) * nativeChannels);
        exInfo.pcmreadcallback = PlaybackDSPReadCallback;
        FMODUnity.RuntimeManager.CoreSystem.createSound("", FMOD.MODE.LOOP_NORMAL | FMOD.MODE.OPENUSER, ref exInfo, out recSound);
   
        FMODUnity.RuntimeManager.CoreSystem.recordStart(0, recSound, true);
        recSound.getLength(out recSoundLength, FMOD.TIMEUNIT.PCM);
    }

    // Update is called once per frame
    void Update()
    {
        /*
            Determine how much has been recorded since we last checked
        */
        uint recordPos = 0;
        FMODUnity.RuntimeManager.CoreSystem.getRecordPosition(0, out recordPos);

        uint recordDelta = (recordPos >= lastRecordPos) ? (recordPos - lastRecordPos) : (recordPos + recSoundLength - lastRecordPos);
        lastRecordPos = recordPos;
        samplesRecorded += recordDelta;

        uint minRecordDelta = 0;
        if (recordDelta != 0 && (recordDelta < minRecordDelta))
        {
            minRecordDelta = recordDelta; // Smallest driver granularity seen so far
            adjustLatency = (recordDelta <= desiredLatency) ? desiredLatency : recordDelta; // Adjust our latency if driver granularity is high
        }

        /*
            Delay playback until our desired latency is reached.
        */
        if (!channel.hasHandle() && samplesRecorded >= adjustLatency)
        {
            FMODUnity.RuntimeManager.CoreSystem.getMasterChannelGroup(out FMOD.ChannelGroup mCG);
            FMODUnity.RuntimeManager.CoreSystem.playSound(recSound, mCG, false, out channel);
        }

        /*
            Determine how much has been played since we last checked.
        */
        if (channel.hasHandle())
        {
            uint playPos = 0;
            channel.getPosition(out playPos, FMOD.TIMEUNIT.PCM);

            uint playDelta = (playPos >= lastPlayPos) ? (playPos - lastPlayPos) : (playPos + recSoundLength - lastPlayPos);
            lastPlayPos = playPos;
            samplesPlayed += playDelta;

            // Compensate for any drift.
            int latency = (int)(samplesRecorded - samplesPlayed);
            actualLatency = (int)((0.97f * actualLatency) + (0.03f * latency));

            int playbackRate = nativeRate;
            if (actualLatency < (int)(adjustLatency - driftThreshold))
            {
                // Playback position is catching up to the record position, slow playback down by 2%
                playbackRate = nativeRate - (nativeRate / 50);
            }

            else if (actualLatency > (int)(adjustLatency + driftThreshold))
            {
                // Playback is falling behind the record position, speed playback up by 2%
                playbackRate = nativeRate + (nativeRate / 50);
            }
            Debug.Log("playback " + playbackRate);
            channel.setFrequency((float)playbackRate);
        }
    }

    private void OnDestroy()
    {
        recSound.release();
    }
}