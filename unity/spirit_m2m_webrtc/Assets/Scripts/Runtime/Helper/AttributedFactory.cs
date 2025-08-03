
using System;
using System.Collections;
using System.Collections.Generic;
using System.Data.SqlTypes;
using System.Linq;
using System.Reflection;

using UnityEngine;

public class AttributedFactory<T, K> where T : RegisterAttribute where K : class
{
    private readonly Dictionary<string, ConstructorInfo> constructors = new();

    public AttributedFactory()
    {
        registerAll();
    }

    private void registerAll()
    {
        var baseType = typeof(K);
        var types = AppDomain.CurrentDomain.GetAssemblies()
            .SelectMany(x => x.GetTypes())
            .Where(x => baseType.IsAssignableFrom(x) && !x.IsAbstract && x.GetCustomAttribute<T>() != null);
        foreach (var type in types)
        {
            var attr = type.GetCustomAttribute<T>();
            var ctor = type.GetConstructors().First();
            constructors[attr.Key] = ctor;
        }
    }

    public K Create(string type, params object[] args)
    {
        bool succes = constructors.TryGetValue(type, out var constructor);
        if (!succes)
        {
            return null;
        }
        return (K)constructor.Invoke(args);

    }
}