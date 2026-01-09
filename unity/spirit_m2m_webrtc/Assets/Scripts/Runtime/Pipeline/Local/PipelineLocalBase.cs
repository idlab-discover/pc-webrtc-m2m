using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public abstract class PipelineLocalBase : MonoBehaviour
{
    protected abstract string NAME { get; }
    public LocalConnectedClient LocalClient { get; private set; }
    public virtual void Init(SessionInfo sessionInfo, LocalConnectedClient localClient)
    {
        LocalClient = localClient;
    }
    void OnDestroy()
    {
        Logger.LogStatusWithMessage(NAME, Logger.Status.Destroying, $"clientID={LocalClient.ClientID}");
        cleanup();
        Logger.LogStatusWithMessage(NAME, Logger.Status.Destroyed, $"clientID={LocalClient.ClientID}");
    }
    protected virtual void cleanup()
    {

    }
}
