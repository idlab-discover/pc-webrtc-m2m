using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AudioSelf : MonoBehaviour
{

    
    public string microphoneName; // Leave empty for default microphone
    public int sampleRate = 44100; // Sample rate for recording
    public float chunkSizeInSeconds = 0.1f; // Chunk size in seconds
    public int playbackBufferSizeInSeconds = 1; // Buffer size for playback

    private AudioClip audioClip;
    private int chunkSizeInSamples;
    private int previousSamplePosition = 0;

    private Queue<float[]> playbackBuffer = new Queue<float[]>(); // Buffer for audio chunks
    private float[] playbackData; // Data for playback
    private AudioSource audioSource;

    private int playbackBufferSizeInSamples;

    void Start()
    {
      

        chunkSizeInSamples = Mathf.CeilToInt(chunkSizeInSeconds * sampleRate);
        playbackBufferSizeInSamples = Mathf.CeilToInt(playbackBufferSizeInSeconds * sampleRate);

        // Initialize AudioSource
        audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.clip = AudioClip.Create("RealTimePlayback", chunkSizeInSamples, 1, sampleRate, true, OnAudioRead, OnAudioSetPosition);
        audioSource.loop = true;

        StartRecording();
    }

    void StartRecording()
    {
        if (Microphone.devices.Length == 0)
        {
            Debug.LogError("No microphone detected!");
            return;
        }
        
        // Start recording in loop mode
        audioClip = Microphone.Start(microphoneName, true, 1, sampleRate); // 10-second buffer
        while((Microphone.GetPosition(null) > 0)) { }
        Debug.Log("Recording started...");

        // Start playback immediately
        audioSource.Play();
    }

    void Update()
    {
        if (audioClip == null || !Microphone.IsRecording(microphoneName)) return;

        ProcessNewAudioData();
    }

    void ProcessNewAudioData()
    {
        // Get the current microphone position
        int currentSamplePosition = Microphone.GetPosition(microphoneName);
        Debug.Log(currentSamplePosition);
        // Determine how many new samples are available
        int samplesAvailable = currentSamplePosition - previousSamplePosition;
        if (samplesAvailable < 0) // Handle looping wrap-around
        {
            samplesAvailable += audioClip.samples;
        }

        while (samplesAvailable >= chunkSizeInSamples)
        {
            // Create a buffer for the chunk
            float[] chunkData = new float[chunkSizeInSamples];

            // Get the audio data
            int chunkStart = previousSamplePosition % audioClip.samples;
            audioClip.GetData(chunkData, chunkStart);

            // Enqueue the chunk for playback
            playbackBuffer.Enqueue(chunkData);

            // Update the previous position
            previousSamplePosition = (previousSamplePosition + chunkSizeInSamples) % audioClip.samples;
            samplesAvailable -= chunkSizeInSamples;
        }
    }

    void OnAudioRead(float[] data)
    {
        int dataOffset = 0;
        //Debug.Log(data.Length);
        // Fill the playback data from the buffer
        while (playbackBuffer.Count > 0 && dataOffset < data.Length)
        {
            float[] chunk = playbackBuffer.Dequeue();
            int copyLength = Mathf.Min(chunk.Length, data.Length - dataOffset);
            System.Array.Copy(chunk, 0, data, dataOffset, copyLength);
            dataOffset += copyLength;
        }

        // Zero out remaining data to avoid noise
        if (dataOffset < data.Length)
        {
            System.Array.Clear(data, dataOffset, data.Length - dataOffset);
        }
    }

    void OnAudioSetPosition(int newPosition)
    {
        // Required by AudioClip.Create, not used in this example
    }
}
