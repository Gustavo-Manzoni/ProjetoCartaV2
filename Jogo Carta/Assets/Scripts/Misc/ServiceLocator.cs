using System;
using System.Collections.Generic;
using UnityEngine.Rendering;

public static class ServiceLocator
{
    private static readonly Dictionary<Type, object> services = new();

    public static void Register<T>(T service)
    {
        var type = typeof(T);
        if (services.ContainsKey(type))
            services[type] = service;
        else
            services.Add(type, service);
    }

    public static T Get<T>()
    {
        var type = typeof(T);
        if (services.TryGetValue(type, out var service))
            return (T)service;

        return default; 
    }
}