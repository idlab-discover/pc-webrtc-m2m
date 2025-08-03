using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public static class NetworkSenderFactory
{
    private static readonly AttributedFactory<NetworkSenderRegisterAttribute, NetworkSenderBase> instance = new();

    public static NetworkSenderBase CreateSender(string type, params object[] args)
    {
        return instance.Create(type, args);
    }
}
