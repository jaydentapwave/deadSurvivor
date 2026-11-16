using System;
using System.Collections.Generic;
using R3;
using UnityEngine;
using VContainer.Unity;

// 몬스터 피해 기록 → 피해 숫자(F6 기획서 B절). 모든 걸음이 끝난 뒤(LateTick) 이번 프레임 기록을 몬스터별로 합쳐 하나씩 내고 비운다.
// 몬스터 기록을 비우는 쪽은 여기뿐이다. 전투를 벗어나거나 새 판이 시작되면 모두 거둔다(A절 지우기).
public class DamageNumberPresenter : IInitializable, ILateTickable, IDisposable
{
    private readonly RunModel _runModel;
    private readonly CombatFeedbackModel _combatFeedbackModel;
    private readonly DamageNumberRulesData _rules;
    private readonly RunRulesData _runRules;
    private readonly DamageNumberViewFactory _damageNumberViewFactory;
    private readonly DamageNumberViewRegistry _damageNumberViewRegistry;
    private readonly CompositeDisposable _disposables = new();

    private readonly List<MonsterHitRecord> _mergedHitList = new(256);
    private readonly Dictionary<int, int> _mergeIndexDict = new(256);

    public DamageNumberPresenter(RunModel runModel, CombatFeedbackModel combatFeedbackModel,
        FeedbackRulesRepository feedbackRulesRepository, RunRulesData runRules, DamageNumberViewFactory damageNumberViewFactory,
        DamageNumberViewRegistry damageNumberViewRegistry)
    {
        _runModel = runModel;
        _combatFeedbackModel = combatFeedbackModel;
        _rules = feedbackRulesRepository.Rules.DamageNumber;
        _runRules = runRules;
        _damageNumberViewFactory = damageNumberViewFactory;
        _damageNumberViewRegistry = damageNumberViewRegistry;
    }

    public void Initialize()
    {
        _runModel.Phase.Where(phase => phase != RunPhase.Battle).Subscribe(_ => OnBattleLeft()).AddTo(_disposables);
        _runModel.RunCount.Skip(1).Subscribe(_ => OnRunStarted()).AddTo(_disposables);
    }

    public void Dispose() => _disposables.Dispose();

    public void LateTick()
    {
        if (_runModel.Phase.CurrentValue != RunPhase.Battle)
        {
            return;
        }

        var deltaSeconds = _runModel.IsPaused.CurrentValue ? 0f : Time.deltaTime;
        AgeNumbers(deltaSeconds);
        ShowNewHits();
        UpdateViews();
    }

    private void OnBattleLeft()
    {
        ReleaseAll();
        _combatFeedbackModel.ClearMonsterHits();
    }

    private void OnRunStarted()
    {
        ReleaseAll();
        _combatFeedbackModel.ClearMonsterHits();
        _combatFeedbackModel.ResetDamageNumberCounts();
    }

    // 나이를 더하고 수명이 끝난 숫자를 거둔다(뒤에서부터 빼야 앞 위치가 그대로다)
    private void AgeNumbers(float deltaSeconds)
    {
        var entryList = _damageNumberViewRegistry.EntryList;
        for (var i = entryList.Count - 1; i >= 0; i--)
        {
            entryList[i].Age += deltaSeconds;
            if (entryList[i].Age >= _rules.Lifetime)
            {
                _damageNumberViewFactory.Release(i);
            }
        }
    }

    // 같은 몬스터 기록을 합쳐 숫자 하나씩. 꽉 차면 가장 오래된 숫자를 거두고 새 숫자에 자리를 준다
    private void ShowNewHits()
    {
        DamageNumberMotion.Merge(_combatFeedbackModel.MonsterHitList, _mergedHitList, _mergeIndexDict);
        _combatFeedbackModel.ClearMonsterHits();
        if (_mergedHitList.Count == 0)
        {
            return;
        }

        foreach (var hit in _mergedHitList)
        {
            var evictCount = DamageNumberMotion.GetEvictCount(_damageNumberViewRegistry.Count, _rules.MaxActive);
            for (var i = 0; i < evictCount; i++)
            {
                _damageNumberViewFactory.Release(0);
            }

            _damageNumberViewFactory.Create(DamageNumberMotion.GetValue(hit.Amount), GetStartPosition(hit));
        }

        _combatFeedbackModel.AddDamageNumbersShown(_mergedHitList.Count, _damageNumberViewRegistry.Count);
    }

    private void UpdateViews()
    {
        foreach (var entry in _damageNumberViewRegistry.EntryList)
        {
            var rise = DamageNumberMotion.GetRisePx(entry.Age, _rules) / Const.PixelsPerUnit;
            entry.View.UpdateView(entry.StartPosition + Vector2.up * rise, DamageNumberMotion.GetAlpha(entry.Age, _rules));
        }
    }

    // 맞은 바닥 좌표의 월드 자리 + 위로 (몬스터 반지름 × unitScale + offsetPx ÷ PPU)(기획서 B절 식 그대로)
    private Vector2 GetStartPosition(MonsterHitRecord hit)
    {
        var foot = IsoProjection.FloorToWorld(hit.Position, _runRules.UnitScale);
        return foot + Vector2.up * (hit.Radius * _runRules.UnitScale + _rules.OffsetPx / Const.PixelsPerUnit);
    }

    private void ReleaseAll()
    {
        for (var i = _damageNumberViewRegistry.Count - 1; i >= 0; i--)
        {
            _damageNumberViewFactory.Release(i);
        }
    }
}
