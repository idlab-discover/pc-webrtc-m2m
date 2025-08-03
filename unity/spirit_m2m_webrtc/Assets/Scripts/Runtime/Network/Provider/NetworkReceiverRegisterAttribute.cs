using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;


public class NetworkReceiverRegisterAttribute : RegisterAttribute
{
    public NetworkReceiverRegisterAttribute(string key) : base(key)
    {
    }
}