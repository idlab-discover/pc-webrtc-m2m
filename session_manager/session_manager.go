package main

import (
	"encoding/json"
	"fmt"
	"log"
	"net/http"
	"os"
	"strconv"
	"sync"
)

const NameManager = "SessionManager"

type SessionManager struct {
	config          SessionManagerConfig
	providers       map[string]*ProviderConnection
	providerConfigs *ProviderConfigRepository
	provisioner     ProviderProvisioner

	clientIDCounter uint
	clients         map[uint]*ClientConnection

	mut sync.Mutex
}

type SessionManagerConfig struct {
	VerifyAuthKey             bool                         `json:"verifyAuthKey"`
	Address                   string                       `json:"address"`
	DefaultProviderConfigPath string                       `json:"defaultProviderConfigPath"`
	ProvisionerType           string                       `json:"provisionerType"`
	ProvisionerConfigPath     string                       `json:"provisionerConfigPath"`
	ProvidersToCreate         []SessionManagerProviderPair `json:"providersToCreate"`

	IgnorePreferredClientID bool `json:"ignorePreferredClientID"`
}

type SessionManagerProviderPair struct {
	Type        string   `json:"type"`
	Key         string   `json:"key"`
	Address     string   `json:"address"`
	Port        uint     `json:"port"`
	ConnectedTo []string `json:"connectedTo"`
}

func NewSessionManager(configPath string) *SessionManager {
	Log(NameManager, Creating, true, true)
	Log(NameManager, ConfigLoading, true, true)
	var config SessionManagerConfig
	file, err := os.Open(configPath)
	if err != nil {
		log.Fatalf("Failed to open config file: %v", err)
	}
	defer file.Close()
	decoder := json.NewDecoder(file)
	if err := decoder.Decode(&config); err != nil {
		log.Fatalf("Failed to decode config file: %v", err)
	}
	LogWithMessage(NameManager, ConfigLoaded, true, true,
		fmt.Sprintf("address=%s defaultProviderConfigPath=%s  provisionerType=%s verifyAuthKey=%t providersToCreate=%+v",
			config.Address, config.DefaultProviderConfigPath, config.ProvisionerType, config.VerifyAuthKey, config.ProvidersToCreate),
	)

	ses := &SessionManager{
		config:          config,
		provisioner:     CreateProviderProvisioner(config.ProvisionerType, config.ProvisionerConfigPath),
		providerConfigs: NewProviderConfigRepository(config.DefaultProviderConfigPath),
		providers:       map[string]*ProviderConnection{},
		clients:         map[uint]*ClientConnection{},
		mut:             sync.Mutex{},
	}

	Log(NameManager, Created, true, true)
	return ses
}

func (sm *SessionManager) CreateDefaultProviders() {
	for i := range sm.config.ProvidersToCreate {
		proConfig := sm.config.ProvidersToCreate[i]
		sm.CreateProvider(proConfig.Type, proConfig.Key, proConfig.Address, proConfig.Port, proConfig.ConnectedTo)
	}
}

func (sm *SessionManager) CreateProvider(providerType string, providerKey string, address string, port uint, connectedTo []string) *ProviderConnection {
	sm.mut.Lock()

	if pc, exists := sm.providers[providerKey]; exists {
		Log(NameManager, ProviderAlreadyExists, true, true)
		sm.mut.Unlock()
		return pc
	}
	authKey := ""
	if sm.config.VerifyAuthKey {
		authKey = sm.generateAuthKey() // TODO
	}
	config := sm.providerConfigs.GetConfigForTypeAndKey(providerType, providerKey)
	if config == nil {
		return nil
	}
	pc := NewProviderConnection(sm, providerType, providerKey, address, port, authKey, config.Settings)
	sm.providers[providerKey] = pc
	// TODO This will have to fixed once we go for more dynamic provider connections
	for _, otherProviderKey := range connectedTo {
		otherProvider, exists := sm.providers[otherProviderKey]
		if !exists {
			continue
		}
		pc.AddRemoteProvider(otherProvider.ProviderType, otherProvider.ProviderKey, otherProvider.Address, otherProvider.Port, true)
		otherProvider.AddRemoteProvider(pc.ProviderType, pc.ProviderKey, pc.Address, pc.Port, false)
	}
	sm.mut.Unlock()
	sm.provisioner.CreateProvider(providerType, sm.config.Address, pc, config.ExtraCmdArgs)
	return pc
}

