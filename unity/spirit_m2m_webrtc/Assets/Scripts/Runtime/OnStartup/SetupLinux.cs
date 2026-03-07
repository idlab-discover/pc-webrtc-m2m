using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public static class SetupLinux 
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Initialize()
    {
        Debug.Log("Setup for Linux...");
       
    }
}
