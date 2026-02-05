using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
[AttributeUsage(AttributeTargets.Class)]
public abstract class RegisterAttribute : Attribute
{
    public string Key { get; }
    public RegisterAttribute(string key)
    {
        Key = key;
    }
}