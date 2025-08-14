using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public abstract class PipelineLocalBase : MonoBehaviour
{    
    public LocalConnectedClient LocalClient { get; private set; }
    public virtual void Init(SessionInfo sessionInfo, LocalConnectedClient localClient)
    {
        LocalClient = localClient;
    }
    void OnDestroy()
    {
        cleanup();    
    }
    protected virtual void cleanup()
    {

    }
}
