using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SpawnerFixedPosition : MonoBehaviour
{
    public float HeightOffset = 0.0f;
    public List<Transform> SpawnPoints;
    
    public bool InitPositionForClient(uint clientId, GameObject prefab)
    {
        if (SpawnPoints.Count == 0)
        {
            Debug.LogError("No spawn points defined for SpawnerFixedPosition.");
            return false;
        }
        if (SpawnPoints.Count <= clientId)
        {
            Debug.LogError($"Not enough spawn points for client ID {clientId}. Spawn points available: {SpawnPoints.Count}. Using fall back");
        }
        int spawnIndex = (int)(clientId % SpawnPoints.Count);
        Transform spawnPoint = SpawnPoints[spawnIndex];
        // Set the position and rotation of the prefab to match the spawn point
        prefab.transform.position = spawnPoint.position + Vector3.up * HeightOffset;
        prefab.transform.rotation = spawnPoint.rotation;
        return true;
    }
}
