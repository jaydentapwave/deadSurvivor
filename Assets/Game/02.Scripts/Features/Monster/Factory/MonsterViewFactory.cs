using System.Collections.Generic;
using UnityEngine;

// 몬스터 그림을 풀에서 꺼내 몸 그림을 넣고 등록부에 올린다. 돌려줄 때는 등록부에서 빼고 풀로.
// 몸은 데이터 그림 키가 있으면 아틀라스 그림(그림 넣기 기획서 C·E절), 비었거나 그림이 없으면 F1 마름모(색·크기 = 데이터) — 종류마다 한 번 정해 캐시.
// 상태 표시(선·원)는 모두 같이 쓰는 흰 반투명 그림 하나씩을 처음 쓸 때 만든다.
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
    private readonly SpriteAtlasService _spriteAtlasService;
    private readonly RunRulesData _runRules;

    private readonly Dictionary<string, (Sprite sprite, bool hasArt)> _bodySpriteDict = new();
    private MonsterView _prefab;
    private Sprite _dashLineSprite;
    private Sprite _blastAreaSprite;

    public MonsterViewFactory(PoolManager poolManager, ResourceService resourceService, MonsterViewRegistry monsterViewRegistry,
        PlaceholderSpriteFactory placeholderSpriteFactory, SpriteAtlasService spriteAtlasService, RunRulesData runRules)
    {
        _poolManager = poolManager;
        _resourceService = resourceService;
        _monsterViewRegistry = monsterViewRegistry;
        _placeholderSpriteFactory = placeholderSpriteFactory;
        _spriteAtlasService = spriteAtlasService;
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
        var body = GetBodySprite(data);
        view.Initialize(body.sprite, body.hasArt, _dashLineSprite, _blastAreaSprite, dashLength, blastRadius, _runRules.UnitScale);
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

    // 그림: 키 이름 그대로(배율 1). 마름모: 지름 = 반지름 × 2(바닥 단위) × unitScale × PPU(F1 임시 적과 같은 식)
    private (Sprite sprite, bool hasArt) GetBodySprite(MonsterData data)
    {
        if (_bodySpriteDict.TryGetValue(data.Id, out var body))
        {
            return body;
        }

        var art = string.IsNullOrEmpty(data.Sprite) ? null : _spriteAtlasService.Get(data.Sprite);
        if (art != null)
        {
            body = (art, true);
        }
        else
        {
            var pixels = Mathf.Max(1, Mathf.RoundToInt(data.Radius * 2f * _runRules.UnitScale * Const.PixelsPerUnit));
            body = (_placeholderSpriteFactory.CreateDiamond(pixels, pixels, _placeholderSpriteFactory.ParseColor(data.Color), FootPivot),
                false);
        }

        _bodySpriteDict.Add(data.Id, body);
        return body;
    }
}
