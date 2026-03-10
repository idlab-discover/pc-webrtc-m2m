using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Buffers.Binary;
using System;

public class ByteConverterLong : ByteConverterBase<long>
{
    public override int ValueSize => sizeof(long);
    public override int CopyToByteArray(Span<byte> byteArray, int offset, long value)
    {
        BinaryPrimitives.WriteInt64LittleEndian(byteArray, value);
        return ValueSize;
    }
    public override long ConvertFromByteArray(Span<byte> byteArray, int offset)
    {
        return BinaryPrimitives.ReadInt64LittleEndian(byteArray);
    }
}
