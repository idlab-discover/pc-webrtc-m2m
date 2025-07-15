using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

public static class NetworkStreamerRepository 
{
    private static readonly Dictionary<string, ConstructorInfo> constructors = new();
    public static Dictionary<string, NetworkStreamerBase> Streamers;
    static NetworkStreamerRepository()
    {
        registerAll();
    }
    private static void registerAll()
    {
        var baseType = typeof(NetworkStreamerBase);
        var types = AppDomain.CurrentDomain.GetAssemblies()
            .SelectMany(x => x.GetTypes())
            .Where(x => baseType.IsAssignableFrom(x) && ! x.IsAbstract && x.GetCustomAttribute<StreamerRegisterAttribute>() != null);
        foreach (var type in types)
        {
            var attr = type.GetCustomAttribute<StreamerRegisterAttribute>();
            var ctor = type.GetConstructors().First();
            constructors[attr.Key] = ctor;
        }
    }


    public static NetworkStreamerBase CreateAndGetStreamer(string type, string key, params object[] args)
    {
        if(Streamers.ContainsKey(key))
        {
            return null; // TODO maybe just return the streamer?
        }

        bool succes = constructors.TryGetValue(type, out var constructor);
        if(!succes)
        {
            return null;
        }
        NetworkStreamerBase streamer = (NetworkStreamerBase)constructor.Invoke(args);
        Streamers.Add(key, streamer);
        return streamer;
    }
}
