#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

// This part runs once when Unity loads or recompiles
[InitializeOnLoad]
public static class PrefabFactoryInitializer
{
    static PrefabFactoryInitializer()
    {
        // Delay until editor is ready
        EditorApplication.update += OnEditorLoaded;
    }

    private static void OnEditorLoaded()
    {
        EditorApplication.update -= OnEditorLoaded;
        PipelineLocalPrefabBuilder.Rebuild("Editor load rebuild");
        PipelineRemotePrefabBuilder.Rebuild("Editor load rebuild");
    }
}

// This part monitors prefab creation/deletion/movement
public class PrefabFactoryAssetWatcher: AssetPostprocessor
{
    // TODO Optimize: Use a more efficient way to track changes, like a hash set
    static void OnPostprocessAllAssets(
        string[] importedAssets,
        string[] deletedAssets,
        string[] movedAssets,
        string[] movedFromAssetPaths)
    {
        bool prefabChanged = false;

        foreach (string path in importedAssets)
            if (path.EndsWith(".prefab")) prefabChanged = true;

        foreach (string path in deletedAssets)
            if (path.EndsWith(".prefab")) prefabChanged = true;

        foreach (string path in movedAssets)
            if (path.EndsWith(".prefab")) prefabChanged = true;

        if (prefabChanged)
        {
            PipelineLocalPrefabBuilder.Rebuild("Asset watcher rebuild");
            PipelineRemotePrefabBuilder.Rebuild("Asset watcher rebuild");
        }
            
    }
}

// This part does the actual rebuild
public class PrefabFactoryAutoBuilder<TBaseComponent, TAttribute>
    where TBaseComponent : Component
        where TAttribute : RegisterAttribute
{
    public static void RebuildFactory2(string reason = "Manual trigger")

    {
        AssetDatabase.SaveAssets();
    }
    public static void RebuildFactory(string factoryPath, string reason = "Manual trigger")
        
    {
        var typesWithAttribute = GetTypesWithAttribute();
        Debug.Log($"{typesWithAttribute.Count} types with {typeof(TAttribute).Name} found.");

        /*List<string> names = typesWithAttribute
            .Select(type =>
            {
                Debug.Log($"[PrefabFactory] Found type with RegisterAttribute: {type.Value.FullName}");
                return type.Value.FullName;
            })
            .ToList();
        */
        // Replace the selected fragment with the following code:
        Dictionary<string, string> prefabGuidsByKey = typesWithAttribute
            .ToDictionary(
                pair => pair.Key,
                pair => AssetDatabase.FindAssets($"t:Prefab {pair.Value}Prefab").FirstOrDefault()
            );


        Dictionary<string, GameObject> foundPrefabs = new();
        foreach (var guid in prefabGuidsByKey)
        {
            if (guid.Value == null)
            {
                Debug.LogWarning($"[PrefabFactory] No prefab found for type {guid.Key} ({guid.Value})");
                continue;
            }
            string path = AssetDatabase.GUIDToAssetPath(guid.Value);
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);

            if (prefab != null && prefab.GetComponent<TBaseComponent>() != null)
            {
                Debug.Log(guid.Key);
                foundPrefabs[guid.Key] = prefab;
            }
        }

        var factory = AssetDatabase.LoadAssetAtPath<PrefabFactory>(factoryPath);
        if (factory == null)
        {
            factory = ScriptableObject.CreateInstance<PrefabFactory>();
            AssetDatabase.CreateAsset(factory, factoryPath);
            Debug.Log("[PrefabFactory] Created new factory asset.");
        }
         
        factory.registeredPrefabs = foundPrefabs; // Interface-based to support multiple factory types*/
        EditorUtility.SetDirty(factory);
        AssetDatabase.SaveAssets();

        Debug.Log($"[PrefabFactory] Auto-rebuilt from {reason}: {factory.registeredPrefabs.Count} prefabs registered.");
    }  

    private static Dictionary<string, string> GetTypesWithAttribute()
    {
        return AppDomain.CurrentDomain.GetAssemblies()
            .SelectMany(asm => asm.GetTypes())
            .Where(t => t.IsClass && !t.IsAbstract && t.GetCustomAttribute<TAttribute>() != null)
            .ToDictionary(
                t =>
                {
                    var attr = t.GetCustomAttribute<TAttribute>();
                    return attr.Key;
                },
                t => t.FullName
            );
    }
}
#endif


public static class PipelineLocalPrefabBuilder 
{
    public readonly static string FactoryPath = "Assets/PipelineLocalPrefabFactory.asset"; // TODO Write this to a path scriptable object

    [MenuItem("Tools/Prefab Factory/Rebuild Pipeline Local Factory")]
    public static void RebuildMenu()
    {
        Rebuild("Manual rebuild from menu");
    }
    public static void Rebuild(string reason)
    {
        PrefabFactoryAutoBuilder<PipelineLocalPointcloudBase, PipelineLocalRegisterAttribute>.RebuildFactory(
            FactoryPath,
            reason
        );
    }
}

public static class PipelineRemotePrefabBuilder
{
    public readonly static string FactoryPath = "Assets/PipelineRemotePrefabFactory.asset"; // TODO Write this to a path scriptable object

    [MenuItem("Tools/Prefab Factory/Rebuild Pipeline Remote Factory")]
    public static void RebuildMenu()
    {
        Rebuild("Manual rebuild from menu");
    }
    public static void Rebuild(string reason)
    {
        PrefabFactoryAutoBuilder<PipelineRemotePointcloudBase, PipelineRemoteRegisterAttribute>.RebuildFactory(
            FactoryPath,
            reason
        );
    }
}