func (sm *SessionManager) StartListening() {
	// TODO Websocket health check
	// TODO HTTP server for health check + metrics
	http.HandleFunc("/websocket_provider", sm.websocketHandlerProvider)
	http.HandleFunc("/websocket_client", sm.websocketHandlerClient)
	http.HandleFunc("/websocket_reconnect_client", sm.websocketHandlerClient)
	log.Fatal(http.ListenAndServe(sm.config.Address, nil))
}

func (sm *SessionManager) websocketHandlerProvider(w http.ResponseWriter, r *http.Request) {
	providerKey := r.URL.Query().Get("providerKey")
	if providerKey == "" {
		fmt.Println("WebRTCSFU: webSocketHandler: No providerID provided, returning 400")
		http.Error(w, "No clientID provided", http.StatusBadRequest)
		return
	}

	sm.mut.Lock()
	provider := sm.providers[providerKey]
	sm.mut.Unlock()
	if provider == nil {
		Log(NameManager, InvalidProvider, true, true)
		http.Error(w, "Invalid provider ID", http.StatusBadRequest)
		return
	}
	if sm.config.VerifyAuthKey {
		authKeyS := r.URL.Query().Get("authKey")
		if authKeyS == "" {
			Log(NameManager, InvalidProviderAuthKey, true, true)
			http.Error(w, "No authKey provided", http.StatusBadRequest)
			return
		}
		if provider.AuthKey != authKeyS {
			Log(NameManager, InvalidProviderAuthKey, true, true)
			http.Error(w, "Invalid authKey", http.StatusForbidden)
			return
		}
	}

	// Upgrade HTTP request to Websocket
	unsafeWebSocketConn, err := upgrader.Upgrade(w, r, nil)
	if err != nil {
		fmt.Printf("WebRTCSFU: webSocketHandler: ERROR: %s\n", err)
		return
	}

	fmt.Println("WebRTCSFU: webSocketHandler: Websocket handler upgraded")

	provider.SetupProvider(
		&ThreadSafeWebsocket{
			unsafeWebSocketConn, sync.Mutex{},
		},
	)

}

func (sm *SessionManager) websocketHandlerClient(w http.ResponseWriter, r *http.Request) {
	preferredClientIDS := r.URL.Query().Get("preferredClientID")
	var clientID uint
	LogWithMessage(NameManager, IncomingClient, true, true, fmt.Sprintf("clientIDS=%s", preferredClientIDS))
	sm.mut.Lock()
	println(!sm.config.IgnorePreferredClientID, preferredClientIDS != "", preferredClientIDS)
	if !sm.config.IgnorePreferredClientID && preferredClientIDS != "" {
		clientID64, err := strconv.ParseUint(preferredClientIDS, 10, 64)
		if err != nil {
			Log(NameManager, InvalidClientID, true, true)
			http.Error(w, "Invalid preferredclientID", http.StatusBadRequest)
			sm.mut.Unlock()
			return
		}
		clientID = uint(clientID64)
	} else {
		clientID = sm.clientIDCounter
		sm.clientIDCounter++
	}
	clOld, exists := sm.clients[clientID]
	if exists && clOld.Status != ClientStatusCreated {
		Log(NameManager, ClientAlreadyExists, true, true)
		http.Error(w, "ClientID already in use", http.StatusBadRequest)
		sm.mut.Unlock()
		return
	}
	authKey := ""
	if sm.config.VerifyAuthKey {
		authKey = sm.generateAuthKey() // TODO
	}

	client := NewClientConnection(sm, clientID, authKey)
	sm.clients[clientID] = client
	sm.mut.Unlock()

	// Upgrade HTTP request to Websocket

	unsafeWebSocketConn, err := upgrader.Upgrade(w, r, nil)

	if err != nil {
		fmt.Printf("WebRTCSFU: webSocketHandler: ERROR: %s\n", err)
		return
	}

	fmt.Println("WebRTCSFU: webSocketHandler: Websocket handler upgraded")

	client.SetupClient(
		&ThreadSafeWebsocket{
			unsafeWebSocketConn, sync.Mutex{},
		},
	)

}

func (sm *SessionManager) websocketHandlerReconnectClient(w http.ResponseWriter, r *http.Request) {
	// Check ClientID + AuthKey in URL
}

func (sm *SessionManager) generateAuthKey() string {
	return "TODO" // TODO
}

