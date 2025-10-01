using Newtonsoft.Json;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Concurrent;
using UnityEngine;
using System.IO;

[System.Serializable]
public class ExternalWebRTCPortRange
{
    public uint start;
    public uint end;
}

public class ExternalWebRTCPorts 
{
    private List<ExternalWebRTCPortRange> portRanges = new();
    private ConcurrentDictionary<uint, bool> portUsage = new();
    private static ExternalWebRTCPorts instance = null;
    public static ExternalWebRTCPorts Instance { 
        get 
        {
            return instance;
        } 
    }
    private ExternalWebRTCPorts()
    {
    }
    public static void LoadConfig(string path, bool resetInstance = false)
    {
        if(instance != null && !resetInstance)
            return;

        instance = new ExternalWebRTCPorts();
        try
        {
            instance.portRanges = JsonConvert.DeserializeObject<List<ExternalWebRTCPortRange>>(File.ReadAllText(path));
            // Initialize portUsage dictionary
            foreach (var range in instance.portRanges)
            {
                for (uint port = range.start; port <= range.end; port++)
                {
                    instance.portUsage[port] = false;
                }
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"ExternalWebRTCPorts: Failed to parse config file at {path}. Exception: {e.Message}");
            return;
        }
    }

    // Returns an unused port and marks it as used
    public uint? AcquirePort()
    {
        foreach (var kvp in portUsage)
        {
            if (!kvp.Value)
            {
                if (portUsage.TryUpdate(kvp.Key, true, false))
                {
                    return kvp.Key;
                }
            }
        }
        return null; // No available port
    }

    // Returns a port to the pool (marks as unused)
    public void ReleasePort(uint port)
    {
        portUsage.TryUpdate(port, false, true);
    }
}
