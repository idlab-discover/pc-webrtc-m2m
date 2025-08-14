using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "PrefabFactory", menuName = "Factories/Prefab Factory")]
public class PrefabFactory : ScriptableObject, ISerializationCallbackReceiver
{
    [Tooltip("List of all registered prefabs.")]
    public Dictionary<string, GameObject> registeredPrefabs = new();

    // Serialized lists for Unity to store on disk
    [SerializeField, HideInInspector]
    private List<string> serializedKeys = new();
    [SerializeField, HideInInspector]
    private List<GameObject> serializedValues = new();

    /// <summary>
    /// Returns the prefab with the given name, or null if not found.
    /// </summary>
    public GameObject GetByName(string key)
    {
        return registeredPrefabs.GetValueOrDefault(key);
    }

    /// <summary>
    /// Returns all registered prefab names. 
    /// </summary>
    public List<string> GetAllNames()
    {
        List<string> names = new List<string>();
        foreach (var prefab in registeredPrefabs)
        {
            if (prefab.Value != null)
                names.Add(prefab.Value.name);
        }
        return names;
    }

    // Called before Unity serializes this object
    public void OnBeforeSerialize()
    {
        serializedKeys.Clear();
        serializedValues.Clear();

        foreach (var kvp in registeredPrefabs)
        {
            serializedKeys.Add(kvp.Key);
            serializedValues.Add(kvp.Value);
        }
    }

    // Called after Unity deserializes this object
    public void OnAfterDeserialize()
    {
        registeredPrefabs.Clear();

        int count = Math.Min(serializedKeys.Count, serializedValues.Count);
        for (int i = 0; i < count; i++)
        {
            registeredPrefabs[serializedKeys[i]] = serializedValues[i];
        }
    }
}