func (sm *SessionManager) OnProviderClose(pc *ProviderConnection) {
	sm.mut.Lock()
	defer sm.mut.Lock()
	LogWithMessage(NameManager, ProviderClosed, true, true, fmt.Sprintf("providerKey=%s", pc.ProviderKey))
	sm.provisioner.OnProviderClose(pc)
	delete(sm.providers, pc.ProviderKey)
}

func (sm *SessionManager) OnClientAddedToProvider(pc *ProviderConnection, addedMsg ProviderClientAddedMessage) {
	sm.mut.Lock()
	defer sm.mut.Unlock()
	pc.mut.Lock()
	defer pc.mut.Unlock()
	client, exists := sm.clients[addedMsg.ClientID]
	if !exists {
		// TODO Log
		LogWithMessage(NameManager, InvalidClientID, true, true, fmt.Sprintf("providerKey=%s clientID=%d", pc.ProviderKey, addedMsg.ClientID))
		return
	}
	pcClient := pc.Clients[addedMsg.ClientID]
	pcClient.IsConnected = true
	for _, t := range addedMsg.SenderVideoTracks {
		pcClient.VideoTracks[t.TrackID].IsConnected = true
	}
	for _, t := range addedMsg.SenderAudioTracks {
		pcClient.AudioTracks[t.TrackID].IsConnected = true
	}
	// Inform connected providers about the new tracks
	providerRemoteProviderClient := ProviderRemoteProviderClientMessage{
		ProviderKey: pc.ProviderKey,
		ClientID:    pcClient.ClientID,
		VideoTracks: addedMsg.SenderVideoTracks,
		AudioTracks: addedMsg.SenderAudioTracks,
	}
	for _, provider := range pc.RemoteProviders {
		remoteProvider, exists := sm.providers[provider.ProviderKey]
		if !exists {
			continue
		}
		remoteProvider.VirtualClients[addedMsg.ClientID] = &ProviderRemoteProviderClient{
			ClientID:    addedMsg.ClientID,
			VideoTracks: map[string]*ProviderTrackSimple{},
			AudioTracks: map[string]*ProviderTrackSimple{},
		}
		for _, videoTrack := range addedMsg.SenderVideoTracks {
			provider.ForwardedVideoTracks[videoTrack.TrackID] = &ProviderTrackSimple{
				TrackID:     videoTrack.TrackID,
				IsConnected: false,
			}
			remoteProvider.VirtualClients[addedMsg.ClientID].VideoTracks[videoTrack.TrackID] = &ProviderTrackSimple{
				TrackID:     videoTrack.TrackID,
				IsConnected: false,
			}
		}
		for _, audioTrack := range addedMsg.SenderAudioTracks {
			provider.ForwardedAudioTracks[audioTrack.TrackID] = &ProviderTrackSimple{
				TrackID:     audioTrack.TrackID,
				IsConnected: false,
			}
			remoteProvider.VirtualClients[addedMsg.ClientID].AudioTracks[audioTrack.TrackID] = &ProviderTrackSimple{
				TrackID:     audioTrack.TrackID,
				IsConnected: false,
			}
		}
		// -> Inform provider tracks are available at SFU X for client Z
		// Send Message containing clientID + trackID
		remoteProvider.websocket.WriteJSONMessageSafe("AddVirtualClient", providerRemoteProviderClient)
	}

	// -> SFU Y will create the tracks on his side for SFU X and attached them to client Z
	// -> SFU Y will inform SFU X that the tracks are ready and can be forwarded
	// -> Inform clients that they can subscribe to these tracks
	// -> Clients subscribe to tracks from SFU Y, using a "virtual client" that corresponds to SFU X
	// ->
	// Change this to use the client specific provider, i.e. in the case of forwarding providers

	// Only send this message to the clients that are connected to this provider
	// For the other clients the remote providers will call a "VirtualClientAddedToProvider" message
	// which will then be used to inform the clients connected to those providers
	msgAllClients := &ProviderTracksConnectedMessage{
		ProviderKey: pc.ProviderKey,
		ClientID:    pcClient.ClientID,
		VideoTracks: pcClient.GetConnectedVideoTracks(),
		AudioTracks: pcClient.GetConnectedAudioTracks(),
	}

	// Inform remote clients that they can now subscribe to these tracks properly
	msgRemoteClients := []RemoteClientSimple{}
	for pcOtherID, pcOtherClient := range pc.Clients {
		if pcOtherID == client.ClientID {
			continue
		}
		if pcOtherClient.IsConnected == false {
			continue
		}
		otherC := sm.clients[pcOtherID]
		otherC.websocket.WriteJSONMessageSafe("ProviderRemoteClientTracksConnected", msgAllClients)
		msgRemoteClients = append(msgRemoteClients, RemoteClientSimple{
			ProviderKey: pc.ProviderKey,
			ClientID:    pcOtherID,
			VideoTracks: pcOtherClient.GetConnectedVideoTracks(),
			AudioTracks: pcOtherClient.GetConnectedAudioTracks(),
		})
	}
	for pcOtherID, pcOtherClient := range pc.VirtualClients {
		msgRemoteClients = append(msgRemoteClients, RemoteClientSimple{
			ProviderKey: pc.ProviderKey,
			ClientID:    pcOtherID,
			VideoTracks: pcOtherClient.GetConnectedVideoTracks(),
			AudioTracks: pcOtherClient.GetConnectedAudioTracks(),
		})
	}
	// TODO Iterate over virtual clients as well?

	msgToClient := ClientAddedToProviderMessage{
		ProviderType:      pc.ProviderType,
		ProviderKey:       pc.ProviderKey,
		Address:           pc.Address,
		Port:              pc.Port,
		Config:            pc.config,
		SenderVideoTracks: addedMsg.SenderVideoTracks,
		SenderAudioTracks: addedMsg.SenderAudioTracks,
		RemoteClients:     msgRemoteClients,
	}
	client.websocket.WriteJSONMessageSafe("ClientAddedToProvider", msgToClient)

	LogWithMessage(NameManager, ClientAddedToProvider, true, true, fmt.Sprintf("type=real providerKey=%s clientID=%d", pc.ProviderKey, addedMsg.ClientID))
}

