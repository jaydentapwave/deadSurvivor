using UnityEngine;

// 몬스터 HP 막대를 풀에서 꺼내 데이터 크기·색의 막대 그림을 넣고 등록부에 올린다(MonsterViewFactory와 같은 방식).
// 프리팹·부모는 장면 LifetimeScope가 넘기고 UI 그림은 아틀라스에서 캐시한다.
public class MonsterHpBarViewFactory
{
    private const string PoolKey = "MonsterHpBarView";

    private readonly PoolManager _poolManager;
    private readonly SpriteAtlasService _spriteAtlasService;
    private readonly MonsterHpBarViewRegistry _monsterHpBarViewRegistry;
    private readonly MonsterHpRulesData _rules;
    private readonly MonsterHpBarView _prefab;
    private readonly Transform _parent;
    private readonly Color _backColor;
    private readonly Color _fillColor;
    private readonly Vector2 _barOffset;

    public MonsterHpBarViewFactory(PoolManager poolManager, PlaceholderSpriteFactory placeholderSpriteFactory,
        SpriteAtlasService spriteAtlasService, FeedbackRulesRepository feedbackRulesRepository, MonsterHpBarViewRegistry monsterHpBarViewRegistry, MonsterHpBarView prefab,
        Transform parent)
    {
        _poolManager = poolManager;
        _spriteAtlasService = spriteAtlasService;
        _monsterHpBarViewRegistry = monsterHpBarViewRegistry;
        _rules = feedbackRulesRepository.Rules.MonsterHp;
        _prefab = prefab;
        _parent = parent;
        _backColor = placeholderSpriteFactory.ParseColor(_rules.BackColor);
        _fillColor = new Color(164f / 255, 83f / 255, 74f / 255); // Art standard 3.13 B
        // 막대 가운데가 뿌리(몬스터 머리 위 자리)에 오게 왼쪽 끝을 반 폭만큼 왼쪽에
        _barOffset = new Vector2(-_rules.WidthPx * 0.5f / Const.PixelsPerUnit, 0f);
    }

    public MonsterHpBarView Create(int monsterId)
    {
        var view = _poolManager.Get(PoolKey, _prefab, _parent);
        view.Initialize(_spriteAtlasService.GetUi("U7_bar_track"), _spriteAtlasService.GetUi("U7_bar_fill"),
            _backColor, _fillColor, _barOffset, new Vector2(_rules.WidthPx, _rules.HeightPx) / Const.PixelsPerUnit);
        _monsterHpBarViewRegistry.Register(monsterId, view);
        return view;
    }

    public void Release(int monsterId)
    {
        if (!_monsterHpBarViewRegistry.TryGet(monsterId, out var view))
        {
            GameLogger.LogWarning($"[MonsterHpBarViewFactory] No bar for monster {monsterId}");
            return;
        }

        _monsterHpBarViewRegistry.Unregister(monsterId);
        _poolManager.Release(PoolKey, view);
    }
}
