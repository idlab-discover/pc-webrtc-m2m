using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class AudioPipelineTest : MonoBehaviour
{
    public AudioCapture Cap;
    public AudioPlayback PlaybackPrefab;
    public int NPlayback;
    List<AudioPlayback> play;
    public AudioPlaybackParams AudioParams;
    private AudioEncoder enc;
    private AudioDecoder dec;
    // Start is called before the first frame update
    void Start()
    {
        play = new List<AudioPlayback>();
        Cap.CB = CopyDataToPlayback;
        Cap.Init(AudioParams.codecName, AudioParams.dspSize);

        enc = AudioCodecFactory.CreateEncoder(AudioParams.codecName, Cap.CaptureSrate, AudioParams.dspSize);
        dec = AudioCodecFactory.CreateDecoder(AudioParams.codecName, Cap.CaptureSrate, AudioParams.dspSize);
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
    void CopyDataToPlayback(byte[] encodedData)
    {
        foreach (AudioPlayback play in play)
        {
            play.DecodeAndCopyToBuffer(encodedData);
        }
    }
}
