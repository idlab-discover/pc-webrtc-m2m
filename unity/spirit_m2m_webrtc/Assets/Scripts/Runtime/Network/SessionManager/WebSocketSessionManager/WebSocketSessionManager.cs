using Newtonsoft.Json.Linq;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

[SessionManagerRegister("WebSocket")]
public class WebSocketSessionManager : SessionManagerBase
{
    protected override string NAME => "WebSocketSessionManager";
    private readonly WebSocketSessionManagerConfig config;
    private ClientWebSocket ws = new ClientWebSocket();
    // Add field for listener thread and cancellation token
    private CancellationTokenSource listenerCts;
    private uint assignedClientID = 0;
    private string authKey = "";
    private JoinSessionMessage tempMessage;
   

    //private SessionConnectionMessage message;
    public WebSocketSessionManager(string configPath) : base()
    {
        config = WebSocketSessionManagerConfig.CreateFromJSON(Application.dataPath + configPath);
        Debug.Log("WebSocketSessionManagerConfig loaded: " + config.managerIP + " preferredClientID: " + config.preferredClientID);

    }

    public override void AddAudioTrack()
    {
        throw new NotImplementedException();
    }

    public override void AddVideoTrack(ReceivingTrackInfo track)
    {
        throw new NotImplementedException();

    }

    public override void ConnectToSession(string name, JoinSessionMessage joinMessage)
    {
        throw new System.NotImplementedException();
    }

    public override void CreateNewSession(string sessionName, JoinSessionMessage joinMessage, string sessionSettings)
    {
        Logger.LogStatusWithMessage(NAME, Logger.Status.ManagerSessionCreating, $"sessionName={sessionName}");
        tempMessage = joinMessage;
        SessionID = sessionName;
        joinMessage.codecMode = config.selectedCodecMode;
        sendJSONMessage("JoinMessage", tempMessage);
    }

    // This method is called with Connect/ConnectAsync so we dont need to wait inside
    protected override void connectToSessionManagerInternal()
    {
        // Example of adding query parameters to the connection URL
        Uri serverUri = new Uri($"ws://{config.managerIP}/websocket_client?preferredClientID={config.preferredClientID}");

        try
        {
            // Connect
            ws.ConnectAsync(serverUri, CancellationToken.None).GetAwaiter().GetResult();
            Debug.Log("Connected!");
            startListenerTask();
            lock (_lock)
            {
                while (!IsConnected && ws.State == WebSocketState.Open) // use while, not if, to avoid spurious wakeups
                {
                    Monitor.Wait(_lock);
                }
                if(ws.State != WebSocketState.Open)
                {
                    IsConnected = false;
                }
            }
            // Send a message
            // string message = "Hello WebSocket!";
            //  ArraySegment<byte> bytesToSend = new ArraySegment<byte>(Encoding.UTF8.GetBytes(message));
            //   ws.SendAsync(bytesToSend, WebSocketMessageType.Text, true, CancellationToken.None).GetAwaiter().GetResult();

            //Receive a message

            //string msg = ReceiveFullMessageAsync(ws, CancellationToken.None).GetAwaiter().GetResult();


        }
        catch (Exception ex)
        {
            Debug.LogError("Exception: " + ex.Message);
        }
    }


