using Newtonsoft.Json.Bson;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ConnectedClient
{
    //public delegate void ClientConnectedCallback();
    
    public delegate void UserVideoTrackAddedCallback(string streamer, uint capturerID, uint descriptionID);
    public delegate void UserVideoTrackRemovedCallback(string streamer, uint capturerID, uint descriptionID);
    public delegate void UserAudioTrackAddedCallback(string streamer);
    public delegate void UserAudioTrackRemovedCallback(string streamer);
    //public event ClientConnectedCallback OnClientConnected;

    public event UserVideoTrackAddedCallback OnUserVideoTrackAdded;
    public event UserVideoTrackRemovedCallback OnUserVideoTrackRemoved;
    public event UserAudioTrackAddedCallback OnUserAudioTrackAdded;
    public event UserAudioTrackRemovedCallback OnUserAudioTrackRemoved;

    public readonly uint ClientID;
    public ConnectedClient(uint clientID)
    {
        ClientID = clientID;
    }
    // TODO maybe keep track of active tracks here
    public void AddVideoTrack(string streamer, uint capturerID, uint descriptionID)
    {
        OnUserVideoTrackAdded?.Invoke(streamer, capturerID, descriptionID);
    }
    public void RemoveVideoTrack(string streamer, uint capturerID, uint descriptionID)
    {
        OnUserVideoTrackRemoved?.Invoke(streamer, capturerID, descriptionID);
    }
    public void AddAudioTrack(string streamer)
    {
        OnUserAudioTrackAdded?.Invoke(streamer);
    }
    public void RemoveAudioTrack(string streamer)
    {
        OnUserAudioTrackRemoved?.Invoke(streamer);
    }
}

public abstract class SessionManagerBase
{
    public delegate void ConnectedToSessionCallback();
    public delegate void DisconnectedFromSessionCallback();
    public delegate void NewClientConnectedCallback(uint clientID);
    public delegate void ClientDisconnectedCallback(uint clientID);

    public event ConnectedToSessionCallback OnConnectedToSession;
    public event DisconnectedFromSessionCallback OnDisconnectedFromSession;
    public event NewClientConnectedCallback OnNewClientConnectedCallback;
    public event ClientDisconnectedCallback OnClientDisconnected;

    public readonly object _lock = new object();
    public Dictionary<uint, ConnectedClient> ConnectedClients;
    public bool IsConnected { get; private set; }
    public int ClientID { get; private set; }
    public string SessionID { get; private set; }
    // OnConnectedCb

    protected abstract void connectToSessionManager();
    protected void onNewClientConnected(uint clientID)
    {
        lock(_lock)
        {
            if(ConnectedClients.ContainsKey(clientID))
            {
                OnClientDisconnected?.Invoke(clientID);
                ConnectedClients.Remove(clientID);
            }
            ConnectedClients[clientID] = new ConnectedClient(clientID);
        }
        OnNewClientConnectedCallback?.Invoke(clientID);
    }
    protected void onClientDisconnected(uint clientID)
    {
        lock(_lock)
        {
            OnClientDisconnected?.Invoke(clientID);
            ConnectedClients.Remove(clientID);
        }
    }
    public abstract void CreateNewSession();
    public abstract void ConnectToSession(string name);
    public abstract void DisconnectFromSession();
    public abstract void AddVideoTrack(uint capturerID, uint descriptionID);
    public abstract void AddAudioTrack();
}
