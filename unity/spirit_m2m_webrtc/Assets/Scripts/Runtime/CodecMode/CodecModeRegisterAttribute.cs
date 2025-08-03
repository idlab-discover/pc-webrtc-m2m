using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[AttributeUsage(AttributeTargets.Class)]
public class CodecModeRegisterAttribute : Attribute
{
    public string Key { get; }
    public CodecModeRegisterAttribute(string key)
    {
        Key = key;
    }
}
