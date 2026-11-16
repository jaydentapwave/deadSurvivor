using System;
using TMPro;
using UnityEngine;
using Object = UnityEngine.Object;

// 피해 숫자를 풀에서 꺼내 글·색·크기·자리를 넣고 등록부 끝(가장 새것)에 올린다. 돌려줄 때는 등록부에서 빼고 풀로.
// 프리팹·부모는 장면 LifetimeScope가 넘긴다(EnemyShotViewFactory와 같은 방식).
// 테두리 색은 글꼴 재질을 한 번 복사해 데이터 값으로 넣고 모든 숫자가 함께 쓴다(숫자마다 재질 사본을 만들지 않는다).
public class DamageNumberViewFactory : IDisposable
{
    private const string PoolKey = "DamageNumberView";
    // TextMeshPro 월드 글자 크기 1 = 0.1유닛 높이
    private const float FontSizePerUnit = 10f;

    private readonly PoolManager _poolManager;
    private readonly DamageNumberViewRegistry _damageNumberViewRegistry;
    private readonly DamageNumberView _prefab;
    private readonly Transform _parent;
    private readonly Color _textColor;
    private readonly Color _outlineColor;
    private readonly float _fontSize;

    private Material _material;

    public DamageNumberViewFactory(PoolManager poolManager, PlaceholderSpriteFactory placeholderSpriteFactory,
        FeedbackRulesRepository feedbackRulesRepository, DamageNumberViewRegistry damageNumberViewRegistry, DamageNumberView prefab,
        Transform parent)
    {
        _poolManager = poolManager;
        _damageNumberViewRegistry = damageNumberViewRegistry;
        _prefab = prefab;
        _parent = parent;
        var rules = feedbackRulesRepository.Rules.DamageNumber;
        _textColor = placeholderSpriteFactory.ParseColor(rules.TextColor);
        _outlineColor = placeholderSpriteFactory.ParseColor(rules.OutlineColor);
        _fontSize = rules.FontSizePx / Const.PixelsPerUnit * FontSizePerUnit;
    }

    public void Dispose()
    {
        if (_material != null)
        {
            Object.Destroy(_material);
        }
    }

    public void Create(int value, Vector2 startPosition)
    {
        if (_material == null)
        {
            _material = new Material(_prefab.FontMaterial);
            _material.SetColor(ShaderUtilities.ID_OutlineColor, _outlineColor);
        }

        var view = _poolManager.Get(PoolKey, _prefab, _parent);
        view.Initialize(value, _textColor, _fontSize, _material);
        view.UpdateView(startPosition, 1f);
        _damageNumberViewRegistry.Register(new DamageNumberEntry(view, startPosition));
    }

    // index = 등록부 안 위치(0 = 가장 오래된 것)
    public void Release(int index)
    {
        var view = _damageNumberViewRegistry.EntryList[index].View;
        _damageNumberViewRegistry.Unregister(index);
        _poolManager.Release(PoolKey, view);
    }
}
