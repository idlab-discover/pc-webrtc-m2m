using Newtonsoft.Json.Bson;
using Newtonsoft.Json.Linq;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
/*
 * {
 *   providers: [
 *      {
 *          key:    XXX
 *          type:   XXX
 *          providerSettings:   (as json)
 *          sendingTracks:  [
 *              {
 *                  capturerID:     XXX
 *                  descriptionID:  XXX
 *              },
 *              ...
 *          ]
 *      },
 *      ...
 *   ]
 *   clients: [
 *      {
 *          clientID:   XXX,
 *          clientSettings: (as json){
 *              nDescriptions:  XXX
 *              nCapturers:     XXX
 *          }
 *          receivingTracks:    [
 *              {
 *                  providerKey:    XXX
 *                  capturerID:     XXX
 *                  descriptionID:  XXX
 *                  trackSettings:  XXX
 *              }
 *          ]
 *      },
 *      ...
 *   ]
 * }
 */

public class ConnectedClient
{
    private const string NAME = "ConnectedClient";
    private readonly object _lock = new();
    //public delegate void ClientConnectedCallback();

    public delegate void UserVideoTrackAddedCallback(string provider, string trackID);
    public delegate void UserVideoTrackRemovedCallback(string provider, string trackID);
    public delegate void UserAudioTrackAddedCallback(string provider);
    public delegate void UserAudioTrackRemovedCallback(string provider);
    //public event ClientConnectedCallback OnClientConnected;

    public event UserVideoTrackAddedCallback OnUserVideoTrackAdded;
    public event UserVideoTrackRemovedCallback OnUserVideoTrackRemoved;
    public event UserAudioTrackAddedCallback OnUserAudioTrackAdded;
    public event UserAudioTrackRemovedCallback OnUserAudioTrackRemoved;

    public readonly uint ClientID;
    public readonly string CodecMode;
    private Dictionary<string, ReceivingTrackInfo> receivingTracks; // TODO maybe change into dictionary
    public ConnectedClient(uint clientID, string codecMode)
    {
        ClientID = clientID;
        CodecMode = codecMode;
        receivingTracks = new();
    }
    // TODO maybe keep track of active tracks here
    public void AddVideoTrack(ReceivingTrackInfo track)
    {
        Logger.LogTrackStatusWithProvider(NAME, Logger.Status.ClientAddVideoTrack, ClientID, track.trackID, track.providerKey);
        lock (_lock)
        {
            // Check if track already exists
            if (receivingTracks.ContainsKey(track.trackID))
            {
                Logger.LogTrackStatusWithProvider(NAME, Logger.Status.ClientTrackAlreadyExists, ClientID, track.trackID, track.providerKey);
                return; // Track already exists
            }
            Debug.Log($"Adding track {track.trackID} with provider {track.providerKey} to client {ClientID}");
            receivingTracks[track.trackID] = track;
        }
        
        OnUserVideoTrackAdded?.Invoke(track.providerKey, track.trackID);
    }
    public void RemoveVideoTrack(string provider, string trackID)
    {
        Logger.LogTrackStatusWithProvider(NAME, Logger.Status.ClientRemoveVideoTrack, ClientID, trackID, provider);
        lock (_lock)
        {
            if (!receivingTracks.Remove(trackID))
            {
                Logger.LogTrackStatusWithProvider(NAME, Logger.Status.ClientTrackNotFound, ClientID, trackID, provider);
                return; // Track not found
            }
        }
        OnUserVideoTrackRemoved?.Invoke(provider, trackID);
    }
    public void AddAudioTrack(string provider)
    {
        Logger.LogTrackStatusWithProvider(NAME, Logger.Status.ClientAddAudioTrack, ClientID, "audio", provider);
        OnUserAudioTrackAdded?.Invoke(provider);
    }
    public void RemoveAudioTrack(string provider)
    {
        Logger.LogTrackStatusWithProvider(NAME, Logger.Status.ClientRemoveAudioTrack, ClientID, "audio", provider);
        OnUserAudioTrackRemoved?.Invoke(provider);
    }
    public List<ReceivingTrackInfo> GetReceivingTracks()
    {
        lock (_lock)
        {
            Debug.Log(receivingTracks.Values.Count);
            return new List<ReceivingTrackInfo>(receivingTracks.Values);
        }
    }
}

public abstract class SessionManagerBase
{
    protected abstract string NAME { get; }

    public delegate void ConnectionToSessionManagerCallback();
    public delegate void SessionCreatedCallback();
    public delegate void ConnectedToSessionCallback(ConnectedClient client, string sessionInfo);
    public delegate void DisconnectedFromSessionCallback();
    public delegate void NewClientConnectedCallback(ConnectedClient client, string clientSettings);
    public delegate void ClientDisconnectedCallback(ConnectedClient client);

