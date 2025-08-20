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
    }
    private void startTracksForRemote(RemoteConnectedClient client, string settings)
    {
        client.GetReceivingTracks().ForEach(track =>
        {
            track.status = TrackStatus.Started;
        });
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
            key = "loopback",
            type = "loopback",
            providerSettings = new()
        };

        // Add all requested sending tracks to the loopback provider
        foreach(var p in joinMessage.providers)
        {
            foreach(var t in p.sendingTracks)
            {
                t.providerKey = "loopback";
                provMessage.sendingTracks.Add(t);
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
                clientMessage.receivingTracks.Add(new()
                {
                    providerKey = "loopback",
                    trackID = t.trackID,
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

    }

    public override void DisconnectFromSession()
    {
        
        throw new System.NotImplementedException();
    }

    protected override void connectToSessionManagerInternal()
    {
        IsConnected = true;
    }
}
