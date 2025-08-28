package main

import "sync"

type ProviderConfigPair struct {
	ProviderType      string `json:"providerType"`
	ProviderKey       string `json:"providerKey"`      // If not empty specific
	IsDefaultForType  bool   `json:"isDefaultForType"` // If true this config will be used if ProviderKey doesnt appear
	DefaultConfigPath string `json:"defaultConfigPath"`
}

type ProviderConfig struct {
	ExtraCmdArgs string                 `json:"extraCmdArgs"`
	Settings     map[string]interface{} `json:"settings"`
}

type ProviderProvisioner interface {
	CreateProvider(configType string, managerIP string, pConn *ProviderConnection, extraCmdArgs string)
}

type BaseProviderProvisioner struct {
	mut sync.Mutex
}
