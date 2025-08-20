using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PipelineRemoteBase : MonoBehaviour
{
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
        cleanup();
    }
    protected virtual void cleanup()
    {

    }
}
