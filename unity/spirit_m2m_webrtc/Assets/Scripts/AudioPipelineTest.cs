using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AudioPipelineTest : MonoBehaviour
{
    public AudioCapture Cap;
    public AudioPlayback PlaybackPrefab;
    public int NPlayback;
    List<AudioPlayback> play;
    public AudioPlaybackParams AudioParams;
    // Start is called before the first frame update
    void Start()
    {
        play = new List<AudioPlayback>();
        Cap.CB = CopyDataToPlayback;
        Cap.Init();
        for (int i = 0; i < NPlayback; i++)
        {
            AudioPlayback p = Instantiate(PlaybackPrefab);
            p.Init(Cap.CaptureSrate, AudioParams);
            play.Add(p);
        }
        
    }

    // Update is called once per frame
    void Update()
    {
        if(Cap.RecordingStarted)
        {
            Debug.Log("rec");
            foreach(AudioPlayback play in play)
            {
               play.StartPlayback();
            }
        }
    }
    void CopyDataToPlayback(UInt32 frameNr, float[] data, int lengthElements)
    {
        float[] temp = new float[data.Length];
        data.CopyTo(temp, 0);
        long timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        foreach (AudioPlayback play in play)
        {
            play.CopyToBuffer((ulong)timestamp, frameNr, temp);
        }
    }
}
