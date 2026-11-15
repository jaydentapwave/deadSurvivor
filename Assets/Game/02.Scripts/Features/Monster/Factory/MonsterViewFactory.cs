using System.Collections.Generic;
using UnityEngine;

// 몬스터 그림을 풀에서 꺼내 몬스터 데이터의 색·크기 그림을 넣고 등록부에 올린다. 돌려줄 때는 등록부에서 빼고 풀로.
// 몸통 마름모는 몬스터 종류마다, 상태 표시(선·원)는 모두 같이 쓰는 흰 반투명 그림 하나씩을 처음 쓸 때 만든다.
public class MonsterViewFactory
{
    private const string PoolKey = "MonsterView";
    private const string PrefabPath = "Prefab/MonsterView";
    private const int DashLinePixelLength = 4;
    private const int DashLinePixelThickness = 2;
    private const int BlastAreaPixels = 64;
    private static readonly Vector2 FootPivot = new(0.5f, 0f);
    private static readonly Vector2 LineStartPivot = new(0f, 0.5f);
    private static readonly Color DashLineColor = new(1f, 1f, 1f, 0.5f);
    private static readonly Color BlastAreaColor = new(1f, 1f, 1f, 0.3f);

    private readonly PoolManager _poolManager;
    private readonly ResourceService _resourceService;
    private readonly MonsterViewRegistry _monsterViewRegistry;
    private readonly PlaceholderSpriteFactory _placeholderSpriteFactory;
    private readonly RunRulesData _runRules;

    private readonly Dictionary<string, Sprite> _bodySpriteDict = new();
    private MonsterView _prefab;
    private Sprite _dashLineSprite;
    private Sprite _blastAreaSprite;

    public MonsterViewFactory(PoolManager poolManager, ResourceService resourceService, MonsterViewRegistry monsterViewRegistry,
        PlaceholderSpriteFactory placeholderSpriteFactory, RunRulesData runRules)
    {
        _poolManager = poolManager;
        _resourceService = resourceService;
        _monsterViewRegistry = monsterViewRegistry;
        _placeholderSpriteFactory = placeholderSpriteFactory;
        _runRules = runRules;
    }

    public MonsterView Create(Monster monster, Transform parent)
    {
        if (_prefab == null)
        {
            _prefab = _resourceService.Load<MonsterView>(PrefabPath);
            _dashLineSprite = _placeholderSpriteFactory.CreateBox(DashLinePixelLength, DashLinePixelThickness, DashLineColor, LineStartPivot);
            _blastAreaSprite = _placeholderSpriteFactory.CreateCircle(BlastAreaPixels, BlastAreaColor);
        }

        var data = monster.Data;
        var dashLength = data.Behavior == MonsterBehavior.Charge ? data.Charge.DashSpeed * data.Charge.DashSeconds : 0f;
        var blastRadius = data.Behavior == MonsterBehavior.Bomb ? data.Bomb.BlastRadius : 0f;
        var view = _poolManager.Get(PoolKey, _prefab, parent);
        view.Initialize(GetBodySprite(data), _dashLineSprite, _blastAreaSprite, dashLength, blastRadius, _runRules.UnitScale);
        _monsterViewRegistry.Register(monster.Id, view);
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

    // 지름 = 반지름 × 2(바닥 단위) × unitScale × PPU(F1 임시 적과 같은 식)
    private Sprite GetBodySprite(MonsterData data)
    {
        if (_bodySpriteDict.TryGetValue(data.Id, out var sprite))
        {
            return sprite;
        }

        var pixels = Mathf.Max(1, Mathf.RoundToInt(data.Radius * 2f * _runRules.UnitScale * Const.PixelsPerUnit));
        sprite = _placeholderSpriteFactory.CreateDiamond(pixels, pixels, _placeholderSpriteFactory.ParseColor(data.Color), FootPivot);
        _bodySpriteDict.Add(data.Id, sprite);
        return sprite;
    }
}
