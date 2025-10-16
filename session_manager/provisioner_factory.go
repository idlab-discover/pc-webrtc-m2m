package main

func CreateProviderProvisioner(proType string, configPath string) ProviderProvisioner {
	switch proType {
	case "remote":
		return NewRemoteProviderProvisioner(configPath)
	default:
		return NewLocalProviderProvisioner(configPath)
	}
}
