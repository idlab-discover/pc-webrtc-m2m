using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[SessionManagerRegister("Loopback")]
public class LoopbackSessionManager : SessionManagerBase
{
    private readonly LoopbackSessionManagerInfo config;
    public LoopbackSessionManager(string configPath) : base() {
        config = LoopbackSessionManagerInfo.CreateFromJSON(Application.dataPath + configPath);
    }
    public override void AddAudioTrack()
    {
        lock(_lock)
        {
            foreach (var (_, c) in ConnectedClients)
            {
                c.AddAudioTrack("loopback");
            }
        }
    }

    public override void AddVideoTrack(uint capturerID, uint descriptionID)
    {
        lock(_lock)
        {
            foreach (var (_, c) in ConnectedClients)
            {
                c.AddVideoTrack("loopback", capturerID, descriptionID);
            }
        }
        
    }

    public override void ConnectToSession(string name)
    {
        throw new System.NotImplementedException();
    }

    public override void CreateNewSession()
    {
        foreach(var c in config.loopbackUsers)
        {
            onNewClientConnected(c.clientID);
        }
    }

    public override void DisconnectFromSession()
    {
        throw new System.NotImplementedException();
    }

    protected override void connectToSessionManager()
    {
        
    }
}
