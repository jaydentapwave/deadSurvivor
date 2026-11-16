using UnityEngine;

// 몬스터 HP 막대를 풀에서 꺼내 데이터 크기·색의 막대 그림을 넣고 등록부에 올린다(MonsterViewFactory와 같은 방식).
// 프리팹·부모는 장면 LifetimeScope가 넘긴다. 흰 상자 그림은 처음 쓸 때 한 번 만든다.
public class MonsterHpBarViewFactory
{
    private const string PoolKey = "MonsterHpBarView";
    private static readonly Vector2 BarPivot = new(0f, 0.5f);

    private readonly PoolManager _poolManager;
    private readonly PlaceholderSpriteFactory _placeholderSpriteFactory;
    private readonly MonsterHpBarViewRegistry _monsterHpBarViewRegistry;
    private readonly MonsterHpRulesData _rules;
    private readonly MonsterHpBarView _prefab;
    private readonly Transform _parent;
    private readonly Color _backColor;
    private readonly Color _fillColor;
    private readonly Vector2 _barOffset;

    private Sprite _barSprite;

    public MonsterHpBarViewFactory(PoolManager poolManager, PlaceholderSpriteFactory placeholderSpriteFactory,
        FeedbackRulesRepository feedbackRulesRepository, MonsterHpBarViewRegistry monsterHpBarViewRegistry, MonsterHpBarView prefab,
        Transform parent)
    {
        _poolManager = poolManager;
        _placeholderSpriteFactory = placeholderSpriteFactory;
        _monsterHpBarViewRegistry = monsterHpBarViewRegistry;
        _rules = feedbackRulesRepository.Rules.MonsterHp;
        _prefab = prefab;
        _parent = parent;
        _backColor = placeholderSpriteFactory.ParseColor(_rules.BackColor);
        _fillColor = placeholderSpriteFactory.ParseColor(_rules.FillColor);
        // 막대 가운데가 뿌리(몬스터 머리 위 자리)에 오게 왼쪽 끝을 반 폭만큼 왼쪽에
        _barOffset = new Vector2(-_rules.WidthPx * 0.5f / Const.PixelsPerUnit, 0f);
    }

    public MonsterHpBarView Create(int monsterId)
    {
        if (_barSprite == null)
        {
            _barSprite = _placeholderSpriteFactory.CreateBox(_rules.WidthPx, _rules.HeightPx, Color.white, BarPivot);
        }

        var view = _poolManager.Get(PoolKey, _prefab, _parent);
        view.Initialize(_barSprite, _backColor, _fillColor, _barOffset);
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
