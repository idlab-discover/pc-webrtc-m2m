using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Buffers.Binary;

public class GenericMetric
{
    public long Timestamp;
    private static int length = sizeof(long);
    public virtual int Length { get { return length; } }
    public GenericMetric()
    {
        Timestamp = System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
    }
    public virtual int CopyToBuffer(Span<byte> byteArray) 
    {
        BinaryPrimitives.WriteInt64LittleEndian(byteArray, Timestamp);
        return length;    
    }
}
