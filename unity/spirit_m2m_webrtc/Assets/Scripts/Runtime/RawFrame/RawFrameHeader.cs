using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RawFrameHeader
{
    public readonly ulong Timestamp;
    public readonly uint CapturerID;
    public readonly uint FrameNr;
    public readonly uint CodecType;
    public readonly uint NPoints;
    public readonly uint Width;
    public readonly uint Height;
    public readonly uint EncodedSize;
    public RawFrameHeader(IntPtr data)
    {
        unsafe
        {
            Timestamp = *(ulong*)data;
            CapturerID = *(uint*)(data + 8);
            FrameNr = *(uint*)(data + 12);
            CodecType = *(uint*)(data + 16);
            NPoints = *(uint*)(data + 20);
            Width = *(uint*)(data + 24);
            Height = *(uint*)(data + 28);
            EncodedSize = *(uint*)(data + 32);
        }
    }
}
