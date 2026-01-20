using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public interface IReceiverSupported
{
    public NetworkReceiverBase GetReceiver(RemoteTrackInfo track, uint clientID);
    public NetworkReceiverBase GetReceiverForTrackList(List<RemoteTrackInfo> tracks, uint clientID);
}
