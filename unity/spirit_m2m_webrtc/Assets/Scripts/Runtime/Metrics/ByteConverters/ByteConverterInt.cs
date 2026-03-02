using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Buffers.Binary;
using System;

public class ByteConverterInt : ByteConverterBase<int>
{
    public override int ValueSize => sizeof(int);

    public override int CopyToByteArray(Span<byte> byteArray, int offset, int value)
    {
        BinaryPrimitives.WriteInt32LittleEndian(byteArray, value);
        return ValueSize;
    }
    public override int ConvertFromByteArray(Span<byte> byteArray, int offset)
    {
        return BinaryPrimitives.ReadInt32LittleEndian(byteArray);
    }
}
