using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SelfProviderManager 
{
    public string DefaultProvider;
    private Dictionary<(uint, uint), string> tracksToProvider = new(); // Has mapping from capturerID+descriptionID to provider

    public string GetProviderForTrack(uint capturerID, uint descriptionID)
    {
        if(tracksToProvider.TryGetValue((capturerID, descriptionID), out var provider))
        {
            return provider;
        }
        return DefaultProvider;
    }

    public void AddPreferredProvider(uint capturerID, uint descriptionID, string provider)
    {
        tracksToProvider[(capturerID, descriptionID)] = provider;
    }
}
