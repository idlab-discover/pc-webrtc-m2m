using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[AttributeUsage(AttributeTargets.Class)]
public class ConnectionProviderRegisterAttribute : Attribute
{
    public string Key { get; }
    public ConnectionProviderRegisterAttribute(string key)
    {
        Key = key;
    }
}
