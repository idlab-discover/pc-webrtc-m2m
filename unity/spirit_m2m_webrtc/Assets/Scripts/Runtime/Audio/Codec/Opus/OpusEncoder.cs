using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Adrenak.UnityOpus;
using System;
using System.Text;
using Encoder = Adrenak.UnityOpus.Encoder;


public class OpusEncoder : AudioEncoder
{
    private Encoder audioEncoder;
    public OpusEncoder(int samplingFreq, uint dspSize) : base()
    {
        SamplingFrequency freq = SamplingFrequency.Frequency_48000;
        if(Enum.IsDefined(typeof(SamplingFrequency), samplingFreq)) {
            freq = (SamplingFrequency)samplingFreq;
        }
        Debug.Log("[AUDIO][ENCODE] Sampling freq: " + freq.ToString() + " " + samplingFreq);
        audioEncoder = new Encoder(freq, NumChannels.Stereo, OpusApplication.RestrictedLowDelay);
        Buffer = new byte[dspSize * 2 * sizeof(float)];
    }

    public override byte[] EncodeSample(AudioFrame frame)
    {
        //encodedData = new byte[sampleData.Length * sizeof(float)];
        int encodeResult = audioEncoder.Encode(frame.AudioData, Buffer);
        byte[] encodedData = null;
        if (encodeResult >= 0)
        {
            encodedData = new byte[AudioHeaderLength + encodeResult];
            var timestampField = BitConverter.GetBytes(frame.Timestamp);
            timestampField.CopyTo(encodedData, 0);
            var frameNrField = BitConverter.GetBytes(frame.FrameNr);
            frameNrField.CopyTo(encodedData, 8);
            Array.Copy(Buffer, 0, encodedData, AudioHeaderLength, encodeResult);
         //   Debug.Log("[AUDIO][ENCODE] Encoded with size: " + encodeResult);
        }
        else
        {
            ErrorCode errorCode = (ErrorCode)encodeResult;
            Debug.Log("[AUDIO][ENCODE] Error during encoding: " + errorCode.ToString() + " SLength: ");
        }
        return encodedData;

    }
}
