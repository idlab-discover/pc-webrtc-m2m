using FMOD;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
//using UnityEditor.ShaderGraph.Drawing.Inspector.PropertyDrawers;
using UnityEngine;
using UnityEngine.UIElements;
using Debug = UnityEngine.Debug;

public class AudioPlaybackBuffer
{
    private Queue<AudioSample> samples;
    private uint maxQueueSize;
    private float averageJitter;
    private float currentDelay;
    private float minPlaybackFrequency;
    private float maxPlaybackFrequency;
    private uint targetSamples;
    private float currentPlaybackSpeed;
    private float prevPlaybackSpeed;
    private int prevSamples;
    private uint soundLength;
    private uint forcedAudioDelay;
    private uint defaultPlaybackFrequency;
    private float playbackSpeedMultiplier;
    private bool ignoreTargetDelay;
    private bool ignoreJitter;
    private uint currentWritePos;
    private uint latestReceivedFrameNr;
    private uint sampleSizeInBytes;
    public bool PlaybackStarted { get; private set; }
    public UInt64 LatestPcTimestamp;
    public FMOD.Sound Sound;
    public FMOD.Channel Channel;
    public AudioPlaybackBuffer(uint soundLength, AudioPlaybackParams pms)
    {
        samples = new Queue<AudioSample>();
        currentPlaybackSpeed = 1.0f;
        this.maxQueueSize = pms.maxQueueSize;
        this.targetSamples = pms.targetSamples;
        this.soundLength = soundLength;
        this.defaultPlaybackFrequency = pms.defaultPlaybackFrequency;
        this.minPlaybackFrequency = pms.minPlaybackFrequency;
        this.playbackSpeedMultiplier = pms.latencyPlaybackMultiplier;
        this.ignoreJitter = pms.ignoreJitter;
        this.ignoreTargetDelay = pms.ignoreTargetDelay;
        this.sampleSizeInBytes = pms.sampleSizeInBytes;
    }
    public void StartPlayback()
    {

        bool foundGoodSample = false;
        while(!foundGoodSample)
        {
            if (samples.Count == 0)
            {
                foundGoodSample = true;
            }
            UInt64 audioTimestamp = samples.Peek().Timestamp;
            if(audioTimestamp > LatestPcTimestamp)
            {
                foundGoodSample = true;
            }
            if(LatestPcTimestamp - audioTimestamp > forcedAudioDelay + 20)
            {
                samples.Dequeue();
            } else
            {
                foundGoodSample = true;
                // Set position of channel to 0 PCM
                Channel.setPosition(0, TIMEUNIT.PCM);
                // Copy all existing samples to sound buffer => stop when sound is full
                FlushQueueIntoBuffer();
            }
        }
        PlaybackStarted = true;
    }
    public void ForceStartPlayback()
    {
        Debug.Log("start playback force");
        Channel.setPosition(0, TIMEUNIT.PCM);
        // Copy all existing samples to sound buffer => stop when sound is full
        FlushQueueIntoBuffer();
        PlaybackStarted = true;
    }
    public void AddItem(UInt64 timestamp, UInt32 frameNr, float[] sample)
    {
        // Got old audio packet so just drop it
        if(frameNr < latestReceivedFrameNr)
        {
            return;
        }
        
        // Check if queue not empty =>
        //      Write as much as possible to queue
        samples.Enqueue(new AudioSample(frameNr, timestamp, sample));
        Debug.Log("QUEUE SIZE " + samples.Count);
        if (samples.Count > maxQueueSize)
        {
            samples.Dequeue();
        }

        // Do not add to actual sound buffer if playback has not yet started 
        if(!PlaybackStarted)
        {
            Debug.Log("Waiting for audio playback to start");
            return;
        }
        // TODO check if read has somehow caught up to write cursor
        FlushQueueIntoBuffer();

        // Calculate new playback rate based on:
        //                          - queue size => target delay
        //                          - jitter target
        //                          - time read / write position =>


    }
    private void FlushQueueIntoBuffer() {
        uint currentPos = GetChannelPositionInBytes();
        uint emptySpace = GetEmptySpaceInSound(currentPos);
        uint tEmptySpace = emptySpace;
        uint flushCounter = 0;
        while (emptySpace > sampleSizeInBytes && samples.Count > 0)
        {
            AudioSample sm = samples.Dequeue();
            // Correct write position for any frame drops
            correctWritePosForPacketLoss(sm.FrameNr);
            CopySampleToSound(sm.AudioData, sampleSizeInBytes);
            latestReceivedFrameNr = sm.FrameNr;
            emptySpace = GetEmptySpaceInSound(currentPos);
            flushCounter++;
           // Debug.Log("Writing: " + currentWritePos);
        }
        Debug.Log("Flushed audio samples: " + flushCounter + " Q size " + samples.Count + " SPACE " + tEmptySpace +  " RPOS " + currentPos + " WPOS " + currentWritePos);
    }
    public float[] DequeueSample()
    {
        if (samples.Count == 0) { return null; }
        return samples.Dequeue().AudioData;
    }
    public float CalculateCurrentFrequency()
    {
        float modifier = CalculatePlaybackSpeed();
        float freq = defaultPlaybackFrequency * modifier;
        return Math.Min(Math.Max(freq, minPlaybackFrequency), maxPlaybackFrequency);
        
    }
    private float CalculatePlaybackSpeed()
    {
        bool changedSpeed = false;
        if(samples.Count == 0)
        {
            currentPlaybackSpeed = 1.0f;
            return currentPlaybackSpeed;
        }
        // Jitter / size of queue
        if(!changedSpeed && !ignoreJitter)
        {
            if(samples.Count < targetSamples) {
                currentPlaybackSpeed -= playbackSpeedMultiplier;
                changedSpeed = true;
            } else if(samples.Count > targetSamples)
            {
                currentPlaybackSpeed += playbackSpeedMultiplier;
                changedSpeed = true;
            }
        }
        // Target Audio Delay
        if (!changedSpeed && !ignoreTargetDelay)
        {
            UInt64 earliestAudioTimestamp = samples.Peek().Timestamp;
            UInt64 delay = 0;
            if (LatestPcTimestamp >= earliestAudioTimestamp)
            {
                // Audio behind
                delay = LatestPcTimestamp - earliestAudioTimestamp;
            }
            else
            {   // Audio ahead
                delay = earliestAudioTimestamp - LatestPcTimestamp;

            }
            if (delay > forcedAudioDelay + 20)
            {
                // Increase playback speed
                currentPlaybackSpeed += playbackSpeedMultiplier;
                changedSpeed = true;
            }
            else if (delay < forcedAudioDelay - 20)
            {
                // Decrease playback speed
                currentPlaybackSpeed -= playbackSpeedMultiplier;
                changedSpeed = true;
            }
            else
            {
                currentPlaybackSpeed = 1.0f;
                changedSpeed = true;
            }
        }
       

        /*if(samples.Count == targetSamples)
        {
            currentPlaybackSpeed = 1.0f;
        }
        else {
            // Too little samples
            if (samples.Count < targetSamples)
            {
                currentPlaybackSpeed *= 0.9f;
                currentPlaybackSpeed = Math.Max(0.0f, currentPlaybackSpeed);
            }
            // Too many samples
            if (samples.Count > targetSamples)
            {
                currentPlaybackSpeed *= 1.1f;
                currentPlaybackSpeed = Math.Min(100.0f, currentPlaybackSpeed);
            }

            prevSamples = samples.Count;
        }
        if(prevPlaybackSpeed == currentPlaybackSpeed && currentPlaybackSpeed == 1.0f )
        {
            return 0.0f;
        }
        prevPlaybackSpeed = currentPlaybackSpeed;*/
        return currentPlaybackSpeed;
    }
    private uint GetChannelPositionInSamples()
    {
        uint playPos = 0;
        Channel.getPosition(out playPos, FMOD.TIMEUNIT.PCM);
        return playPos;
    }
    private uint GetChannelPositionInBytes()
    {
        uint playPos = 0;
        Channel.getPosition(out playPos, FMOD.TIMEUNIT.PCMBYTES);
        return playPos;
    }
    private uint GetEmptySpaceInSound(uint currentReadPos)
    {
        uint emptySpace = 0;
        if(currentWritePos >= currentReadPos)
        {
            emptySpace = soundLength - currentWritePos + currentReadPos;
        } else
        {
            emptySpace = currentReadPos - currentWritePos;   
        }
        
        return emptySpace;
    }
    private void CopySampleToSound(float[] sampleData, uint sampleSize)
    {
        int lengthToRead = (int)(sampleSize * sizeof(float) / 4);
        IntPtr ptr1, ptr2;
        uint lenBytes1, lenBytes2;
        var res = Sound.@lock((uint)currentWritePos, (uint)lengthToRead, out ptr1, out ptr2, out lenBytes1, out lenBytes2);
        if (lenBytes1 > 0)
        {
            Marshal.Copy(sampleData, 0, ptr1, (int)lenBytes1 / sizeof(float));
        }
        if (lenBytes2 > 0)
        {
            Marshal.Copy(sampleData, (int)lenBytes1 / sizeof(float), ptr2, (int)lenBytes2 / sizeof(float));
        }
        currentWritePos = (currentWritePos + sampleSizeInBytes) % soundLength;
    }
    private void correctWritePosForPacketLoss(uint newestFrameNr)
    {
        uint frameNrOffset = newestFrameNr - latestReceivedFrameNr;
        // No audio frames were dropped
        if (frameNrOffset == 1)
        {
            return;
        }
        currentWritePos = (currentWritePos + (sampleSizeInBytes*(frameNrOffset))) % soundLength;
    }
}
