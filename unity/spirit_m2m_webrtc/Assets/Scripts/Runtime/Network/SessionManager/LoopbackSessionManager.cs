using Newtonsoft.Json.Linq;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[SessionManagerRegister("Loopback")]
public class LoopbackSessionManager : SessionManagerBase
{
    protected override string NAME => "LoopbackSessionManager";
    private readonly LoopbackSessionManagerInfo config;
    //private SessionConnectionMessage message;
    public LoopbackSessionManager(string configPath) : base() {
        config = LoopbackSessionManagerInfo.CreateFromJSON(Application.dataPath + configPath);
        //OnSessionCreated += createLoopbackUsers;
        OnConnectedToSession += parseSessionJoined;
        OnNewClientConnected += startTracksForRemote;
        isReadyToConnect = true;

    }
    private void startTracksForRemote(RemoteConnectedClient client, string settings)
    {
        LoopbackProvider provider = (LoopbackProvider) ConnectionProviderRepository.GetProvider("loopback");
        if (provider == null)
        {
            Debug.LogWarning($"[THIS SHOULD NEVER HAPPEN] Provider loopback not found");
            return; // Provider not found, skip
        }
        client.SetAllTracksNetworkReceiver(provider as IReceiverSupported);
        client.StartAllTracks();
        client.GetReceivingTracks().ForEach(t => provider.SubscribeClientToTrack(client.ClientID, t.trackID));
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
        SessionID = sessionName;
        SessionConnectionMessage message = new() { 
            defaultProvider = "loopback",
            codecMode = config.selectedCodecMode,
        };
        ConnectionProviderMessage provMessage = new()
        {
            providerKey = "loopback",
            providerType = "loopback",
            providerSettings = new()
        };

        // Add all requested sending tracks to the loopback provider
        foreach(var p in joinMessage.providers)
        {
            foreach(var t in p.videoTracks)
            {
                t.providerKey = "loopback";
                t.trackID = $"cl{joinMessage.preferredClientID}_{t.trackID}";
                provMessage.videoTracks.Add(t);
            }   
        }
        message.providers.Add(provMessage);
            
        foreach (var c in config.loopbackUsers)
        {

            ConnectedClientMessage clientMessage = new()
            {
                clientID = c.clientID,
                clientSettings = "",
                codecMode = config.selectedCodecMode,
            };
            foreach (var t in c.loopbackTracks)
            {
                clientMessage.videoTracks.Add(new()
                {
                    providerKey = "loopback",
                    trackID = $"cl{c.clientID}_{t.trackID}",
                    isVideo = true,
                    //pipelineSettings =
                });
            }

            message.clients.Add(clientMessage);
        }
;
        onSessionCreated(joinMessage.preferredClientID, message.ConvertToJSON(), "");
    }
    private void createLoopbackUsers()
    {
        throw new NotImplementedException();
    }
    private void parseSessionJoined(LocalConnectedClient client, string json)
    {
        // Debug.Log(json);
        //SessionConnectionMessage message = SessionConnectionMessage.CreateFromJSON(json);
        // Add providers
        onConnectionProviderRequested(LocalClient, new ClientAddedToProviderMessage
        {
            providerKey = "loopback",
            providerType = "loopback",
            address= "none",
            port = 0,
        });
        LoopbackProvider provider = (LoopbackProvider) ConnectionProviderRepository.GetProvider("loopback");
        if (provider == null)
        {
            Debug.LogWarning($"Provider loopback not found");
            return; // Provider not found, skip
        }
        provider.LocalClientID = client.ClientID;
        LocalClient.SetAllTracksSender(provider as ISenderSupported);
        LocalClient.StartAllTracks();
        provider.AddLocalTracks(LocalClient.GetReceivingTracks().Select(t => t.trackID).ToList());
    }

    protected override void disconnectFromSessionInternal()
    {
        IsConnected = false;
    }

    protected override void connectToSessionManagerInternal()
    {
        IsConnected = true;
    }
}
