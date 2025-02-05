using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Adrenak.UnityOpus;
using System;
using System.Text;

public class NopEncoder : AudioEncoder
{
    public NopEncoder() : base()
    {
    }

    public override byte[] EncodeSample(AudioFrame frame)
    {
        byte[] encodedData = new byte[AudioHeaderLength + frame.AudioData.Length * sizeof(float)];
        var timestampField = BitConverter.GetBytes(frame.Timestamp);
        timestampField.CopyTo(encodedData, 0);
        var frameNrField = BitConverter.GetBytes(frame.FrameNr);
        frameNrField.CopyTo(encodedData, 8);
        System.Buffer.BlockCopy(frame.AudioData, 0, encodedData, (int)AudioHeaderLength, frame.AudioData.Length * sizeof(float));

        return encodedData;

    }
}
