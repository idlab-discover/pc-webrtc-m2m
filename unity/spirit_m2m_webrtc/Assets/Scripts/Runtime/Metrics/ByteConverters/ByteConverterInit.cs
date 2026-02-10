using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public static class ByteConverterInit
{
    public static void Init()
    {
        ByteConverterCache<int>.Converter = new ByteConverterInt();
        ByteConverterCache<uint>.Converter = new ByteConverterUInt();
    }

}
