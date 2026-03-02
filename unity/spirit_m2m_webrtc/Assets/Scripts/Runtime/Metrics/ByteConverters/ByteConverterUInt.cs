using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Buffers.Binary;
using System;

public class ByteConverterUInt : ByteConverterBase<uint>
{
    public override int ValueSize => sizeof(uint);
    public override int CopyToByteArray(Span<byte> byteArray, int offset, uint value)
    {
        BinaryPrimitives.WriteUInt32LittleEndian(byteArray, value);
        return ValueSize;
    }
    public override uint ConvertFromByteArray(Span<byte> byteArray, int offset)
    {
        return BinaryPrimitives.ReadUInt32LittleEndian(byteArray);
    }
}
