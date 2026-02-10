using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
public class GenericMetricWithValue<T> : GenericMetric
{
    public T Value;
    private static int length = ByteConverterCache<T>.Converter.ValueSize;
    public override int Length { get { return base.Length + length; } }
    public override int CopyToBuffer(Span<byte> byteArray) 
    {
        int offset = base.CopyToBuffer(byteArray);
        ByteConverterCache<T>.Converter.CopyToByteArray(byteArray, offset, Value);
        return offset+length;
    }

}
