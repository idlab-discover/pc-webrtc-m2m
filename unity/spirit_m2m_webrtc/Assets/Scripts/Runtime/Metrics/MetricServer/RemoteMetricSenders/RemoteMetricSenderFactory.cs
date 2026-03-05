using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public static class RemoteMetricSenderFactory 
{
    public static RemoteMetricSenderBase CreateRemoteMetricSender(string connectionType)
    {
        switch (connectionType)
        {
            case "websocket":
                return new RemoteMetricSenderWebSocket();
            default:
                throw new System.ArgumentException($"Unsupported connection type {connectionType} for RemoteMetricSenderFactory.");
        }
    }
}