func (sm *SessionManager) OnVirtualClientAddedToProvider(pc *ProviderConnection, addedMsg ProviderRemoteProviderClientMessage) {
	LogWithMessage(NameManager, MutLock, true, true, "try e=sm func=OnVirtualClientAddedToProvider")
	sm.mut.Lock()
	LogWithMessage(NameManager, MutLock, true, true, "e=sm func=OnVirtualClientAddedToProvider")
	defer sm.mut.Unlock()
	pc.mut.Lock()
	LogWithMessage(NameManager, MutLock, true, true, "e=pc func=OnVirtualClientAddedToProvider")
	defer pc.mut.Unlock()
	client, exists := sm.clients[addedMsg.ClientID]
	if !exists {
		// TODO Log
		LogWithMessage(NameManager, InvalidClientID, true, true, fmt.Sprintf("providerKey=%s clientID=%d", pc.ProviderKey, addedMsg.ClientID))
		return
	}
	pcClient := pc.VirtualClients[addedMsg.ClientID]
	for _, t := range addedMsg.VideoTracks {
		pcClient.VideoTracks[t.TrackID].IsConnected = true
	}
	for _, t := range addedMsg.AudioTracks {
		pcClient.AudioTracks[t.TrackID].IsConnected = true
	}
	// TODO In the future we might need to inform connected providers here
	// But only if they are not getting the forwarded tracks from the "main" provider
	msgAllClients := &ProviderTracksConnectedMessage{
		ProviderKey: pc.ProviderKey,
		ClientID:    pcClient.ClientID,
		VideoTracks: pcClient.GetConnectedVideoTracks(),
		AudioTracks: pcClient.GetConnectedAudioTracks(),
	}

	// Inform remote clients that they can now subscribe to these tracks properly
	for pcOtherID := range pc.Clients {
		if pcOtherID == client.ClientID {
			continue
		}
		otherC := sm.clients[pcOtherID]
		otherC.websocket.WriteJSONMessageSafe("ProviderRemoteClientTracksConnected", msgAllClients)
	}

	LogWithMessage(NameManager, ClientAddedToProvider, true, true, fmt.Sprintf("type=virtual providerKey=%s clientID=%d", pc.ProviderKey, addedMsg.ClientID))
}

func (sm *SessionManager) OnClientClose(cl *ClientConnection) {
	sm.mut.Lock()
	defer sm.mut.Lock()
	// TODO
	// Make sure to keep client connection semi-alive so he can reconnect
}
