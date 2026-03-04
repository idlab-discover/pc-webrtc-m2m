
using System;
using System.Collections;
using System.Collections.Generic;
using System.Net.WebSockets;
using System.Threading;
using UnityEngine;

public class RemoteMetricSenderWebSocket : RemoteMetricSenderBase
{
    private ClientWebSocket ws = new ClientWebSocket();
    public override void Connect(string serverAddress, uint clientId)
    {
        Uri serverUri = new Uri($"ws://{serverAddress}/ws_metrics?clientId={clientId}");

        try
        {
            ws.ConnectAsync(serverUri, CancellationToken.None).GetAwaiter().GetResult();
            if(ws.State == WebSocketState.Open)
            {
                IsConnected = true;
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"Server URI {serverUri} Exception: {ex.Message}");
        }
    }

    public override void WriteMetrics(byte[] bytes)
    {
        if(ws.State != WebSocketState.Open)
        {
            Debug.LogError("WebSocket is not open. Cannot send metrics.");
            IsConnected = false;
            return;
        }
        ws.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Binary, true, CancellationToken.None).GetAwaiter().GetResult();
    }
}
