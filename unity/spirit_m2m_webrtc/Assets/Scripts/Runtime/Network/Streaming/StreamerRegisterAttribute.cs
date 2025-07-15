using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[AttributeUsage(AttributeTargets.Class)]
public class StreamerRegisterAttribute : Attribute
{
    public string Key { get; }
    public StreamerRegisterAttribute(string key)
    {
        Key = key;
    }
}
