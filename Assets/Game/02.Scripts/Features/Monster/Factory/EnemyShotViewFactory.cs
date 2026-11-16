using System.Collections.Generic;
using UnityEngine;

// 적 투사체 그림을 풀에서 꺼내 색 그림을 넣고 등록부에 올린다(MonsterViewFactory와 같은 방식).
// 프리팹과 부모는 장면 LifetimeScope가 넘긴다. 작은 원 그림은 색마다 한 번 만든다.
public class EnemyShotViewFactory
{
    private const string PoolKey = "EnemyShotView";
    private const int ShotDiameterPixels = 6;

    private readonly PoolManager _poolManager;
    private readonly PlaceholderSpriteFactory _placeholderSpriteFactory;
    private readonly EnemyShotViewRegistry _enemyShotViewRegistry;
    private readonly EnemyShotView _prefab;
    private readonly Transform _parent;

    private readonly Dictionary<string, Sprite> _spriteDict = new();

    public EnemyShotViewFactory(PoolManager poolManager, PlaceholderSpriteFactory placeholderSpriteFactory,
        EnemyShotViewRegistry enemyShotViewRegistry, EnemyShotView prefab, Transform parent)
    {
        _poolManager = poolManager;
        _placeholderSpriteFactory = placeholderSpriteFactory;
        _enemyShotViewRegistry = enemyShotViewRegistry;
        _prefab = prefab;
        _parent = parent;
    }

    public EnemyShotView Create(EnemyShot shot)
    {
        if (!_spriteDict.TryGetValue(shot.Color, out var sprite))
        {
            sprite = _placeholderSpriteFactory.CreateCircle(ShotDiameterPixels, _placeholderSpriteFactory.ParseColor(shot.Color));
            _spriteDict.Add(shot.Color, sprite);
        }

        var view = _poolManager.Get(PoolKey, _prefab, _parent);
        view.Initialize(sprite);
        _enemyShotViewRegistry.Register(shot.Id, view);
        return view;
    }

    public void Release(int shotId)
    {
        if (!_enemyShotViewRegistry.TryGet(shotId, out var view))
        {
            GameLogger.LogWarning($"[EnemyShotViewFactory] No view for shot {shotId}");
            return;
        }

        _enemyShotViewRegistry.Unregister(shotId);
        _poolManager.Release(PoolKey, view);
    }
}
