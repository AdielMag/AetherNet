using System;

namespace AetherNet;

public sealed class AetherEventBus
{
    public const int MaxEventTypes = 64;
    public const int MaxListeners  = 32;

    private readonly Action<int>?[][] _listeners;
    private readonly int[]            _counts;

    public AetherEventBus()
    {
        _listeners = new Action<int>?[MaxEventTypes][];
        _counts    = new int[MaxEventTypes];
        for (int i = 0; i < MaxEventTypes; i++)
            _listeners[i] = new Action<int>?[MaxListeners];
    }

    public void Subscribe(int eventType, Action<int> handler)
    {
        if ((uint)eventType >= MaxEventTypes) return;
        int count = _counts[eventType];
        if (count >= MaxListeners) return;
        _listeners[eventType][count] = handler;
        _counts[eventType]++;
    }

    public void Unsubscribe(int eventType, Action<int> handler)
    {
        if ((uint)eventType >= MaxEventTypes) return;
        var list  = _listeners[eventType];
        int count = _counts[eventType];
        for (int i = 0; i < count; i++)
        {
            if (list[i] == handler)
            {
                list[i] = list[--_counts[eventType]];
                list[_counts[eventType]] = null;
                return;
            }
        }
    }

    public void Raise(int eventType, int entityId)
    {
        if ((uint)eventType >= MaxEventTypes) return;
        var list  = _listeners[eventType];
        int count = _counts[eventType];
        for (int i = 0; i < count; i++)
            list[i]?.Invoke(entityId);
    }
}
