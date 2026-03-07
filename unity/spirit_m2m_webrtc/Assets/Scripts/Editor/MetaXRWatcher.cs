using System.Collections;
using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
public class MetaXRWatcher : AssetPostprocessor
{
   static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets, string[] movedAssets, string[] movedFromAssetPaths)
    {
        foreach (string path in importedAssets)
        {
            if (path.Contains("MetaXR"))
            {
            Debug.Log(path);
                // The object was just created/imported!
                // Trigger your wrapping logic here.
            }
        }
    }
}
#endif 
