using System;
using System.Buffers.Binary;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
public class GenericMetricWithValue<T> : GenericMetric
{
    private T Value;
    private static int length = ByteConverterCache<T>.Converter.ValueSize;
    public override int Length { get { return base.Length + length; } }
    public GenericMetricWithValue(T value) : base()
    {
        Value = value;
    }
    public override int CopyToBuffer(Span<byte> byteArray) 
    {
        int offset = base.CopyToBuffer(byteArray);
        ByteConverterCache<T>.Converter.CopyToByteArray(byteArray.Slice(offset), offset, Value);
        return offset+length;
    }

}
