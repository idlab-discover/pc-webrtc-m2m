using UnityEngine;
using System.Collections.Generic;
using System;

public static class LoadFactories
{
    // Your Dictionary for easy access
    private static Dictionary<Type, PrefabFactory> factories = new();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Initialize()
    {
        Debug.Log("Loading Prefab Factories...");
        // 1. Load all PrefabFactory assets from Assets/Resources/PrefabFactories
        // Note: Do not include "Resources" or the file extension in the path
        PrefabFactory[] loadedFactories = Resources.LoadAll<PrefabFactory>("PrefabFactories");

        foreach (var factory in loadedFactories)
        {
            if (!factories.ContainsKey(factory.prefabType))
            {
                factories.Add(factory.prefabType, factory);
                Debug.Log($"Registered factory for: {factory.prefabType}");
            }
        }
    }

    public static PrefabFactory GetFactory<T>()
    {
        Type type = typeof(T);
        if (factories.TryGetValue(type, out PrefabFactory factory))
        {
            return factory;
        }
        else
        {
            Debug.LogWarning($"No factory found for type: {type}");
            return null;
        }
    }

}