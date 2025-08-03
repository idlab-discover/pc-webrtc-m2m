using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

public static class SessionManagerRepository 
{
    private const string NAME = "SessionManagerRepository";
    private static readonly Dictionary<string, ConstructorInfo> constructors = new();
    public static SessionManagerBase ActiveManager;
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
            constructors[attr.Key.ToLower()] = ctor;
        }
    }


    public static SessionManagerBase CreateAndGetManager(string type, string configPath)
    {
        Logger.LogStatusWithMessage(NAME, Logger.Status.FactoryCreate, $"type={type} configPath={configPath}");
        bool succes = constructors.TryGetValue(type.ToLower(), out var constructor);
        if(!succes)
        {
            Logger.LogStatusWithMessage(NAME, Logger.Status.FactoryCreateFailed, $"type={type} configPath={configPath}");
            return null;
        }
        ActiveManager = (SessionManagerBase)constructor.Invoke(new object[] { configPath });
        Logger.LogStatusWithMessage(NAME, Logger.Status.FactoryCreateSucces, $"type={type} configPath={configPath}");
        return ActiveManager;
    }
}
