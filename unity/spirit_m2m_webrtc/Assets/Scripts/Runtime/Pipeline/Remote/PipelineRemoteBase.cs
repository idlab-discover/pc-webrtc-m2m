using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public abstract class PipelineRemoteBase : MonoBehaviour
{

    protected abstract string NAME { get; }
    public RemoteConnectedClient RemoteClient { get; private set; }
    protected Dictionary<string, RemoteTrackInfo> tracks;
    public virtual void Init(SessionInfo sessionInfo, RemoteConnectedClient remoteClient)
    {
        RemoteClient = remoteClient;
        tracks = remoteClient.GetReceivingTracksDict();
    }
    

    // TODO Maybe later if needed to dynamically add/remove tracks
    private void onTrackAdded(RemoteTrackInfo remoteTrack)
    {

    }
    private void onTrackRemoved()
    {
    }

    void OnDestroy()
    {
        Logger.LogStatusWithMessage(NAME, Logger.Status.Destroying, $"clientID={RemoteClient.ClientID}");
        cleanup();
        Logger.LogStatusWithMessage(NAME, Logger.Status.Destroyed, $"clientID={RemoteClient.ClientID}");
    }
    protected virtual void cleanup()
    {

    }
}
