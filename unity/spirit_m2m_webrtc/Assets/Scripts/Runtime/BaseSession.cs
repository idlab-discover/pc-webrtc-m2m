using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using UnityEngine;
using Debug = UnityEngine.Debug;

public class BaseSession : MonoBehaviour
{
    // ################## Session Variables ####################
    private SessionInfo sessionInfo;
    private SessionManagerBase sessionManager;

    // ################## GameObjects ####################
    public List<GameObject> StartLocations;
    public PCSelf PCSelfPrefab;
    public PCReceiver PCReceiverPrefab;
    public GameObject Table;

    // ################# Private Variables ###############
    private readonly object _lock = new();
    private int clientID;
    private PCSelf pcSelf;
    private Dictionary<uint, PCReceiver> pcReceivers = new();
    private SelfProviderManager selfProviderManager = new();

    void Start()
    {
        sessionInfo = SessionInfo.CreateFromJSON(Application.dataPath + "/config/session_config.json");
        Logger.Init(sessionInfo.loggerSettings);
        sessionManager = SessionManagerRepository.CreateAndGetManager(sessionInfo.sessionManagerSettings.type, sessionInfo.sessionManagerSettings.configPath);
        if(sessionManager == null)
        {
            Debug.LogError("SessionManager creation failed");
            return;
        }
        clientID = sessionInfo.clientID;
        
        sessionManager.OnNewClientConnected += newClientConnectedCallback;
        sessionManager.OnClientDisconnected += clientDisconnectedCallback;
        sessionManager.OnConnectedToSessionManager += connectedToSessionManagerCallback;
        sessionManager.OnSessionCreated += sessionCreatedCallback;
        sessionManager.OnConnectedToSession += connectedToSessionCallback;
        _ = sessionManager.ConnectToSessionManagerAsync();
    }

    void Update()
    {
        
    }
    private void connectedToSessionManagerCallback()
    {
        try
        {
            PreferredProvidersConfig provConfig = PreferredProvidersConfig.CreateFromJSON(sessionInfo.providersConfigPath);
            
            JoinSessionMessage message = new()
            {
                preferredClientID = (uint)sessionInfo.clientID,
            };
            // Add all not chosen tracks to default provider
            Dictionary<string, CodecModeTrack> tracks = collectTrackInfo();
            Dictionary<string, bool> trackHasPreferredProvider = new();
            ConnectionProviderMessage defaultProvider = null;
            foreach (var t in tracks)
            {
                trackHasPreferredProvider[t.Key] = false;
            }
            if (provConfig != null) {
                foreach (var p in provConfig.providers)
                {
                    if (p.key == "default")
                    {
                        Debug.LogWarning("Provider with key 'default' is reserved, skipping");
                        continue;
                    }

                    ConnectionProviderMessage provider = new()
                    {
                        key = p.key,
                        type = p.type,
                        providerSettings = p.providerSettings,
                    };

                    foreach (var t in p.sendingTracks)
                    {
                        if (!tracks.TryGetValue(t.trackID, out var track))
                        {
                            Debug.LogWarning($"Track {t.trackID} not found in sessionInfo, skipping");
                            continue;
                        }
                        trackHasPreferredProvider[t.trackID] = true;
                        provider.sendingTracks.Add(new()
                        {
                            providerKey = p.key,
                            trackID = track.trackID,
                            capturerType = sessionInfo.capturerName, /*TODO CHANGE THIS TO BE MORE DYNAMIC*/
                            trackType = track.mode,
                            trackSettings = track.trackSettings,
                        });

                    }
                    if (provConfig.defaultProvider == p.key)
                    {
                        Debug.Log($"Setting {p.key} as default provider");
                        defaultProvider = provider;
                    }
                    message.providers.Add(provider);
                }
            }
            
            // Add default provider with all tracks that do not have a preferred provider
            if (defaultProvider == null)
            {
                defaultProvider = new()
                {
                    key = "default",
                    type = "default",
                };
                message.providers.Add(defaultProvider);
            }

            foreach(var t in tracks)
            {
                if (!trackHasPreferredProvider[t.Key])
                {
                    defaultProvider.sendingTracks.Add(new()
                    {
                        providerKey = defaultProvider.key,
                        trackID = t.Value.trackID,
                        capturerType = sessionInfo.capturerName, /*TODO CHANGE THIS TO BE MORE DYNAMIC*/
                        trackType = t.Value.mode,
                        trackSettings = t.Value.trackSettings,
                    });
                }
            }
            
            sessionManager.CreateNewSession(sessionInfo.sessionManagerSettings.defaultName, message, "" /* IMPLEMENT SESSION CONFIG */);
        } catch (Exception ex)
        {
            Debug.LogException(ex);
        }
    }
    private void connectedToSessionCallback(ConnectedClient client, string sessionInfo)
    {
        // Potentially change trackInfo in selfProviderManager
        // Loop over tracks in client and print info
        Debug.Log("Connected to session");
        Debug.Log($"Client {client.ClientID} connected with codec mode {client.CodecMode}");
        

        // Generate pipeline based on codecMode

    }
    private void sessionCreatedCallback()
    {
        Debug.Log("Session created");
    }
    private void createSelfPrefab()
    {
        StartLocations[clientID].transform.position = new Vector3(sessionInfo.startPositions[clientID].x, sessionInfo.startPositions[clientID].y - 1, sessionInfo.startPositions[clientID].z);
        pcSelf = Instantiate(PCSelfPrefab, StartLocations[clientID].transform.position, StartLocations[clientID].transform.rotation);
        pcSelf.transform.parent = StartLocations[clientID].transform;
        pcSelf.SessionInfo = sessionInfo;

        if (sessionInfo.useMic)
        {
            pcSelf.InitAudioCapture();
        }
    }

    private void newClientConnectedCallback(ConnectedClient client, string clientSettings)
    {
        Debug.Log("New client connected");
        /*clientDisconnectedCallback(clientID);
        StartLocations[(int)clientID].transform.position = new Vector3(sessionInfo.startPositions[clientID].x, sessionInfo.startPositions[clientID].y, sessionInfo.startPositions[clientID].z);
        PCReceiver pcReceiver = Instantiate(PCReceiverPrefab, StartLocations[(int)clientID].transform.position, StartLocations[(int)clientID].transform.rotation);
        pcReceiver.transform.parent = StartLocations[(int)clientID].transform;
        pcReceiver.ClientID = clientID;
        pcReceiver.NDescriptions = NDescriptions;
        pcReceiver.AudioParams = sessionInfo.audioPlayback;
        pcReceivers[clientID] = pcReceiver);*/
    }
    private void clientDisconnectedCallback(ConnectedClient client)
    {
        lock(_lock)
        {
            bool succes = pcReceivers.Remove(client.ClientID, out var receiver);
            if(succes)
            {
                Destroy(receiver);
            }
            
        }
    }
    void OnDestroy()
    {
        Logger.ForceFlush();
        if(Application.platform == RuntimePlatform.WindowsEditor)
        {
            Process.Start("notepad.exe", Logger.FilePath);
        }
        
    }

    // TODO
    private Dictionary<string, CodecModeTrack> collectTrackInfo()
    {
        return TrackInfoGatherer.GatherTrackInfo(sessionInfo);
        // Collect from providerConfig
        // Collect from SessionInfo
        //              Type of content i.e. pc or raw
        //              Type of camera  i.e. artificial, prerecKinect, realsense etc...
        //
        //              Support Audio => Add AudioTrack
    }
}
