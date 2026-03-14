using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public abstract class PlayerControllerBase: MonoBehaviour
{
    protected abstract string NAME { get; }
    private LocalConnectedClient localClient;
    private float updatePositionInterval = 1.000f;
    private float currentPositionUpdateInterval = 0f;
    private bool isMovementEnabled = false;

    public delegate void PlayerControllerPositionUpdatedCallback(ClientPositionUpdate newPostion);

    public event PlayerControllerPositionUpdatedCallback OnPlayerControllerPositionUpdated;
    protected ClientPositionUpdate currentPositionUpdate = new ClientPositionUpdate
    {
        position = new float[3],
        projectionMatrix = new float[4, 4],
        worldToCameraMatrix = new float[4, 4]
    };

    // I think LateUpdate is better because this would make sure that the movement etc... was already applied
    protected virtual void LateUpdate()
    {
        
            currentPositionUpdateInterval += Time.deltaTime;
            if (currentPositionUpdateInterval >= updatePositionInterval)
            {
                currentPositionUpdateInterval = 0f;
                updatePositionMatrix();
                if(localClient != null)
                {
                    localClient.UpdatePositionMatrix(currentPositionUpdate);
                }
                OnPlayerControllerPositionUpdated?.Invoke(currentPositionUpdate);
            }
        
    }

    public void Init(LocalConnectedClient client /*TODO Controller settings*/)
    {
        Logger.LogStatusWithMessage(NAME, Logger.Status.Initializing, $"clientID={client.ClientID}");
        localClient = client;
    }
    protected abstract Vector3 Position { get; }
    protected abstract void updatePositionMatrix();
    public abstract Transform CameraTransform { get; }
    public abstract Transform ObjectTransform { get; }
}
