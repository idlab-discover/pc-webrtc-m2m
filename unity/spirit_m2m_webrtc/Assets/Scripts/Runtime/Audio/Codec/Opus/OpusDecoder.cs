using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Adrenak.UnityOpus;
using System;
using System.Text;
using Decoder = Adrenak.UnityOpus.Decoder;
using System.Linq;


public class OpusDecoder : AudioDecoder
{
    private Decoder audioDecoder;
    public OpusDecoder(int samplingFreq, uint dspSize) : base()
    {
        SamplingFrequency freq = SamplingFrequency.Frequency_48000;
        if (Enum.IsDefined(typeof(SamplingFrequency), samplingFreq))
        {
            freq = (SamplingFrequency)samplingFreq;
        }
        audioDecoder = new Decoder(freq, NumChannels.Stereo);
        Buffer = new float[dspSize*2];
    }

    public override AudioFrame DecodeSample(byte[] encodedData)
    {
       
        UInt64 timestamp = BitConverter.ToUInt64(encodedData, 0);
        uint audioFrameNr = BitConverter.ToUInt32(encodedData, 8);

        byte[] encodedSampleData = new byte[encodedData.Length - AudioEncoder.AudioHeaderLength];
        Array.Copy(encodedData, AudioEncoder.AudioHeaderLength, encodedSampleData, 0, encodedSampleData.Length);
        int decodeResult = audioDecoder.Decode(encodedSampleData, encodedSampleData.Length, Buffer);
        if (decodeResult > 0)
        {
           // Debug.Log("[AUDIO][DECODE] Decoded length: " + decodeResult);
            return new AudioFrame(audioFrameNr, timestamp, Buffer.ToArray()); // Need copy so we can reuse buffer later 
        }
        else
        {
            Debug.Log("[AUDIO][DECODE] Error during decoding:");
            return new AudioFrame(audioFrameNr, timestamp, null);
        }

    }
}
