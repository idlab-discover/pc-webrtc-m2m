using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

public static class SessionManagerRepository 
{
    private static readonly Dictionary<string, ConstructorInfo> constructors = new();
    public static Dictionary<string, SessionManagerBase> Managers;
    static SessionManagerRepository()
    {
        registerAll();
    }
    private static void registerAll()
    {
        var baseType = typeof(SessionManagerBase);
        var types = AppDomain.CurrentDomain.GetAssemblies()
            .SelectMany(x => x.GetTypes())
            .Where(x => baseType.IsAssignableFrom(x) && ! x.IsAbstract && x.GetCustomAttribute<SessionManagerRegisterAttribute>() != null);
        foreach (var type in types)
        {
            var attr = type.GetCustomAttribute<SessionManagerRegisterAttribute>();
            var ctor = type.GetConstructor(new[] { typeof(string) });
            constructors[attr.Key] = ctor;
        }
    }


    public static SessionManagerBase CreateAndGetStreamer(string type, string key, string configPath)
    {
        if(Managers.ContainsKey(key))
        {
            return null; // TODO maybe just return the manager?
        }

        bool succes = constructors.TryGetValue(type, out var constructor);
        if(!succes)
        {
            return null;
        }
        SessionManagerBase manager = (SessionManagerBase)constructor.Invoke(new object[] { configPath });
        Managers.Add(key, manager);
        return manager;
    }
}
