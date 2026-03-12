using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public abstract class PipelineLocalBase : MonoBehaviour
{
    protected abstract string NAME { get; }
    public LocalConnectedClient LocalClient { get; private set; }
    private PlayerControllerBase playerControllerBase;
    public virtual void Init(SessionInfo sessionInfo, LocalConnectedClient localClient)
    {
        LocalClient = localClient;
        if(sessionInfo.playerControllerName != null && sessionInfo.playerControllerName != "" )
        {
            GameObject temp = LoadFactories.GetFactory<PlayerControllerBase>()?.InstantiatePrefab<PlayerControllerBase>(sessionInfo.playerControllerName);
            // transform.SetParent(temp.transform); // TODO Maybe use this or maybe not. Need to make sure that we can also move self renderer when we move in the world
            playerControllerBase = temp?.GetComponent<PlayerControllerBase>();
            playerControllerBase.Init(LocalClient);
            temp.transform.SetParent(this.transform); // For now this is fine but we need to check if this will also be fine when there will be movement. With movement maybe inverting the parenting might be better
            temp.transform.localPosition = Vector3.zero;
            temp.transform.localRotation = Quaternion.identity;
            if(sessionInfo.recordPosition)
            {
                PositionRecorder rec = gameObject.AddComponent<PositionRecorder>();
                rec.InitRecording(sessionInfo.positionTrackerConfig, playerControllerBase.CameraTransform, playerControllerBase.ObjectTransform);
                rec.StartRecording();
            }
            if(sessionInfo.playbackPosition) 
            {
                PositionPlayback playback = gameObject.AddComponent<PositionPlayback>(); // Maybe instead of using this component we just force a specific playercontroller instead
                
                // Also maybe save playerControllerName in playbackConfig so we can be sure to use exactly the same?
                playback.InitPlayback(sessionInfo.positionPlaybackConfig, playerControllerBase.CameraTransform, playerControllerBase.ObjectTransform);
                playback.StartPlayback();
            }
        }
        // Check session if mic is enabled
        // If it is -> Add Audio Pipeline to this GameObject
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
