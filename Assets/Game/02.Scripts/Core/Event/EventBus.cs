using System;
using System.Collections.Generic;
using R3;

public delegate void GameEventHandler<T>(in T e) where T : struct;

public class EventBus
{
    private readonly Dictionary<Type, Delegate> _handlerDict = new();

    public IDisposable Subscribe<T>(GameEventHandler<T> handler) where T : struct
    {
        var type = typeof(T);
        _handlerDict.TryGetValue(type, out var existing);
        _handlerDict[type] = Delegate.Combine(existing, handler);
        return Disposable.Create(() => Unsubscribe(handler));
    }

    public void Publish<T>(in T e) where T : struct
    {
        if (_handlerDict.TryGetValue(typeof(T), out var handlers))
        {
            ((GameEventHandler<T>)handlers).Invoke(in e);
        }
    }

    private void Unsubscribe<T>(GameEventHandler<T> handler) where T : struct
    {
        var type = typeof(T);
        if (!_handlerDict.TryGetValue(type, out var existing))
        {
            return;
        }

        var remaining = Delegate.Remove(existing, handler);
        if (remaining == null)
        {
            _handlerDict.Remove(type);
        }
        else
        {
            _handlerDict[type] = remaining;
        }
    }
}
