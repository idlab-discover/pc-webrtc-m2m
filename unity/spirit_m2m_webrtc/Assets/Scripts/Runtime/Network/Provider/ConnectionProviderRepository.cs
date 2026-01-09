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
            var ctor = type.GetConstructor(new[] { typeof(LocalConnectedClient), typeof(ClientAddedToProviderMessage)});
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

    public static ConnectionProviderBase CreateProvider(LocalConnectedClient localClient, ClientAddedToProviderMessage pMsg)
    {
        Logger.LogStatusWithMessage(NAME, Logger.Status.FactoryCreate, $"type={pMsg.providerType} key={pMsg.providerKey} ip={pMsg.address} port={pMsg.port}");
        bool succes = constructors.TryGetValue(pMsg.providerType.ToLower(), out var constructor);
        if (!succes)
        {
            Logger.LogStatusWithMessage(NAME, Logger.Status.FactoryCreateFailed, $"type={pMsg.providerType} key={pMsg.providerKey}");
            return null;
        }
        ConnectionProviderBase provider = (ConnectionProviderBase)constructor.Invoke(new object[] { localClient, pMsg });
        Logger.LogStatusWithMessage(NAME, Logger.Status.FactoryCreateSucces, $"type={pMsg.providerType}  key={pMsg.providerKey}");
        providers.Add(pMsg.providerKey, provider);
        return provider;
    }
    public static ConnectionProviderBase GetAndCreateIfNotExists(LocalConnectedClient localClient, ClientAddedToProviderMessage pMsg)
    {
        lock (_lock)
        {
            if (providers.TryGetValue(pMsg.providerKey, out var provider))
            {
                return provider; 
            }
            return CreateProvider(localClient, pMsg);
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
