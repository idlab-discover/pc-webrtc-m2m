using AOT;
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using UnityEngine;
using Debug = UnityEngine.Debug;

public class BaseSession : MonoBehaviour
{

    enum Color { red, green, blue, black, white, yellow, orange };
    [MonoPInvokeCallback(typeof(DLLLogger.debugCallback))]
    static void OnDebugCallback(IntPtr request, int color, int size)
    {
        // Ptr to string
        string debug_string = Marshal.PtrToStringAnsi(request, size);
        // Add specified color
        debug_string =
            String.Format("Realsense Capturing: {0}{1}{2}{3}{4}",
            "<color=",
            ((Color)color).ToString(), ">", debug_string, "</color>");
        // Log the string
        Debug.Log(debug_string);
    }
    // ################## Session Variables ####################
    private SessionInfo sessionInfo;
    private SessionManagerBase sessionManager;

    // ################## GameObjects ####################
    public List<GameObject> StartLocations;
    public PCSelf PCSelfPrefab;
    public PCReceiver PCReceiverPrefab;
    public GameObject Table;
    public PrefabFactory PipelineLocalPrefabFactory;
    public PrefabFactory PipelineRemotePrefabFactory;

    // ################# Private Variables ###############
    private readonly object _lock = new();
    private readonly ConcurrentQueue<Action> mainThreadActions = new();
    private PipelineLocalBase localPipeline;
    private Dictionary<uint, PipelineRemoteBase> remotePipelines = new();

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
        sessionManager.OnNewClientConnected += newClientConnectedCallback;
        sessionManager.OnClientDisconnected += clientDisconnectedCallback;
        sessionManager.OnConnectedToSessionManager += connectedToSessionManagerCallback;
        sessionManager.OnSessionCreated += sessionCreatedCallback;
        sessionManager.OnConnectedToSession += connectedToSessionCallback;
        sessionManager.OnReadyToConnect += ReadyToConnect;
        sessionManager.CheckIfReadyToConnect();
        
    }

    public void ReadyToConnect()
    {
        _ = sessionManager.ConnectToSessionManagerAsync();
    }

    void Update()
    {
        while (mainThreadActions.TryDequeue(out var action))
        {
            action();
        }
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
                    if (p.providerKey == "default")
                    {
                        Debug.LogWarning("Provider with key 'default' is reserved, skipping");
                        continue;
                    }

                    ConnectionProviderMessage provider = new()
                    {
                        providerKey = p.providerKey,
                        providerType = p.providerType,
                        providerSettings = p.providerSettings,
                    };

                    foreach (var t in p.videoTracks)
                    {
                        if (!tracks.TryGetValue(t.trackID, out var track))
                        {
                            Debug.LogWarning($"Track {t.trackID} not found in sessionInfo, skipping");
                            continue;
                        }
                        trackHasPreferredProvider[t.trackID] = true;
                        provider.videoTracks.Add(new()
                        {
                            providerKey = p.providerKey,
                            trackID = track.trackID,
                            capturerType = sessionInfo.capturerName, /*TODO CHANGE THIS TO BE MORE DYNAMIC*/
                            trackType = track.mode,
                            trackSettings = track.trackSettings,
                            isVideo = true,
                        });

                    }
                    if (provConfig.defaultProvider == p.providerKey)
                    {
                        Debug.Log($"Setting {p.providerKey} as default provider");
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
                    providerKey = "default",
                    providerType = "default",
                };
                message.providers.Add(defaultProvider);
            }

            foreach(var t in tracks)
            {
                if (!trackHasPreferredProvider[t.Key])
                {
                    defaultProvider.videoTracks.Add(new()
                    {
                        providerKey = defaultProvider.providerKey,
                        trackID = t.Value.trackID,
                        capturerType = sessionInfo.capturerName, /*TODO CHANGE THIS TO BE MORE DYNAMIC*/
                        trackType = t.Value.mode,
                        trackSettings = t.Value.trackSettings,
                        isVideo = true,
                    });
                }
            }
            
            sessionManager.CreateNewSession(sessionInfo.sessionManagerSettings.defaultName, message, "" /* IMPLEMENT SESSION CONFIG */);
        } catch (Exception ex)
        {
            Debug.LogException(ex);
        }
    }
    private void connectedToSessionCallback(LocalConnectedClient client, string additionalSessionInfo)
    {
        // Potentially change trackInfo in selfProviderManager
        // Loop over tracks in client and print info
        Debug.Log("Connected to session");
        Debug.Log($"Client {client.ClientID} connected with codec mode {client.CodecMode}");

        // TODO If factory is null => load from path maybe
        Debug.Log($"Registered prefabs count: {PipelineLocalPrefabFactory.registeredPrefabs.Count}");
        if (PipelineLocalPrefabFactory == null)
        {
            Debug.LogError("PipelineLocalPrefabFactory is null, please assign it in the inspector");
            return;
        }
        mainThreadActions.Enqueue(() =>
        {
            PipelineLocalPrefabFactory.registeredPrefabs.TryGetValue(client.CodecMode, out var prefab);
            if (prefab == null) 
            {
                Debug.LogError($"No prefab found for codec mode {client.CodecMode}");
                return;
            }
            if (prefab.GetComponent<PipelineLocalBase>() == null)
            {
                Debug.LogError($"Prefab for coded mode {client.CodecMode} does not have a PipelineLocalBase component.");
                return;
            }
            Debug.Log($"Using prefab for codec mode {client.CodecMode}");
            GameObject temp = Instantiate(prefab, Vector3.zero, Quaternion.identity);
            if (temp == null)
            {
                Debug.LogError("Failed to instantiate prefab for codec mode " + client.CodecMode);
                return;
            }

            localPipeline = temp.GetComponent<PipelineLocalBase>();
            localPipeline.Init(sessionInfo, client);
        });
        
        // Generate pipeline based on codecMode


    }
    private void sessionCreatedCallback() 
    {
        Debug.Log("Session created");
    }
    private void createSelfPrefab()
    {
        /*StartLocations[clientID].transform.position = new Vector3(sessionInfo.startPositions[clientID].x, sessionInfo.startPositions[clientID].y - 1, sessionInfo.startPositions[clientID].z);
        pcSelf = Instantiate(PCSelfPrefab, StartLocations[clientID].transform.position, StartLocations[clientID].transform.rotation);
        pcSelf.transform.parent = StartLocations[clientID].transform;
        pcSelf.SessionInfo = sessionInfo;

        if (sessionInfo.useMic)
        {
            pcSelf.InitAudioCapture();
        }*/
    }

    private void newClientConnectedCallback(RemoteConnectedClient client, string clientSettings)
    {
        Debug.Log("New client connected");
        if (PipelineLocalPrefabFactory == null)
        {
            Debug.LogError("PipelineLocalPrefabFactory is null, please assign it in the inspector");
            return;
        }
        mainThreadActions.Enqueue(() =>
        {
            PipelineRemotePrefabFactory.registeredPrefabs.TryGetValue(client.CodecMode, out var prefab);
            if (prefab == null)
            {
                Debug.LogError($"No prefab found for codec mode {client.CodecMode}");
                return;
            }
            if (prefab.GetComponent<PipelineRemoteBase>() == null)
            {
                Debug.LogError($"Prefab for coded mode {client.CodecMode} does not have a PipelineRemoteBase component.");
                return;
            }
            Debug.Log($"Using prefab for codec mode {client.CodecMode}");
            GameObject temp = Instantiate(prefab, Vector3.zero, Quaternion.identity);
            if (temp == null)
            {
                Debug.LogError("Failed to instantiate prefab for codec mode " + client.CodecMode);
                return;
            }
            lock(_lock)
            {
                //  TODO Handle this
                if (remotePipelines.ContainsKey(client.ClientID))
                {
                    Debug.LogError($"Remote pipeline for client {client.ClientID} already exists, skipping creation");
                    return;
                }
                PipelineRemoteBase remoteipeline = temp.GetComponent<PipelineRemoteBase>();
                remoteipeline.Init(sessionInfo, client);
                remotePipelines[client.ClientID] = remoteipeline;
            }
        });
    }
    private void clientDisconnectedCallback(RemoteConnectedClient client)
    {
        lock(_lock)
        {
            bool succes = remotePipelines.Remove(client.ClientID, out var remotePipeline);
            if(succes)
            {
                //TODO remotePipeline.Dispose();
            }
            
        }
    }
    void OnDestroy()
    {
        sessionManager?.DisconnectFromSession();
        Logger.ForceFlush();
        if(Application.platform == RuntimePlatform.WindowsEditor)
        {
            Process.Start("notepad.exe", Logger.FilePath);
        }
        
    }
    private void OnApplicationQuit()
    {
        Debug.Log("Application quitting, flushing logs");
        Logger.SetFlushAll();
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
