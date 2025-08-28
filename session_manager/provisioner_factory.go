package main

func CreateProviderProvisioner(proType string, configPath string) ProviderProvisioner {
	switch proType {
	default:
		return NewLocalProviderProvisioner(configPath)
	}
}
