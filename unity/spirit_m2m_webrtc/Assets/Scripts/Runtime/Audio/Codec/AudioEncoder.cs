using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public abstract class AudioEncoder 
{
    public const uint AudioHeaderLength = 12;
    public byte[] Buffer { get; protected set; }
    public abstract byte[] EncodeSample(AudioFrame frame);
}