    public event ConnectionToSessionManagerCallback OnConnectedToSessionManager;
    public event SessionCreatedCallback OnSessionCreated;
    public event ConnectedToSessionCallback OnConnectedToSession;
    public event DisconnectedFromSessionCallback OnDisconnectedFromSession;
    public event NewClientConnectedCallback OnNewClientConnected;
    public event ClientDisconnectedCallback OnClientDisconnected;

    protected readonly object _lock = new object();
    public ConnectedClient LocalClient;
    public Dictionary<uint, ConnectedClient> ConnectedClients = new();
    public bool IsConnected { get; protected set; }

    public string SessionID { get; protected set; }
    // OnConnectedCb

    public void ConnectToSessionManager()
    {
        Logger.LogStatus(NAME, Logger.Status.ManagerConnectionStart);
        connectToSessionManagerInternal();
        if (IsConnected)
        {
            onConnectedToSessionManager();
        } else
        {
            Logger.LogStatus(NAME, Logger.Status.ManagerConnectionFailed);
        }
    }
    protected abstract void connectToSessionManagerInternal();
    public async Task ConnectToSessionManagerAsync()
    {
        await Task.Run(() =>
        {
            ConnectToSessionManager();
        });
        
        
    }
    protected void onConnectionProviderRequested(string type, string key, JObject jsonSettings)
    {
        Logger.LogStatus(NAME, Logger.Status.ManagerProviderRequested);
        ConnectionProviderBase prov = ConnectionProviderRepository.CreateProvider(type, key, jsonSettings);
        if (prov == null)
        {
            return;
        }
        _ = prov.ConnectAsync();
    }
    protected void onConnectionProvidedRemoved(string key)
    {
        Logger.LogStatus(NAME, Logger.Status.ManagerProviderRemoved);
        ConnectionProviderRepository.RemoveProvider(key);
        // TODO maybe add a disconnect here
    }
    protected void onNewClientConnected(ConnectedClientMessage c)
    {
        Logger.LogStatusClient(NAME, Logger.Status.ManagerClientConnected, c.clientID);
        ConnectedClient client;
        lock (_lock)
        {
            if (ConnectedClients.TryGetValue(c.clientID, out client))
            {
                OnClientDisconnected?.Invoke(client); // TODO Maybe remove tracks from client?
                ConnectedClients.Remove(c.clientID);
            }

            client = new ConnectedClient(c.clientID, c.codecMode);
            ConnectedClients[c.clientID] = client;
        }
        foreach(var t in c.receivingTracks)
        {
            client.AddVideoTrack(t);
        }
        OnNewClientConnected?.Invoke(client, c.clientSettings);
    }
    protected void onClientDisconnected(uint clientID)
    {
        Logger.LogStatus(NAME, Logger.Status.ManagerClientDisconnected);
        lock (_lock)
        {
            if(ConnectedClients.TryGetValue(clientID, out var client))
            {
                OnClientDisconnected?.Invoke(client);
                ConnectedClients.Remove(clientID);
            }
        }
    }
    protected void clearAllClients()
    {
        lock(_lock)
        {
            foreach(var client in ConnectedClients.Values)
            {
                OnClientDisconnected?.Invoke(client);
            }
            ConnectedClients.Clear();
        } 
    }
    public abstract void CreateNewSession(string name, JoinSessionMessage joinMessage, string sessionConfig);
    public abstract void ConnectToSession(string name, JoinSessionMessage joinMessage);
    public abstract void DisconnectFromSession();
    public abstract void AddVideoTrack(ReceivingTrackInfo track);
    public abstract void AddAudioTrack();

    private void onConnectedToSessionManager()
    {
        Logger.LogStatus(NAME, Logger.Status.ManagerConnectionSuccess);
        OnConnectedToSessionManager?.Invoke();
    }
    protected void onConnectedToSession(uint assignedClientID, string connectMessageJSON, string sessionInfo)
    {
        Logger.LogStatus(NAME, Logger.Status.ManagerSessionJoined);
        SessionConnectionMessage connectionMessage = SessionConnectionMessage.CreateFromJSON(connectMessageJSON);
        LocalClient = new ConnectedClient(assignedClientID, connectionMessage.codecMode);
        
        foreach (var p in connectionMessage.providers)
        {
            onConnectionProviderRequested(p.type, p.key, p.providerSettings);
            foreach(var t in p.sendingTracks)
            {
                LocalClient.AddVideoTrack(t);
            }
        }

        OnConnectedToSession?.Invoke(LocalClient, sessionInfo);
        foreach (var c in connectionMessage.clients)
        {
            onNewClientConnected(c); // TODO probably change this to JObject
        }
    }
    protected void onSessionCreated(uint assignedClientID, string connectMessage, string sessionInfo)
    {
        Debug.Log(connectMessage);
        Logger.LogStatus(NAME, Logger.Status.ManagerSessionCreated);
        OnSessionCreated?.Invoke();
        onConnectedToSession(assignedClientID, connectMessage, sessionInfo);
    }
}
