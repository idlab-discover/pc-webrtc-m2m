using Newtonsoft.Json.Bson;
using Newtonsoft.Json.Linq;
using System;
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

public abstract class ConnectedClient<TTrackInfo> where TTrackInfo : ReceivingTrackInfo
{
    // Replace this line:
    // private abstract readonly string NAME;

    // With this property:
    protected abstract string NAME { get; }
    private readonly object _lock = new();
    //public delegate void ClientConnectedCallback();

    // TODO Add on provider change
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
    protected Dictionary<string, TTrackInfo> receivingTracks;
    public ConnectedClient(uint clientID, string codecMode)
    {
        ClientID = clientID;
        CodecMode = codecMode;
        receivingTracks = new();
    }
    // TODO maybe keep track of active tracks here
    public void AddVideoTrack(TTrackInfo track)
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
    public List<TTrackInfo> GetReceivingTracks()
    {
        lock (_lock)
        {
            Debug.Log(receivingTracks.Values.Count);
            return new List<TTrackInfo>(receivingTracks.Values);
        }
    }
    public Dictionary<string, TTrackInfo> GetReceivingTracksDict()
    {
        lock (_lock)
        {
            return new Dictionary<string, TTrackInfo>(receivingTracks);
        }
    }
}

public class LocalConnectedClient : ConnectedClient<LocalTrackInfo>
{
    protected override string NAME => "LocalConnectedClient";
    public LocalConnectedClient(uint clientID, string codecMode) : base(clientID, codecMode)
    {
    }

    // TODO Also make it so you can send via track itself
    public int SendVideoData(string trackID, IntPtr data, uint size)
    {
  
        // TODDO probably do need to lock this tbh
        if (receivingTracks.TryGetValue(trackID, out var track))
        {
            if (track.Sender == null)
            {
                Logger.LogTrackStatus(NAME, Logger.Status.ClientTrackSenderNull, ClientID, trackID);
                return -1;

            }
            if (track.Sender.IsValid == false)
            {
                Logger.LogTrackStatus(NAME, Logger.Status.ClientTrackSenderInvalid, ClientID, trackID);
                return -1;
            }
            return track.Sender.SendVideoData(trackID, data, size);
        }
        else
        {
            Logger.LogTrackStatus(NAME, Logger.Status.ClientTrackNotFound, ClientID, trackID);
            return -1;
        }
    }

    public void SendAudioData(IntPtr data, uint size)
    {
        // TODO Implement audio sending
    }
}

public class RemoteConnectedClient : ConnectedClient<RemoteTrackInfo>
{
    protected override string NAME => "RemoteConnectedClient";
    public RemoteConnectedClient(uint clientID, string codecMode) : base(clientID, codecMode)
    {
    }
}

public abstract class SessionManagerBase
{
    protected abstract string NAME { get; }

    public delegate void ConnectionToSessionManagerCallback();
    public delegate void SessionCreatedCallback();
    public delegate void ConnectedToSessionCallback(LocalConnectedClient client, string sessionInfo);
    public delegate void DisconnectedFromSessionCallback();
    public delegate void NewClientConnectedCallback(RemoteConnectedClient client, string clientSettings);
    public delegate void ClientDisconnectedCallback(RemoteConnectedClient client);

    public event ConnectionToSessionManagerCallback OnConnectedToSessionManager;
    public event SessionCreatedCallback OnSessionCreated;
    public event ConnectedToSessionCallback OnConnectedToSession;
    public event DisconnectedFromSessionCallback OnDisconnectedFromSession;
    public event NewClientConnectedCallback OnNewClientConnected;
    public event ClientDisconnectedCallback OnClientDisconnected;

