using UnityEngine;

// 피해 숫자를 풀에서 꺼내 글·색·크기·자리를 넣고 등록부 끝(가장 새것)에 올린다. 돌려줄 때는 등록부에서 빼고 풀로.
// 프리팹·부모는 장면 LifetimeScope가 넘긴다(EnemyShotViewFactory와 같은 방식).
// 숫자 0~9 아틀라스 그림은 scope에서 캐시하고 풀 인스턴스가 함께 쓴다.
public class DamageNumberViewFactory
{
    private const string PoolKey = "DamageNumberView";
    private readonly PoolManager _poolManager;
    private readonly DamageNumberViewRegistry _damageNumberViewRegistry;
    private readonly DamageNumberView _prefab;
    private readonly Transform _parent;
    private readonly Color _textColor;
    private readonly float _fontSize;

    private readonly Sprite[] _digits = new Sprite[10];

    public DamageNumberViewFactory(PoolManager poolManager, PlaceholderSpriteFactory placeholderSpriteFactory,
        SpriteAtlasService spriteAtlasService, FeedbackRulesRepository feedbackRulesRepository, DamageNumberViewRegistry damageNumberViewRegistry, DamageNumberView prefab,
        Transform parent)
    {
        _poolManager = poolManager;
        _damageNumberViewRegistry = damageNumberViewRegistry;
        _prefab = prefab;
        _parent = parent;
        var rules = feedbackRulesRepository.Rules.DamageNumber;
        _textColor = placeholderSpriteFactory.ParseColor(rules.TextColor);
        _fontSize = rules.FontSizePx;
        for (var i = 0; i < _digits.Length; i++)
        {
            _digits[i] = spriteAtlasService.Get($"digit_{i}");
        }
    }

    public void Create(int value, Vector3 startPosition, Vector3 anchorPosition)
    {
        var view = _poolManager.Get(PoolKey, _prefab, _parent);
        view.Initialize(value, _textColor, _fontSize, _digits);
        view.UpdateView(startPosition, 1f);
        _damageNumberViewRegistry.Register(new DamageNumberEntry(view, startPosition, anchorPosition));
    }

    // index = 등록부 안 위치(0 = 가장 오래된 것)
    public void Release(int index)
    {
        var view = _damageNumberViewRegistry.EntryList[index].View;
        _damageNumberViewRegistry.Unregister(index);
        _poolManager.Release(PoolKey, view);
    }
}