    // Method to start the listener thread
    private void startListenerTask()
    {
        listenerCts = new CancellationTokenSource();
        Task.Factory.StartNew(async () =>
        {
            try
            {
                while (ws.State == WebSocketState.Open && !listenerCts.Token.IsCancellationRequested)
                {
                    WebSocketSessionManagerMessage msg = await ReceiveFullMessageAsync(ws, listenerCts.Token).ConfigureAwait(false);
                    if (msg == null)
                        break;
                    // TODO: Handle received message (e.g., dispatch to Unity main thread, parse, etc.)
                   Debug.Log($"WebSocket received: {msg.messageType}");
                    switch (msg.messageType)
                    {
                        case "FullyConnected":
                            {
                                handleFullyConnected(msg.message);
                                break;
                            }
                        case "SessionJoined":
                            {
                                handleSessionJoined(msg.message);
                                break;
                            }
                        case "ClientAddedToProvider": 
                            {

                                break;
                            }
                    }
                }
            } finally
            {
                IsConnected = false;
            }
            /*catch (Exception ex)
            {
                Debug.LogError("WebSocket listener exception: " + ex.Message);
            }*/

        });

    }
    private void handleFullyConnected(JObject msg)
    {
        ClientConnectedSettings settings = msg.ToObject<ClientConnectedSettings>();
        assignedClientID = settings.clientID;
        authKey = settings.authKey;
        lock(_lock)
        {
            IsConnected = true;
            Monitor.PulseAll(_lock); // Notify the waiting thread
        }
    }

    private void handleSessionJoined(JObject msg)
    {
        SessionConnectionMessage sMsg = msg.ToObject<SessionConnectionMessage>();
        onConnectedToSession(assignedClientID, sMsg, "");
    }

    private void handleClientAddedToProvider(JObject msg)
    {
        onConnectionProviderRequested(p.providerType, p.providerKey, p.providerSettings);
        ConnectionProviderBase provider = ConnectionProviderRepository.GetProvider(p.providerKey);
        if (provider == null)
        {
            Debug.LogWarning($"Provider {p.providerKey} not found");
            return; // Provider not found, skip
        }
        if (p.videoTracks.Count > 0)
        {
            // Check if provider supports sending
            if (provider is not ISenderSupported senderSupported)
            {
                Logger.LogStatusWithMessage(NAME, Logger.Status.ProviderSenderNotSupported, $"provider={p.providerKey}");
                return; // Provider does not support sending
            }
        }
        // Check for remote clients
        //          => retrieve remote clients and set their receiver!!! to the provider receiver
    }
    private void sendJSONMessage(string messageType, object messageObj)
    {
        // Serialize the message object to JSON
       // string messageJson = Newtonsoft.Json.JsonConvert.SerializeObject(messageObj);

        // Create the WebSocketSessionManagerMessage
        var wsMessage = new WebSocketSessionManagerMessageObject
        {
            messageType = messageType,
            message = messageObj
        };

        // Serialize the wsMessage to JSON
        string wsMessageJson = Newtonsoft.Json.JsonConvert.SerializeObject(wsMessage);
        Debug.Log($"Sending {messageType} message: {wsMessageJson}");
        var bytesToSend = new ArraySegment<byte>(Encoding.UTF8.GetBytes(wsMessageJson));

        // Send the message asynchronously
        Task.Factory.StartNew(async () =>
        {
            try
            {
                await ws.SendAsync(bytesToSend, WebSocketMessageType.Text, true, CancellationToken.None).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error sending {messageType} message: {ex.Message}");
            }
        });
    }

    async Task<WebSocketSessionManagerMessage> ReceiveFullMessageAsync(ClientWebSocket ws, CancellationToken ct)
    {
        var buffer = new byte[4096];
        using (var ms = new MemoryStream())
        {
            WebSocketReceiveResult result;
            do
            {
                var segment = new ArraySegment<byte>(buffer);
                result = await ws.ReceiveAsync(segment, ct);

                if (result.MessageType == WebSocketMessageType.Close)
                {
                    await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing", ct);
                    return null;
                }

                ms.Write(buffer, 0, result.Count);

            } while (!result.EndOfMessage);

            string json = Encoding.UTF8.GetString(ms.ToArray());
            Debug.Log(json);
            return WebSocketSessionManagerMessage.CreateFromJSON(json);
        }
    }

    public override void DisconnectFromSession()
    {
        // Probably doesnt have to be async as it is called when Session GO is destroyed
        if (ws != null && ws.State == WebSocketState.Open)
        {
            listenerCts.Cancel();
            //  ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing", CancellationToken.None).GetAwaiter().GetResult();
        }
    }


}
