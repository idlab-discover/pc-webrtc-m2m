package main

import (
	"encoding/json"
	"fmt"
	"os"
	"os/exec"
)

const NameLocalProvisioner = "LocalProvisioner"

type LocalProviderProvisioner struct {
	BaseProviderProvisioner
	portsInUse map[uint]bool
	AssignPort bool
	Paths      map[string]string
}

type LocalProviderProvisionerConfig struct {
	PortRanges    []PortRange    `json:"portRanges"`
	AssignPort    bool           `json:"assignPort"`
	ProviderPaths []ProviderPath `json:"providerPaths"`
}

type ProviderPath struct {
	ProviderType string `json:"providerType"`
	Path         string `json:"path"`
}

type PortRange struct {
	Start uint `json:"start"`
	End   uint `json:"end"`
}

func NewLocalProviderProvisioner(configPath string) *LocalProviderProvisioner {
	Log(NameLocalProvisioner, Creating, true, true)
	var config LocalProviderProvisionerConfig
	file, err := os.Open(configPath)
	LogWithMessage(NameLocalProvisioner, ConfigLoading, true, true, fmt.Sprintf("configPath=%s", configPath))
	if err != nil {
		Log(NameLocalProvisioner, ConfigLoadingFailed, true, true)
		panic(err)
	}
	defer file.Close()
	decoder := json.NewDecoder(file)
	if err := decoder.Decode(&config); err != nil {
		Log(NameLocalProvisioner, ConfigLoadingFailed, true, true)
		panic(err)
	}
	p := &LocalProviderProvisioner{
		portsInUse: make(map[uint]bool),
		Paths:      make(map[string]string),
		AssignPort: config.AssignPort,
	}
	for i := range config.ProviderPaths {
		path := config.ProviderPaths[i]
		p.Paths[path.ProviderType] = path.Path
	}
	for i := range config.PortRanges {
		portRange := config.PortRanges[i]
		for j := portRange.Start; j <= portRange.End; j++ {
			p.portsInUse[j] = false
		}
	}
	LogWithMessage(NameLocalProvisioner, Created, true, true, fmt.Sprintf("assignPorts=%t portRanges=%+v", p.AssignPort, config.PortRanges))
	return p
}

func (p *LocalProviderProvisioner) CreateProvider(providerType string, managerIP string, pConn *ProviderConnection, extraCmdArgs string) {
	p.mut.Lock()
	defer p.mut.Unlock()
	pConn.Address = "127.0.0.1"
	if p.AssignPort {
		for candidatePort, inUse := range p.portsInUse {
			if !inUse {
				p.portsInUse[candidatePort] = true
				pConn.Port = candidatePort
				break
			}

		}
	}
	path, exists := p.Paths[providerType]
	if !exists {
		panic("cannot create provider")
	}
	LogWithMessage(NameLocalProvisioner, CreatingProvider, true, true,
		fmt.Sprintf("configType=%s providerKey=%s assignedAddress=%s assignedPort=%d path=%s",
			providerType, pConn.ProviderKey, pConn.Address, pConn.Port, path))
	portStr := fmt.Sprintf("%d", pConn.Port)
	args := []string{
		"--managerIP", managerIP,
		"--address", pConn.Address,
		"--providerKey", pConn.ProviderKey,
		"--port", portStr,
	}
	if pConn.AuthKey != "" {
		args = append(args, "--authKey", pConn.AuthKey)
	}

	cmd := exec.Command(path, args...)
	cmd.Stdout = os.Stdout
	cmd.Stderr = os.Stderr
	err := cmd.Start()
	if err != nil {
		panic(err)
	}
	// TODO save processs for gracefull shutdown
}

func (p *LocalProviderProvisioner) OnProviderClose(pc *ProviderConnection) {
	p.mut.Lock()
	defer p.mut.Unlock()
	if !p.AssignPort {
		return
	}
	p.portsInUse[pc.Port] = false
}
