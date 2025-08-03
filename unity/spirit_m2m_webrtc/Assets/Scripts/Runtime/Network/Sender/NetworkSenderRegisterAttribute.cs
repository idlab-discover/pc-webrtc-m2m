using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class NetworkSenderRegisterAttribute : RegisterAttribute
{
    public NetworkSenderRegisterAttribute(string key) : base(key)
    {
    }
}
