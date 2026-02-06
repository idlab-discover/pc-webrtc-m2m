using Newtonsoft.Json;
using Newtonsoft.Json.Bson;
using Newtonsoft.Json.Linq;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Unity.VisualScripting;
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
    protected readonly object _lock = new();
    //public delegate void ClientConnectedCallback();

    // TODO Add on provider change
    public delegate void UserVideoTrackAddedCallback(string provider, string trackID);
    public delegate void UserVideoTrackRemovedCallback(string provider, string trackID);
    public delegate void UserAudioTrackAddedCallback(string provider);
    public delegate void UserAudioTrackRemovedCallback(string provider);
    public delegate void UserPositionUpdatedCallback(ClientPositionUpdate newPostion);
    //public event ClientConnectedCallback OnClientConnected;

    public event UserVideoTrackAddedCallback OnUserVideoTrackAdded;
    public event UserVideoTrackRemovedCallback OnUserVideoTrackRemoved;
    public event UserAudioTrackAddedCallback OnUserAudioTrackAdded;
    public event UserAudioTrackRemovedCallback OnUserAudioTrackRemoved;
    public event UserPositionUpdatedCallback OnUserPositionUpdated;

    public readonly uint ClientID;
    public readonly string CodecMode;
    protected Dictionary<string, TTrackInfo> receivingTracks;
    public ConnectedClient(uint clientID, string codecMode)
    {
        ClientID = clientID;
        CodecMode = codecMode;
        receivingTracks = new();
    }

    public void StartAllTracks()
    {
        lock (_lock)
        {
            foreach (var track in receivingTracks.Values)
            {
                track.status = TrackStatus.Started;
            }
        }
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
    public void SetVideoTracksStatus(List<TrackSimple> tracks, TrackStatus status)
    {
        Logger.LogStatusWithMessage(NAME, Logger.Status.ClientSettingTrackStatus, $"clientID={ClientID} nTracks={tracks.Count} status={status}");
        lock (_lock)
        {
            foreach (var t in tracks)
            {
                if (!receivingTracks.TryGetValue(t.trackID, out var track))
                {
                    continue; // Track not found
                }
                track.status = status;
                // TODO Maybe add an onTrack Status changed
               
            }
        }
        Logger.LogStatusWithMessage(NAME, Logger.Status.ClientTrackStatusSet, $"clientID={ClientID} nTracks={tracks.Count} status={status}");
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

    // TODO: We probably want to extend this to include other servers
    // i.e., metric server or maybe a GameObject sync server
    // These components could then decide to directly forward it or only forward it every X ms
    public void UpdatePositionMatrix(ClientPositionUpdate newPosition)
    {
        OnUserPositionUpdated?.Invoke(newPosition);
    }
}

public class LocalConnectedClient : ConnectedClient<LocalTrackInfo>
{
    protected override string NAME => "LocalConnectedClient";
    public LocalConnectedClient(uint clientID, string codecMode) : base(clientID, codecMode)
    {
    }
    public void SetAllTracksSender(ISenderSupported sender)
    {
        lock (_lock)
        {
            foreach (var track in receivingTracks.Values)
            {
                track.SetSender(sender.GetSender(track));
            }
        }
    }
    public void SetTracksNetworkSender(List<TrackSimple> tracks, ISenderSupported sender)
    {
        lock (_lock) {
            foreach (var t in tracks) {
                if (!receivingTracks.TryGetValue(t.trackID, out var track))
                {
                    continue; // Track not found
                }
                track.SetSender(sender.GetSender(track));
            }
        }
    }

    // TODO Also make it so you can send via track itself
    public int SendVideoData(string trackID, uint frameNr, IntPtr data, uint size)
    {
  
        // TODDO probably do need to lock this tbh
        if (receivingTracks.TryGetValue(trackID, out var track))
        {
            if(track.status == TrackStatus.NotStarted)
            {
                Logger.LogTrackStatus(NAME, Logger.Status.ClientTrackNotStarted, ClientID, trackID);
                return 0;
            }
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
            return track.Sender.SendVideoData(trackID, frameNr, data, size);
        }
        else
        {
            Logger.LogTrackStatus(NAME, Logger.Status.ClientTrackNotFound, ClientID, trackID);
            return -1;
        }
    }

    public void SendAudioData(uint frameNr, IntPtr data, uint size)
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

    public void SetAllTracksNetworkReceiver(IReceiverSupported receiver)
    {
        lock (_lock)
        {
            foreach (var track in receivingTracks.Values)
            {
                track.SetReceiver(receiver.GetReceiver(track, ClientID));
            }
        }
    }
    public void SetTracksNetworkReceiver(List<TrackSimple> tracks, IReceiverSupported receiver)
    {
        Logger.LogStatusWithMessage(NAME, Logger.Status.ClientSettingTrackReceiver, $"clientID={ClientID} nTracks={tracks.Count}");
        lock (_lock)
        {

            List<RemoteTrackInfo> trackInfos = new();
            foreach (var t in tracks)
            {
                if (!receivingTracks.TryGetValue(t.trackID, out var track))
                {
                    Logger.LogStatusWithMessage(NAME, Logger.Status.ClientTrackNotFound, $"func=SetTracksNetworkReceiver trackID={t.trackID}");
                    continue; // Track not found
                }
                trackInfos.Add(track);
            }
            NetworkReceiverBase recv = receiver.GetReceiverForTrackList(trackInfos, ClientID);
            foreach (var t in trackInfos)
            {
                t.SetReceiver(recv);
            }
        }
        Logger.LogStatusWithMessage(NAME, Logger.Status.ClientTrackReceiverSet, $"clientID={ClientID} nTracks={tracks.Count}");
    }
}

// TODO Add unsubscribe method
public class GenericMessageList
{
    public List<Action<object>> subscribers = new();
    private readonly Func<string, object> deserializer;
    public GenericMessageList(Func<string, object> deserializer)
    {
        this.deserializer = deserializer;
    }
    public void Subscribe(Action<object> callback)
    {
        subscribers.Add(callback);
    }
    public void NotiyAll(JObject message)
    {
        // Convert to Object first
       // object obj = deserializer(message);
        foreach (var sub in subscribers)
        {
            sub.Invoke(message);
        }
    }
}

public abstract class SessionManagerBase
{
    protected abstract string NAME { get; }

    private readonly Dictionary<string, Dictionary<Type, GenericMessageList>> genericMessagesubscribers = new();

    public delegate void ReadyToConnectCallback();
    public delegate void ConnectionToSessionManagerCallback();
    public delegate void SessionCreatedCallback();
    public delegate void ConnectedToSessionCallback(LocalConnectedClient client, string sessionInfo);
    public delegate void DisconnectedFromSessionCallback();
    public delegate void NewClientConnectedCallback(RemoteConnectedClient client, string clientSettings);
    public delegate void ClientDisconnectedCallback(RemoteConnectedClient client);

    public event ReadyToConnectCallback OnReadyToConnect;
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
    protected bool isReadyToConnect = false;
    private bool readyToConnectCalled = false;
    public string SessionID { get; protected set; }
    // OnConnectedCb
    private List<ConnectionProviderBase> createdProviders = new();

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
    protected void onConnectionProviderRequested(LocalConnectedClient localClient, ClientAddedToProviderMessage pMsg)
    {
        Logger.LogStatusWithMessage(NAME, Logger.Status.ManagerProviderRequested, $"type={pMsg.providerType} provider={pMsg.providerKey}");
        ConnectionProviderBase prov = ConnectionProviderRepository.CreateProvider(localClient, pMsg);
        if (prov == null)
        {
            Logger.LogStatusWithMessage(NAME, Logger.Status.ProviderNotFound, $"type={pMsg.providerType} provider={pMsg.providerKey}");
            return;
        }
        createdProviders.Add(prov);
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
        Logger.LogStatusClientWithMessage(NAME, Logger.Status.ManagerClientConnected, c.clientID, $"codecMode={c.codecMode} nVideoTracks={c.videoTracks.Count} nAudioTracks={c.audioTracks.Count}");
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
        
        foreach (var t in c.videoTracks)
        {
            client.AddVideoTrack(new RemoteTrackInfo
            {
                clientID = c.clientID,
                providerKey = t.providerKey,
                trackID = t.trackID,
                capturerType = t.capturerType,
                trackType = t.trackType,
                trackSettings = t.trackSettings,
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
    public void DisconnectFromSession()
    {
        Logger.LogStatus(NAME, Logger.Status.ManagerDisconnecting);
        disconnectFromSessionInternal();
        foreach (var prov in createdProviders)
        {
            prov.Dispose();
        }
        Logger.LogStatus(NAME, Logger.Status.ManagerDisconnected);
        
    }
    protected abstract void disconnectFromSessionInternal();
    public abstract void AddVideoTrack(ReceivingTrackInfo track);
    public abstract void AddAudioTrack();

    private void onConnectedToSessionManager()
    {
        Logger.LogStatus(NAME, Logger.Status.ManagerConnectionSuccess);
        OnConnectedToSessionManager?.Invoke();
    }
    protected void onConnectedToSession(uint assignedClientID, SessionConnectionMessage connectionMessage, string sessionInfo)
    {
        Logger.LogStatus(NAME, Logger.Status.ManagerSessionJoined);
        LocalClient = new LocalConnectedClient(assignedClientID, connectionMessage.codecMode);

        foreach (var p in connectionMessage.providers)
        {
            foreach (var t in p.videoTracks)
            {
                LocalClient.AddVideoTrack(new LocalTrackInfo
                {
                    providerKey = p.providerKey,
                    trackID = t.trackID,
                    capturerType = t.capturerType,
                    trackType = t.trackType,
                    trackSettings = t.trackSettings
                });
            }
        }

        OnConnectedToSession?.Invoke(LocalClient, sessionInfo);
        foreach (var c in connectionMessage.clients)
        {
            onNewClientConnected(c); // TODO probably change this to JObject
        }
    }
    protected void onConnectedToSession(uint assignedClientID, string connectMessageJSON, string sessionInfo)
    {
       
        SessionConnectionMessage connectionMessage = SessionConnectionMessage.CreateFromJSON(connectMessageJSON);
        onConnectedToSession(assignedClientID, connectionMessage, sessionInfo);
    }
    protected void onSessionCreated(uint assignedClientID, string connectMessage, string sessionInfo)
    {
        Debug.Log(connectMessage);
        Logger.LogStatus(NAME, Logger.Status.ManagerSessionCreated);
        OnSessionCreated?.Invoke();
        onConnectedToSession(assignedClientID, connectMessage, sessionInfo);
    }


    public void CheckIfReadyToConnect()
    {
        lock (_lock)
        {
            if (isReadyToConnect && !readyToConnectCalled)
            {
                onReadyToConnect();
                return;
            }
        }
    }
    private void onReadyToConnect()
    {
        Logger.LogStatus(NAME, Logger.Status.ManagerReadyToConnect);
        readyToConnectCalled = true;
        OnReadyToConnect?.Invoke();
        
    }

    protected void onGenericMessageReceived(GenericSessionManagerMessage message) { 
        if(!genericMessagesubscribers.TryGetValue(message.messageType, out var subscriberDict))
        {
            return; // No subscribers
        }
        foreach(var subscriberList in subscriberDict.Values)
        {
            subscriberList.NotiyAll(message.message);
        }

    }

    public void SubscribeToGenericMessage<T>(string messageName, Action<T> callback)
    {
        lock (_lock) {
            if (!genericMessagesubscribers.ContainsKey(messageName))
                genericMessagesubscribers[messageName] = new();

            var subscriberDict = genericMessagesubscribers[messageName];
            if(!subscriberDict.ContainsKey(typeof(T)))
                subscriberDict[typeof(T)] = new GenericMessageList(json => JsonConvert.DeserializeObject<T>(json));
            var subscriberList = subscriberDict[typeof(T)];

            subscriberList.Subscribe((obj) =>
            {
                callback((T)obj);
            });
        } 
    }
}

