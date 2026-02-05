using Newtonsoft.Json;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class ClientPositionUpdate
{
    public float[] position;
    public float[,] worldToCameraMatrix;
    public float[,] projectionMatrix;
    public string ConvertToJSON()
    {
        bool isValid = Validate();
        if (!isValid)
        {
            Debug.LogError("Cannot convert to JSON due to invalid data");
            return null;
        }
        return JsonConvert.SerializeObject(this);
    }
    public bool Validate()
    {
        if (position == null || position.Length != 3)
        {
            Debug.LogError("Invalid position data");
            return false;
        }
        bool worldToCameraValid = false;
        if(worldToCameraMatrix != null && worldToCameraMatrix.GetLength(0) == 4 && worldToCameraMatrix.GetLength(1) == 4)
        {
            worldToCameraValid = true;
            
        }
        if (!worldToCameraValid)
        {
            Debug.LogError("Invalid worldToCameraMatrix data");
            return false;
        }
        bool projectionMatrixValid = false;
        if(projectionMatrix != null && projectionMatrix.GetLength(0) == 4 && projectionMatrix.GetLength(1) == 4)
        {
            projectionMatrixValid = true;
        }
        if (!projectionMatrixValid)
        {
            Debug.LogError("Invalid projectionMatric data");
            return false;
        }
        return true;
    }
}
