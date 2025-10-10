package main

import (
	"fmt"
	"goweb/shared/src/logger"
)

const NameRemoteProviderFactory = "RemoteProviderFactory"

func CreateRemoteProvider(parent *SFU, proType string, providerKey string, address string, port uint, authKey string) ProviderConnection {
	logger.LogWithMessage(NameRemoteProviderFactory, logger.Creating, true, true, fmt.Sprintf("proType=%s providerKey=%s address=%s port=%d", proType, providerKey, address, port))
	var Provider ProviderConnection
	switch proType {
	case "webrtc_sfu":
		Provider = NewRemoteSFUConnection(parent, providerKey, address, port, authKey)
	default:
		Provider = nil
	}
	if Provider == nil {
		logger.LogWithMessage(NameRemoteProviderFactory, logger.Failed, true, true, fmt.Sprintf("proType=%s providerKey=%s", proType, providerKey))
		return nil
	} else {
		logger.LogWithMessage(NameRemoteProviderFactory, logger.Created, true, true, fmt.Sprintf("proType=%s providerKey=%s", proType, providerKey))
	}
	return Provider
}
