using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public static class ByteConverterInit
{
    public static void Init()
    {
        Debug.Log("FIELD " + typeof(ByteConverterCache<int>));
        ByteConverterCache<int>.Init(new ByteConverterInt(), ByteConverterType.SignedNumeric, sizeof(int));
        ByteConverterCache<uint>.Init(new ByteConverterUInt(), ByteConverterType.UnsignedNumeric, sizeof(uint));
        ByteConverterCache<long>.Init(new ByteConverterLong(), ByteConverterType.SignedNumeric, sizeof(long));
    }

}
