using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Adrenak.UnityOpus;
using System;
using System.Text;
using Decoder = Adrenak.UnityOpus.Decoder;
using System.Linq;

public class NopDecoder : AudioDecoder
{
    public NopDecoder() : base()
    {
    }

    public override AudioFrame DecodeSample(byte[] encodedData)
    {
        UInt64 timestamp = BitConverter.ToUInt64(encodedData, 0);
        uint audioFrameNr = BitConverter.ToUInt32(encodedData, 8);

        byte[] encodedSampleData = new byte[encodedData.Length - AudioEncoder.AudioHeaderLength];
        Array.Copy(encodedData, AudioEncoder.AudioHeaderLength, encodedSampleData, 0, encodedSampleData.Length);
        float[] sampleData = new float[(encodedData.Length - AudioEncoder.AudioHeaderLength) / sizeof(float)];
        System.Buffer.BlockCopy(encodedData, 12, sampleData, 0, (int)(encodedData.Length - AudioEncoder.AudioHeaderLength));
        return new AudioFrame(audioFrameNr, timestamp, sampleData); // Need copy so we can reuse buffer later 
    }
}
