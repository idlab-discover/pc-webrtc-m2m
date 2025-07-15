using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[AttributeUsage(AttributeTargets.Class)]
public class SessionManagerRegisterAttribute : Attribute
{
    public string Key { get; }
    public SessionManagerRegisterAttribute(string key)
    {
        Key = key;
    }
}
