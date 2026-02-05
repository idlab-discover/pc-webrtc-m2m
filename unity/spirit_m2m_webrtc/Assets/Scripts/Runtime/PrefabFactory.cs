using System;
using System.Collections.Generic;
using Unity.VisualScripting;

using UnityEngine;

[CreateAssetMenu(fileName = "PrefabFactory", menuName = "Factories/Prefab Factory")]
public class PrefabFactory : ScriptableObject, ISerializationCallbackReceiver
{
    [Tooltip("List of all registered prefabs.")]
    public Dictionary<string, GameObject> registeredPrefabs = new();

    public Type prefabType;
    [SerializeField] private string _prefabType;

    // Serialized lists for Unity to store on disk
    [SerializeField, HideInInspector]
    private List<string> serializedKeys = new();
    [SerializeField, HideInInspector]
    private List<GameObject> serializedValues = new();


    public GameObject InstantiatePrefab<T>(string prefabName) 
    {
        if(typeof(T) != prefabType)
        {
            Debug.LogError($"PrefabFactory of type {prefabType.Name} cannot instantiate prefab of type {typeof(T).Name}");
            return default;
        }
        registeredPrefabs.TryGetValue(prefabName, out var prefab);
        if (prefab == null)
        {
            Debug.LogError($"No prefab found for {prefabName}");
            return default;
        }
        if (prefab.GetComponent<T>() == null)
        {
            Debug.LogError($"Prefab for {prefabName} does not have a {prefabType.Name} component.");
            return default;
        }
        Debug.Log($"Using prefab for {prefabName}");
        GameObject temp = Instantiate(prefab, Vector3.zero, Quaternion.identity);
        if (temp == null)
        {
            Debug.LogError("Failed to instantiate prefab for " + prefabName);
            return default;
        }
        return temp;
    }


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

        _prefabType = prefabType?.AssemblyQualifiedName;
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

        if (!string.IsNullOrEmpty(_prefabType))
            prefabType = Type.GetType(_prefabType);
    }
}
