using System;
using System.Buffers.Binary;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum ByteConverterType : byte
{
    SignedNumeric = 1,
    UnsignedNumeric = 2,
    SignedNumericFloat = 3,
    UnsignedNumericFloat = 4,
    String = 5,
    Custom = 6
}

public static class ByteConverterCache<T>
{
    public static ByteConverterBase<T> Converter { get; private set; }
    public static ByteConverterType ValueType { get; private set; }
    public static uint FixedLength { get; private set; }

    public static void Init(ByteConverterBase<T> converter, ByteConverterType type, uint fixedLength = 0)
    {
        Converter = converter;
        ValueType = type;
        FixedLength = fixedLength;
    }
    public static byte[] GetHeader()
    {
        uint size = sizeof(ByteConverterType);
        if(FixedLength > 0)
        {
            size += sizeof(uint);
        }
        byte[] header = new byte[size];
        header[0] = (byte)ValueType;
        if(FixedLength > 0)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(header.AsSpan(1), FixedLength);
        }
        return header;
    }
}
