using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.WebSockets;
using System.Text;
using System.Text.RegularExpressions;
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
        Debug.Log($"WebSocketSessionManagerConfig loaded: IP={config.managerIP} PrefID={config.preferredClientID} " +
            $"StartManager={config.startNewManager} ProvIP={config.managerProvisionerIP} StartCfg={config.managerProvisionerConfigPath}");
        if (config.startNewManager && config.managerProvisionerIP != "")
        {
            _ = startSessionManagerInstance();
           
        } else
        {
            isReadyToConnect = true;
        }
    }

     private async Task startSessionManagerInstance()
    {
        using var client = new HttpClient();

        var url = $"http://{config.managerProvisionerIP}/start";

        WebSocketSessionManagerCreate startConfig = WebSocketSessionManagerCreate.CreateFromJSON(Application.dataPath + config.managerProvisionerConfigPath);
        startConfig.address = config.managerIP;
        // TODO Maybe do something with the config
        string json = JsonConvert.SerializeObject(startConfig);

        var content = new StringContent(json, Encoding.UTF8, "application/json");
        Debug.Log($"Starting new Session Manager via {url}");
        HttpResponseMessage response = await client.PostAsync(url, content);
        Debug.Log($"Reponse code: {response.StatusCode}");
        if(response.IsSuccessStatusCode)
        {
            Debug.Log("Session Manager started successfully.");
            isReadyToConnect = true;
            CheckIfReadyToConnect();
        }
        else
        {
            Debug.LogError("Failed to start Session Manager.");
        }
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
            Debug.LogError($"Server URI {serverUri} Exception: {ex.Message}");
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
                    Logger.LogStatusWithMessage(NAME, Logger.Status.WSMessageReceive, $"type={msg.messageType}");
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
                                handleClientAddedToProvider(msg.message);
                                break;
                            }
                        case "RemoteClientAdded":
                            {
                                handleRemoteClientAdded(msg.message);
                                break;
                            }
                        case "ProviderRemoteClientTracksConnected":
                            {
                                handleProviderRemoteClientTracksConnected(msg.message);
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
        ClientAddedToProviderMessage pMsg= msg.ToObject<ClientAddedToProviderMessage>();
        
        onConnectionProviderRequested(LocalClient, pMsg);
        ConnectionProviderBase provider = ConnectionProviderRepository.GetProvider(pMsg.providerKey);
        if (provider == null)
        {
            Debug.LogWarning($"Provider {pMsg.providerKey} not found");
            return; // Provider not found, skip
        }
        if (pMsg.senderVideoTracks.Count > 0)
        {
            // Check if provider supports sending
            if (provider is not ISenderSupported senderSupported)
            {
                Logger.LogStatusWithMessage(NAME, Logger.Status.ProviderSenderNotSupported, $"provider={pMsg.providerKey}");
                return; // Provider does not support sending
            }
            LocalClient.SetTracksNetworkSender(pMsg.senderVideoTracks, (provider as ISenderSupported));
            LocalClient.SetVideoTracksStatus(pMsg.senderVideoTracks, TrackStatus.Started);
        }
        // Check for remote clients
        //          => retrieve remote clients and set their receiver!!! to the provider receiver
        if(pMsg.remoteClients.Count > 0)
        {
            // Check if provider supports receiving
            if (provider is not IReceiverSupported receiverSupported)
            {
                Logger.LogStatusWithMessage(NAME, Logger.Status.ProviderReceiverNotSupported, $"provider={pMsg.providerKey}");
                return; // Provider does not support receiving
            }
        }
        foreach (var remoteClient in pMsg.remoteClients)
        {
            if(!ConnectedClients.TryGetValue(remoteClient.clientID, out var cClient))
            {
                // TODO LOG
                continue;
            }
            cClient.SetTracksNetworkReceiver(remoteClient.videoTracks, (provider as IReceiverSupported));
            cClient.SetVideoTracksStatus(remoteClient.videoTracks, TrackStatus.Started);
        }
    }

    private void handleRemoteClientAdded(JObject msg)
    {
        ConnectedClientMessage cMsg = msg.ToObject<ConnectedClientMessage>();
        onNewClientConnected(cMsg);
    }

    private void handleProviderRemoteClientTracksConnected(JObject msg)
    {
        ProviderTracksConnectedMessage pMsg = msg.ToObject<ProviderTracksConnectedMessage>();
        Logger.LogStatusWithMessage(NAME, Logger.Status.WSProviderRemoteClientTracksConnected, $"clientID={pMsg.clientID} " +
            $"providerKey={pMsg.providerKey} nVideoTracks={pMsg.videoTracks.Count} nAudioTracks={pMsg.audioTracks.Count}");
        // Communicate with provider to set tracks as connected
        // Set Tracks as connected => remote client tracks set to ready
        // Subscribe to tracks using provider

        if (!ConnectedClients.TryGetValue(pMsg.clientID, out var rClient))
        {
            Logger.LogStatusWithMessage(NAME, Logger.Status.ManagerClientNotFound, $"type=RemoteClientTracks clientID={pMsg.clientID}");
            return;
        }
        ConnectionProviderBase provider = ConnectionProviderRepository.GetProvider(pMsg.providerKey);
        if (provider == null)
        {
            Logger.LogStatusWithMessage(NAME, Logger.Status.ManagerProviderNotFound, $"type=RemoteClientTracks providerKey={pMsg.providerKey}");
            return; // Provider not found, skip
        }
        rClient.SetVideoTracksStatus(pMsg.videoTracks, TrackStatus.Started);
        rClient.SetTracksNetworkReceiver(pMsg.videoTracks, (provider as IReceiverSupported)); // TODO Merge audio and video tracks first

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
                    if(ws.State == WebSocketState.Open)
                    {
                        await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing", ct);
                    }
                        
                    return null;
                }

                ms.Write(buffer, 0, result.Count);

            } while (!result.EndOfMessage);

            string json = Encoding.UTF8.GetString(ms.ToArray());
            Debug.Log(json);
            return WebSocketSessionManagerMessage.CreateFromJSON(json);
        }
    }

    protected override void disconnectFromSessionInternal()
    {
        // Probably doesnt have to be async as it is called when Session GO is destroyed
        if (ws != null && ws.State == WebSocketState.Open)
        {
            listenerCts.Cancel();
            //  ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing", CancellationToken.None).GetAwaiter().GetResult();
            //ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing", CancellationToken.None).GetAwaiter().GetResult(); // Maybe change this to make it proper async? Might cause problems with unity flow though
        }
        IsConnected = false;
    }


}
