using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;

public class PoolManager
{
    private readonly Dictionary<string, ObjectPool<Component>> _poolDict = new();

    // 꺼낼 때 켜기·부모·위치/회전/크기를 되돌린다. 그림별 값은 꺼낸 쪽(팩토리)이 넣는다.
    public T Get<T>(string key, T prefab, Transform parent) where T : Component
    {
        if (!_poolDict.TryGetValue(key, out var pool))
        {
            pool = new ObjectPool<Component>(() => Object.Instantiate(prefab), actionOnRelease: OnReleased);
            _poolDict.Add(key, pool);
        }

        var instance = (T)pool.Get();
        var instanceTransform = instance.transform;
        instanceTransform.SetParent(parent, false);
        instanceTransform.localPosition = Vector3.zero;
        instanceTransform.localRotation = Quaternion.identity;
        instanceTransform.localScale = prefab.transform.localScale;
        instance.gameObject.SetActive(true);
        return instance;
    }

    public void Release(string key, Component instance)
    {
        if (!_poolDict.TryGetValue(key, out var pool))
        {
            GameLogger.LogError($"[PoolManager] No pool for key {key}");
            return;
        }

        pool.Release(instance);
    }

    private static void OnReleased(Component instance) => instance.gameObject.SetActive(false);
}
