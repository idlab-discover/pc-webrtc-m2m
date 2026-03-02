using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public abstract class ByteConverterBase<T> 
{
    public abstract int ValueSize { get; }
    // TODO Maybe remove the offset as it probably isnt used
    public abstract int CopyToByteArray(Span<byte> byteArray, int offset, T value);
    public abstract T ConvertFromByteArray(Span<byte> byteArray, int offset);
}