    protected readonly object _lock = new object();
    public LocalConnectedClient LocalClient;
    public Dictionary<uint, RemoteConnectedClient> ConnectedClients = new();
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
        }
        else
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
        Logger.LogStatusWithMessage(NAME, Logger.Status.ManagerProviderRequested, $"provider={key}");
        ConnectionProviderBase prov = ConnectionProviderRepository.CreateProvider(type, key, jsonSettings);
        if (prov == null)
        {
            Logger.LogStatusWithMessage(NAME, Logger.Status.ProviderNotFound, $"provider={key}");
            return;
        }
         _ = prov.ConnectAsync();

    }
    protected void onConnectionProviderRemoved(string key)
    {
        Logger.LogStatus(NAME, Logger.Status.ManagerProviderRemoved);
        ConnectionProviderRepository.RemoveProvider(key);
        // TODO maybe add a disconnect here
    }
    protected void onNewClientConnected(ConnectedClientMessage c)
    {
        Logger.LogStatusClient(NAME, Logger.Status.ManagerClientConnected, c.clientID);
        RemoteConnectedClient client;
        
        lock (_lock)
        {
            if (ConnectedClients.TryGetValue(c.clientID, out client))
            {
                OnClientDisconnected?.Invoke(client); // TODO Maybe remove tracks from client?
                ConnectedClients.Remove(c.clientID);
            }

            client = new RemoteConnectedClient(c.clientID, c.codecMode);
            ConnectedClients[c.clientID] = client;
        }
        
        foreach (var t in c.receivingTracks)
        {
            // TODO Check if provider already exists
            // Request receiver from provider
            ConnectionProviderBase provider = ConnectionProviderRepository.GetProvider(t.providerKey);
            if (provider == null)
            {
                // TODO Logger.LogStatus();
                continue;
            }
            // Check if provider supports receiving
            if (provider is not IReceiverSupported receiverSupported)
            {
                Logger.LogStatusWithMessage(NAME, Logger.Status.ProviderReceiverNotSupported, $"provider={t.providerKey}");
                continue; // Provider does not support receiving
            }
            client.AddVideoTrack(new RemoteTrackInfo
            {
                clientID = c.clientID,
                providerKey = t.providerKey,
                trackID = t.trackID,
                capturerType = t.capturerType,
                trackType = t.trackType,
                trackSettings = t.trackSettings,
                Receiver = (provider as IReceiverSupported).GetReceiver(t, c.clientID)
            });
        }
        
        OnNewClientConnected?.Invoke(client, c.clientSettings);
    }
    protected void onClientDisconnected(uint clientID)
    {
        Logger.LogStatus(NAME, Logger.Status.ManagerClientDisconnected);
        lock (_lock)
        {
            if (ConnectedClients.TryGetValue(clientID, out var client))
            {
                OnClientDisconnected?.Invoke(client);
                ConnectedClients.Remove(clientID);
            }
        }
    }
    protected void clearAllClients()
    {
        lock (_lock)
        {
            foreach (var client in ConnectedClients.Values)
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
        LocalClient = new LocalConnectedClient(assignedClientID, connectionMessage.codecMode);

        foreach (var p in connectionMessage.providers)
        {
            onConnectionProviderRequested(p.type, p.key, p.providerSettings);
            ConnectionProviderBase provider = ConnectionProviderRepository.GetProvider(p.key);
            if (provider == null)
            {
                Debug.LogWarning($"Provider {p.key} not found");
                continue; // Provider not found, skip
            }
            if (p.sendingTracks.Count > 0)
            {
                // Check if provider supports sending
                if (provider is not ISenderSupported senderSupported)
                {
                    Logger.LogStatusWithMessage(NAME, Logger.Status.ProviderSenderNotSupported, $"provider={p.key}");
                    continue; // Provider does not support sending
                }
            }
            foreach (var t in p.sendingTracks)
            {
                LocalClient.AddVideoTrack(new LocalTrackInfo
                {
                    providerKey = p.key,
                    trackID = t.trackID,
                    capturerType = t.capturerType,
                    trackType = t.trackType,
                    trackSettings = t.trackSettings,
                    Sender = (provider as ISenderSupported).GetSender(t)
                });
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
