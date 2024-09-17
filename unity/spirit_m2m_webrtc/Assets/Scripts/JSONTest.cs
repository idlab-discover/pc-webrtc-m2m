using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class JSONTest : MonoBehaviour
{
    // Start is called before the first frame update
    void Start()
    {
        var s = SessionInfo.CreateFromJSON(Application.dataPath + "/config/session_config.json");
        Debug.Log(s.startPositions[0].x);
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
