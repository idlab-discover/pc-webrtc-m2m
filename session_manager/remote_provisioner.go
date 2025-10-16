package main

import (
	"bytes"
	"encoding/json"
	"fmt"
	"net/http"
	"os"
)

const NameRemoteProvisioner = "RemoteProvisioner"

type RemoteProviderProvisioner struct {
	BaseProviderProvisioner
	ControllerAddress string
}

type RemoteProviderProvisionerConfig struct {
	ControllerAddress string `json:"controllerAddress"`
}

type RemoteProviderPair struct {
	ProviderType string `json:"providerType"`
	Address      string `json:"address"`
	Port         uint   `json:"port"`
}

func NewRemoteProviderProvisioner(configPath string) *RemoteProviderProvisioner {
	Log(NameRemoteProvisioner, Creating, true, true)
	var config RemoteProviderProvisionerConfig
	file, err := os.Open(configPath)
	LogWithMessage(NameRemoteProvisioner, ConfigLoading, true, true, fmt.Sprintf("configPath=%s", configPath))
	if err != nil {
		Log(NameRemoteProvisioner, ConfigLoadingFailed, true, true)
		panic(err)
	}
	defer file.Close()
	decoder := json.NewDecoder(file)
	if err := decoder.Decode(&config); err != nil {
		Log(NameLocalProvisioner, ConfigLoadingFailed, true, true)
		panic(err)
	}
	p := &RemoteProviderProvisioner{
		ControllerAddress: config.ControllerAddress,
	}

	LogWithMessage(NameRemoteProvisioner, Created, true, true, fmt.Sprintf("controllerAddress=%+v", p.ControllerAddress))
	return p
}

func (p *RemoteProviderProvisioner) CreateProvider(providerType string, managerIP string, pConn *ProviderConnection, extraCmdArgs string) {
	p.mut.Lock()
	defer p.mut.Unlock()
	requestBody := map[string]interface{}{
		"providerKey":  pConn.ProviderKey,
		"providerType": providerType,
		"managerIP":    managerIP,
	}
	jsonBody, err := json.Marshal(requestBody)
	if err != nil {
		//Log(NameRemoteProvisioner, "FailedToMarshalRequest", true, true)
		panic(err)
	}

	url := fmt.Sprintf("%s/start_provider", p.ControllerAddress)
	resp, err := http.Post(url, "application/json", bytes.NewBuffer(jsonBody))
	if err != nil {
		//Log(NameRemoteProvisioner, "HTTPPostFailed", true, true)
		panic(err)
	}
	defer resp.Body.Close()

	var result struct {
		Status  string `json:"status"`
		Address string `json:"address"`
		Port    uint   `json:"port"`
	}
	if err := json.NewDecoder(resp.Body).Decode(&result); err != nil {
		//Log(NameRemoteProvisioner, "FailedToDecodeResponse", true, true)
		panic(err)
	}

	pConn.Address = result.Address
	pConn.Port = result.Port

	LogWithMessage(NameRemoteProvisioner, CreatingProvider, true, true,
		fmt.Sprintf("configType=%s providerKey=%s assignedAddress=%s assignedPort=%d",
			providerType, pConn.ProviderKey, pConn.Address, pConn.Port))
}

func (p *RemoteProviderProvisioner) OnProviderClose(pc *ProviderConnection) {
	p.mut.Lock()
	defer p.mut.Unlock()

}
