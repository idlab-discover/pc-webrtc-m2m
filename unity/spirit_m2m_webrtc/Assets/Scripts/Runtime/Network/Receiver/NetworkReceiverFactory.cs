
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

using UnityEngine;

public static class NetworkReceiverFactory
{
    private static readonly AttributedFactory<NetworkReceiverRegisterAttribute, NetworkReceiverBase> instance = new();

    public static NetworkReceiverBase CreateReceiver(string type, params object[] args)
    {
        return instance.Create(type, args);
    }
}
