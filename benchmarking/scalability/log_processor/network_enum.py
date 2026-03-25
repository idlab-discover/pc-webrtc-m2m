"""Network event/status codes as a Python IntEnum.

Converted from a Go-style const block. Use NetworkEnum.NAME to refer to
specific codes and int(NetworkEnum.NAME) to get the numeric value.
"""

from enum import IntEnum


class NetworkEnum(IntEnum):
	"""Network event/status codes."""

	# Object Status
	Creating = 0
	Created = 1
	Destroying = 2
	Destroyed = 3
	Disposing = 4
	Disposed = 5
	Failed = 6

	ConfigLoading = 100
	ConfigLoaded = 101
	ConfigLoadingFailed = 102

	InvalidProvider = 1000
	InvalidProviderAuthKey = 1001
	ProviderAlreadyExists = 1002
	CreatingProvider = 1003
	CreatedProvider = 1004
	ClientAddedToProvider = 1005

	ClientConnectionComplete = 2000
	ClientSendingProviders = 2001
	ClientAddedSenderVideoTrack = 2002
	ClientAddedSenderAudioTrack = 2003
	ClientAddedToBufferedProvider = 2004
	ClientSessionJoined = 2005
	ClientAddingTransceivers = 2006
	ClientAddedTransceivers = 2007
	ClientAddingTrackFromOther = 2008
	ClientSignalRenegotiation = 2009
	ClientOnTrackCalled = 2010

	RemoteClientAdded = 3000

	ProviderConfigReadDefaultStart = 4000
	ProviderConfigReadDefaultEnd = 4001

	RemoteProviderConnectionStarted = 5000
	RemoteProviderConnectionFailed = 5001
	RemoteProviderConnectionConnecting = 5002
	RemoteProviderConnectionSuccess = 5003
	RemoteProviderConnectForwardingStarted = 5004
	RemoteProviderConnectForwardingFailed = 5005
	RemoteProviderConnectForwardingConnecting = 5006
	RemoteProviderConnectForwardingSuccess = 5007
	RemoteProviderAddVirtualClient = 5008

	FrameSending = 6000
	FrameFullySent = 6001
	FrameFirstPacketRecv = 6002
	FrameFullyRecv = 6003

	ReceivedWSMessage = 7000

	SFUReceivedOffer = 8000
	SFUClientConnectionChange = 8001

	CriticalFail = 9999


__all__ = ["NetworkEnum"]