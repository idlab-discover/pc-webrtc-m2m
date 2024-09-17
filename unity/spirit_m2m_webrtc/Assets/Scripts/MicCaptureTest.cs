using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;


public class MicCaptureTest : MonoBehaviour
{
    public AudioSource audioSource;

    private string micDevice;
    private AudioClip micAudioClip;
    private int previousPos;
    private int nSamples;
    // Start is called before the first frame update
    void Start()
    {
        micDevice = Microphone.devices.First();
        micAudioClip = Microphone.Start(micDevice, true, 1, 44100);
        nSamples = micAudioClip.samples * micAudioClip.channels;
        Debug.Log($"Channels: {micAudioClip.channels}");
    }

    // Update is called once per frame
    void Update()
    {
        int currentMicPos = Microphone.GetPosition(micDevice);
        int nSamplesToRead;
        int nextPos = currentMicPos;
        Debug.Log($"{currentMicPos} {previousPos}");
        if (currentMicPos >= previousPos)
        {
            nSamplesToRead = currentMicPos - previousPos;
        }
        else {
            nSamplesToRead = nSamples - previousPos;
            nextPos = 0;
        }
        if(nSamplesToRead == 0) { return; }
        float[] sampleData = new float[nSamplesToRead];
        micAudioClip.GetData(sampleData, previousPos);
        previousPos = nextPos;
        Debug.Log($"{currentMicPos} {nextPos} {nSamplesToRead}");
       // micAudioClip.
    }

    private void OnDestroy()
    {
        Microphone.End(micDevice);
    }
}
