using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public static class AudioCodecFactory 
{
    private readonly static Dictionary<string, Func<int, uint, AudioEncoder>> encoders = new();
    private readonly static Dictionary<string, Func<int, uint, AudioDecoder>> decoders = new();

    static AudioCodecFactory()
    {
        RegisterEncoder("opus", (int samplingFreq, uint dspSize) => new OpusEncoder(samplingFreq, dspSize));
        RegisterDecoder("opus", (int samplingFreq, uint dspSize) => new OpusDecoder(samplingFreq, dspSize));
    }

    public static void RegisterEncoder<T>(string key, Func<int, uint, T>  contructor) where T : AudioEncoder
    {
        encoders[key] = contructor;
    }

    public static void RegisterDecoder<T>(string key, Func<int, uint, T> contructor) where T : AudioDecoder
    {
        decoders[key] = contructor;
    }

    public static AudioEncoder CreateEncoder(string encName, int freq, uint dspSize)
    {
        if(encoders.TryGetValue(encName, out var encoder))
        {
            return encoder(freq, dspSize);
        }
        return new NopEncoder();
    }
    public static AudioDecoder CreateDecoder(string decName, int freq, uint dspSize)
    {
        if (decoders.TryGetValue(decName, out var decoder))
        {
            return decoder(freq, dspSize);
        }
        return new NopDecoder();
    }
}
