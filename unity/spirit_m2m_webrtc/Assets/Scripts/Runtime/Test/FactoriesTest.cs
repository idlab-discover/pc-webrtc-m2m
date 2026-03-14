using System.Collections;
using System.Collections.Generic;

using UnityEngine;

public class FactoriesTest : MonoBehaviour
{
    public string PlayerControllerName = "simple";
    private FoVDetectionTest fovDetectionTest;
    // Start is called before the first frame update
    PrefabFactory playerFactory;
    void Start()
    {
        fovDetectionTest = gameObject.GetComponent<FoVDetectionTest>();
        playerFactory = LoadFactories.GetFactory<PlayerControllerBase>();
        Debug.Log("Factory loaded: " + (playerFactory != null ? "Success" : "Failed"));
        if(playerFactory != null)
        {
            GameObject go = playerFactory.InstantiatePrefab<PlayerControllerBase>(PlayerControllerName);
            if (go == null)
            {
                Debug.LogError("Failed");
                return;
            }
            fovDetectionTest?.Init(go.GetComponent<PlayerControllerBase>());
        }

    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
