package main

import (
	"encoding/json"
	"fmt"
	"log"
	"net/http"
	"os"
	"sync"
)

const NameManager = "SessionManager"

type SessionManager struct {
	config          SessionManagerConfig
	providers       map[string]*ProviderConnection
	providerConfigs *ProviderConfigRepository
	provisioner     ProviderProvisioner

	mut sync.Mutex
}

type SessionManagerConfig struct {
	VerifyAuthKey             bool                         `json:"verifyAuthKey"`
	Address                   string                       `json:"address"`
	DefaultProviderConfigPath string                       `json:"defaultProviderConfigPath"`
	ProvisionerType           string                       `json:"provisionerType"`
	ProvisionerConfigPath     string                       `json:"provisionerConfigPath"`
	ProvidersToCreate         []SessionManagerProviderPair `json:"providersToCreate"`
}

type SessionManagerProviderPair struct {
	Type    string `json:"type"`
	Key     string `json:"key"`
	Address string `json:"address"`
	Port    uint   `json:"port"`
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
		mut:             sync.Mutex{},
	}
	for i := range config.ProvidersToCreate {
		pro := config.ProvidersToCreate[i]
		ses.CreateProvider(pro.Type, pro.Key, pro.Address, pro.Port)
	}
	Log(NameManager, Created, true, true)
	return ses
}

func (sm *SessionManager) CreateProvider(providerType string, providerKey string, address string, port uint) {
	sm.mut.Lock()

	if _, exists := sm.providers[providerKey]; exists {
		Log(NameManager, ProviderAlreadyExists, true, true)
		return
	}
	authKey := ""
	if sm.config.VerifyAuthKey {
		authKey = sm.generateAuthKey() // TODO
	}
	config := sm.providerConfigs.GetConfigForTypeAndKey(providerType, providerKey)
	if config == nil {
		return
	}
	pc := NewProviderConnection(providerKey, address, port, authKey, config.Settings)
	sm.providers[providerKey] = pc
	sm.mut.Unlock()
	sm.provisioner.CreateProvider(providerType, sm.config.Address, pc, config.ExtraCmdArgs)
}

func (sm *SessionManager) StartListening() {
	http.HandleFunc("/websocket_provider", sm.websocketHandlerProvider)
	http.HandleFunc("/websocket_client", sm.websocketHandlerClient)
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

}

func (sm *SessionManager) generateAuthKey() string {
	return "TODO" // TODO
}
