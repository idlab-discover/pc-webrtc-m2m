using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public abstract class AudioDecoder
{
    public float[] Buffer { get; protected set; }
    public abstract AudioFrame DecodeSample(byte[] encodedData);
}
