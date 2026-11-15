using UnityEngine;

// 적 그림을 풀에서 꺼내 그림을 넣고 등록부에 올린다. 돌려줄 때는 등록부에서 빼고 풀로.
public class MonsterViewFactory
{
    private const string PoolKey = "MonsterView";
    private const string PrefabPath = "Prefab/MonsterView";

    private readonly PoolManager _poolManager;
    private readonly ResourceService _resourceService;
    private readonly MonsterViewRegistry _monsterViewRegistry;

    private MonsterView _prefab;

    public MonsterViewFactory(PoolManager poolManager, ResourceService resourceService, MonsterViewRegistry monsterViewRegistry)
    {
        _poolManager = poolManager;
        _resourceService = resourceService;
        _monsterViewRegistry = monsterViewRegistry;
    }

    public MonsterView Create(int monsterId, Sprite sprite, Transform parent)
    {
        if (_prefab == null)
        {
            _prefab = _resourceService.Load<MonsterView>(PrefabPath);
        }

        var view = _poolManager.Get(PoolKey, _prefab, parent);
        view.Initialize(sprite);
        _monsterViewRegistry.Register(monsterId, view);
        return view;
    }

    public void Release(int monsterId)
    {
        if (!_monsterViewRegistry.TryGet(monsterId, out var view))
        {
            GameLogger.LogWarning($"[MonsterViewFactory] No view for monster {monsterId}");
            return;
        }

        _monsterViewRegistry.Unregister(monsterId);
        _poolManager.Release(PoolKey, view);
    }
}
