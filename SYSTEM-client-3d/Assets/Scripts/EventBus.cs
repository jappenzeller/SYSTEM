using System;
using System.Collections.Generic;

/// <summary>
/// Simple event bus for decoupling systems
/// </summary>
public static class EventBus
{
    private static Dictionary<Type, List<Delegate>> subscribers = new Dictionary<Type, List<Delegate>>();
    
    public static void Subscribe<T>(Action<T> handler)
    {
        var type = typeof(T);
        if (!subscribers.ContainsKey(type))
            subscribers[type] = new List<Delegate>();
        subscribers[type].Add(handler);
    }
    
    public static void Unsubscribe<T>(Action<T> handler)
    {
        var type = typeof(T);
        if (subscribers.ContainsKey(type))
            subscribers[type].Remove(handler);
    }
    
    public static void Publish<T>(T eventData)
    {
        var type = typeof(T);
        if (subscribers.ContainsKey(type))
        {
            foreach (var handler in subscribers[type])
            {
                (handler as Action<T>)?.Invoke(eventData);
            }
        }
    }
    
    public static void Clear()
    {
        subscribers.Clear();
    }
    
    public static void ClearType<T>()
    {
        var type = typeof(T);
        if (subscribers.ContainsKey(type))
            subscribers[type].Clear();
    }
}