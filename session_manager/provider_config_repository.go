package main

import (
	"encoding/json"
	"io/ioutil"
	"log"
	"os"
	"sync"
)

const NameProviderConfigRepository = "ProviderConfigRepository"

type ProviderConfigRepository struct {
	configs map[string]*ProviderTypeConfigs
	mut     sync.Mutex
}

func NewProviderConfigRepository(defaultConfigPath string) *ProviderConfigRepository {
	Log(NameProviderConfigRepository, Creating, true, true)
	pr := &ProviderConfigRepository{
		configs: make(map[string]*ProviderTypeConfigs),
		mut:     sync.Mutex{},
	}
	if defaultConfigPath != "" {
		Log(NameProviderConfigRepository, ProviderConfigReadDefaultStart, true, true)
		pr.loadDefaultConfigs(defaultConfigPath)
		Log(NameProviderConfigRepository, ProviderConfigReadDefaultEnd, true, true)
	}
	Log(NameProviderConfigRepository, Created, true, true)
	return pr
}

func (pr *ProviderConfigRepository) loadDefaultConfigs(configPath string) {
	data, err := ioutil.ReadFile(configPath)
	if err != nil {
		log.Fatalf("failed to read config file: %v", err)
	}

	var configs []ProviderConfigPair
	if err := json.Unmarshal(data, &configs); err != nil {
		log.Fatalf("failed to unmarshal config file: %v", err)
	}

	// Use configs as needed
	log.Printf("Loaded %d provider configs", len(configs))
	mapPerType := make(map[string]map[string]string)
	defaultConfigForType := make(map[string]string)
	for i := range configs {
		config := configs[i]

		if _, exists := mapPerType[config.ProviderType]; !exists {
			mapPerType[config.ProviderType] = make(map[string]string)
		}
		t := mapPerType[config.ProviderType]
		if config.ProviderKey != "" {
			t[config.ProviderKey] = config.DefaultConfigPath
		}
		if config.IsDefaultForType {
			defaultConfigForType[config.ProviderType] = config.DefaultConfigPath
		}

	}
	configsPerType := make(map[string]*ProviderTypeConfigs)
	for k, v := range mapPerType {
		configsPerType[k] = NewProviderTypeConfigs(v, defaultConfigForType[k])
	}
	pr.configs = configsPerType
}

func (pr *ProviderConfigRepository) GetConfigForTypeAndKey(providerType string, providerKey string) *ProviderConfig {
	configsForType, typeExists := pr.configs[providerType]
	if !typeExists {
		return nil
	}
	config, keyExists := configsForType.configs[providerKey]
	if !keyExists {
		return &configsForType.defaultConfig
	}
	return &config
}

type ProviderTypeConfigs struct {
	configs       map[string]ProviderConfig
	defaultConfig ProviderConfig
}

func NewProviderTypeConfigs(configPaths map[string]string, defaultConfigPath string) *ProviderTypeConfigs {
	ptc := &ProviderTypeConfigs{
		configs: make(map[string]ProviderConfig),
	}
	ptc.defaultConfig = getMapFromJSON(defaultConfigPath)
	for ProviderKey, configPath := range configPaths {
		ptc.configs[ProviderKey] = getMapFromJSON(configPath)
	}

	return ptc
}

func getMapFromJSON(fileName string) ProviderConfig {
	file, err := os.ReadFile(fileName)
	if err != nil {
		panic(err)
	}
	var data ProviderConfig
	err = json.Unmarshal(file, &data)

	if err != nil {
		panic(err)
	}
	return data
}
