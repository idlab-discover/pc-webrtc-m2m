using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public abstract class ByteConverterBase<T> 
{
    public abstract int ValueSize { get; }
    public abstract void CopyToByteArray(Span<byte> byteArray, int offset, T value);
}
