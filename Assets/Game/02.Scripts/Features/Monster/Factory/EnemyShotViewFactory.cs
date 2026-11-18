using System.Collections.Generic;
using UnityEngine;

// 적 탄의 데이터 그림 키로 프레임을 읽어 풀에서 재사용한다. 색 원을 만들지 않는다.
public class EnemyShotViewFactory
{
    private const string PoolKey = "EnemyShotView";

    private readonly PoolManager _poolManager;
    private readonly SpriteAtlasService _spriteAtlasService;
    private readonly CombatEffectData _effectData;
    private readonly EnemyShotViewRegistry _enemyShotViewRegistry;
    private readonly EnemyShotView _prefab;
    private readonly Transform _parent;

    private readonly Dictionary<string, Sprite[]> _spriteDict = new();

    public EnemyShotViewFactory(PoolManager poolManager, SpriteAtlasService spriteAtlasService, CombatEffectData effectData,
        EnemyShotViewRegistry enemyShotViewRegistry, EnemyShotView prefab, Transform parent)
    {
        _poolManager = poolManager;
        _spriteAtlasService = spriteAtlasService;
        _effectData = effectData;
        _enemyShotViewRegistry = enemyShotViewRegistry;
        _prefab = prefab;
        _parent = parent;
    }

    public EnemyShotView Create(EnemyShot shot)
    {
        var clip = _effectData.GetClip(shot.ShotSprite);
        if (!_spriteDict.TryGetValue(shot.ShotSprite, out var frames))
        {
            frames = new Sprite[clip.Frames];
            for (var i = 0; i < frames.Length; i++)
            {
                frames[i] = _spriteAtlasService.Get(clip.FrameKey(i));
            }
            _spriteDict.Add(shot.ShotSprite, frames);
        }

        var view = _poolManager.Get(PoolKey, _prefab, _parent);
        view.Initialize(frames, clip.Fps);
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
