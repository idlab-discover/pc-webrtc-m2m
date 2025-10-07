package main

import "fmt"

const NameRemoteProviderFactory = "RemoteProviderFactory"

func CreateRemoteProvider(proType string, providerKey string, address string, port uint, authKey string) ProviderConnection {
	LogWithMessage(NameRemoteProviderFactory, Creating, true, true, fmt.Sprintf("proType=%s providerKey=%s address=%s port=%d", proType, providerKey, address, port))
	var Provider ProviderConnection
	switch proType {
	case "webrtc_sfu":
		Provider = NewRemoteSFUConnection(providerKey, address, port, authKey)
	default:
		Provider = nil
	}
	if Provider == nil {
		LogWithMessage(NameRemoteProviderFactory, Failed, true, true, fmt.Sprintf("proType=%s providerKey=%s", proType, providerKey))
		return nil
	} else {
		LogWithMessage(NameRemoteProviderFactory, Created, true, true, fmt.Sprintf("proType=%s providerKey=%s", proType, providerKey))
	}
	return Provider
}
