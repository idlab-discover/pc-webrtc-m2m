using Newtonsoft.Json.Linq;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

public static class CodecModeRepository 
{
    private const string NAME = "CodecModeRepository";
    private static readonly object _lock = new();
    private static readonly Dictionary<string, ConstructorInfo> constructors = new();
    private static Dictionary<string, CodecModeBase> modes = new();
    static CodecModeRepository()
    {
        registerAll();
    }
    private static void registerAll()
    {
        var baseType = typeof(CodecModeBase);
        var types = AppDomain.CurrentDomain.GetAssemblies()
            .SelectMany(x => x.GetTypes())
            .Where(x => baseType.IsAssignableFrom(x) && !x.IsAbstract && x.GetCustomAttribute<CodecModeRegisterAttribute>() != null);
        foreach (var type in types)
        {
            var attr = type.GetCustomAttribute<CodecModeRegisterAttribute>();
            var ctor = type.GetConstructors().First();
            if (ctor != null)
            {
                constructors[attr.Key.ToLower()] = ctor;
            }

        }
    }

    public static CodecModeBase GetCodecMode(string key)
    {
        lock (_lock)
        {
            modes.TryGetValue(key, out var provider);
            return provider;
        }
    }

    public static CodecModeBase CreateCodecMode(string type)
    {
        Logger.LogStatusWithMessage(NAME, Logger.Status.FactoryCreate, $"type={type}");
        bool succes = constructors.TryGetValue(type.ToLower(), out var constructor);
        if (!succes)
        {
            Logger.LogStatusWithMessage(NAME, Logger.Status.FactoryCreateFailed, $"type={type}");
            return null;
        }
        CodecModeBase mode = (CodecModeBase)constructor.Invoke(new object[] { });
        Logger.LogStatusWithMessage(NAME, Logger.Status.FactoryCreateSucces, $"type={type}");
        modes.Add(type, mode);
        return mode;
    }
    public static CodecModeBase GetAndCreateIfNotExists(string type)
    {
        lock (_lock)
        {
            if (modes.TryGetValue(type, out var provider))
            {
                return provider;
            }
            return CreateCodecMode(type);
        }

    }

    public static void RemoveCodecMode(string type)
    {
        lock (_lock)
        {
            modes.Remove(type, out var mode);
            mode.Dispose();
        }
    }
}
