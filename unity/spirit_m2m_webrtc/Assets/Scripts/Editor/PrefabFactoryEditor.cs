#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(PrefabFactory))]
public class PrefabFactoryEditor : Editor
{
    public override void OnInspectorGUI()
    {
        // Draw the default inspector (everything except the dictionary)
        DrawDefaultInspector();

        var factory = (PrefabFactory)target;

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Registered Prefabs", EditorStyles.boldLabel);

        if (factory.registeredPrefabs.Count == 0)
        {
            EditorGUILayout.LabelField("None (dictionary is empty)");
        }
        else
        {
            foreach (var kvp in factory.registeredPrefabs)
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField(kvp.Key, GUILayout.Width(150));
                EditorGUILayout.ObjectField(kvp.Value, typeof(GameObject), false);
                EditorGUILayout.EndHorizontal();
            }
        }
    }
}
#endif
