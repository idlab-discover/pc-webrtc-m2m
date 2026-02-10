using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Buffers.Binary;
using System;

public class ByteConverterInt : ByteConverterBase<int>
{
    public override int ValueSize => sizeof(int);

    public override void CopyToByteArray(Span<byte> byteArray, int offset, int value)
    {
        BinaryPrimitives.WriteInt32LittleEndian(byteArray, value);
    }
}
