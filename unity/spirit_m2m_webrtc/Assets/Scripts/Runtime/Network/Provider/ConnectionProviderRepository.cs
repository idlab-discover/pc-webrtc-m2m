using Newtonsoft.Json.Linq;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

public static class ConnectionProviderRepository 
{
    private const string NAME = "ConnectionProviderRepository";
    private static readonly object _lock = new();
    private static readonly Dictionary<string, ConstructorInfo> constructors = new();
    private static Dictionary<string, ConnectionProviderBase> providers = new();
    static ConnectionProviderRepository()
    {
        registerAll();
    }
    private static void registerAll()
    {
        var baseType = typeof(ConnectionProviderBase);
        var types = AppDomain.CurrentDomain.GetAssemblies()
            .SelectMany(x => x.GetTypes())
            .Where(x => baseType.IsAssignableFrom(x) && ! x.IsAbstract && x.GetCustomAttribute<ConnectionProviderRegisterAttribute>() != null);
        foreach (var type in types)
        {
            var attr = type.GetCustomAttribute<ConnectionProviderRegisterAttribute>();
            var ctor = type.GetConstructor(new[] { typeof(string), typeof(string), typeof(uint), typeof(JObject)});
            if(ctor != null)
            {
                constructors[attr.Key.ToLower()] = ctor;
            }
            
        }
    }

    public static ConnectionProviderBase GetProvider(string key)
    {
        lock (_lock)
        {
            providers.TryGetValue(key, out var provider);
            return provider;
        }
    }

    public static ConnectionProviderBase CreateProvider(string type, string key, string ip, uint port, JObject jsonSettings)
    {
        Logger.LogStatusWithMessage(NAME, Logger.Status.FactoryCreate, $"type={type} key={key} ip={ip} port={port}");
        bool succes = constructors.TryGetValue(type.ToLower(), out var constructor);
        if (!succes)
        {
            Logger.LogStatusWithMessage(NAME, Logger.Status.FactoryCreateFailed, $"type={type} key={key}");
            return null;
        }
        ConnectionProviderBase provider = (ConnectionProviderBase)constructor.Invoke(new object[] { key, jsonSettings });
        Logger.LogStatusWithMessage(NAME, Logger.Status.FactoryCreateSucces, $"type={type} key={key}");
        providers.Add(key, provider);
        return provider;
    }
    public static ConnectionProviderBase GetAndCreateIfNotExists(string type, string key, string ip, uint port, JObject jsonSettings)
    {
        lock (_lock)
        {
            if (providers.TryGetValue(key, out var provider))
            {
                return provider; 
            }
            return CreateProvider(type, key, ip, port, jsonSettings);
        }
        
    }

    public static void RemoveProvider(string key)
    {
        lock ( _lock)
        {
            providers.Remove(key, out var provider);
            provider.Dispose();
        }
    }
}
