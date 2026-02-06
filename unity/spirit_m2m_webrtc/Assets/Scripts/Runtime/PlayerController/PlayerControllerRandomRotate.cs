using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;

[PlayerControllerRegister("randomrot")]
public class PlayerControllerRandomRotate : PlayerControllerBase
{
    private enum RotationState
    {
        RandomRotate,
        ResetPosition,
        Waiting,
        Finished
    }
    protected override string NAME => "PlayerControllerRandomRotate";
    protected override Vector3 Position => transform.position;

    public override Transform CameraTransform => Camera.transform;

    public override Transform ObjectTransform => transform;

    public Camera Camera;
    
    private float rotationSpeed = 30f;
    private float targetRotationY;
    private float currentRotationY;
    private int minRotationY = -45;
    private int maxRotationY = 45;
    private int minRangeFromCenterY = 10;
    private int rotationDirection = 1; // 1 for clockwise, -1 for counterclockwise
    private float currentWaitTime = 0f;
    private float targetWaitTime = 2f; // seconds to wait after finishing rotation before next action
    private int minwaitTime = 2;
    private int maxwaitTime = 5;
    private System.Random rng = new();
    // -------------- Event ----------------
    // Either Random rotate or Reset position
    // Event = target position/rotation
    private RotationState currentRotationState = RotationState.Finished;
    private RotationState previousRotationState = RotationState.ResetPosition;

    // TODO: Currently, the rotations themselves will overshoot a bit (mainly because we dont clamp the rotation), should be fixed later if needed
    void Update()
    {
        if(currentRotationState == RotationState.Waiting)
        {
            currentWaitTime += Time.deltaTime;
            if(currentWaitTime >= targetWaitTime)
            {
                currentWaitTime = 0f;
                currentRotationState = RotationState.Finished;
            }
        }
        if(currentRotationState == RotationState.Finished)
        {
            
            if(previousRotationState == RotationState.RandomRotate)
            {
                currentRotationState = RotationState.ResetPosition;
                targetRotationY = -currentRotationY;
                rotationDirection = -rotationDirection; // reset in opposite direction for next time
            }
            else
            {
                int leftOrRight = rng.Next(0, 2);
                if (leftOrRight == 0) {
                    targetRotationY = rng.Next(minRotationY, 0 - minRangeFromCenterY);
                    rotationDirection = -1;
                } else
                {
                    targetRotationY = rng.Next(minRangeFromCenterY, maxRotationY);
                    rotationDirection = 1;
                }
                currentRotationState = RotationState.RandomRotate;
            }
            currentRotationY = 0;
        }
        if (currentRotationState == RotationState.RandomRotate || currentRotationState == RotationState.ResetPosition)
        {
            float step = rotationSpeed * Time.deltaTime * rotationDirection;
            Camera.transform.Rotate(0f, step, 0f, Space.Self);
            currentRotationY += step;
            if ((rotationDirection == 1 && currentRotationY >= targetRotationY) || (rotationDirection == -1 && currentRotationY <= targetRotationY))
            {
                previousRotationState = currentRotationState;
                currentRotationState = RotationState.Waiting;
                targetWaitTime = rng.Next(minwaitTime, maxwaitTime);
            }
        }
    }

    protected override void updatePositionMatrix()
    {
        currentPositionUpdate.position[0] = Position.x;
        currentPositionUpdate.position[1] = Position.y;
        currentPositionUpdate.position[2] = Position.z;
        Matrix4x4 worldToCamera = Camera.worldToCameraMatrix;
        Matrix4x4 projection = Camera.projectionMatrix;
        for (int i = 0; i < 4; i++)
        {
            for (int j = 0; j < 4; j++)
            {
                currentPositionUpdate.worldToCameraMatrix[i, j] = worldToCamera[i, j];
                currentPositionUpdate.projectionMatrix[i, j] = projection[i, j];
            }
        }
    }

}
