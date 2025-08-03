using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class JoinSessionMessage 
{
    public uint preferredClientID;
    public List<ConnectionProviderMessage> providers = new();
}
