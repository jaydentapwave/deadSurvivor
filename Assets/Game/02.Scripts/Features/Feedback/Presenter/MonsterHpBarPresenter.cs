using System;
using System.Collections.Generic;
using R3;
using UnityEngine;
using VContainer.Unity;

// 강한 적·공격 중인 적만 HP 막대(F6 기획서 E절). 프레임 끝에 산 몬스터마다 조건을 보고, 상한 안에서 보일 막대를 만들고 나머지는 풀로.
// 몬스터 View(F4 MonsterView)는 고치지 않는다. 전투를 벗어나면 모두 거둔다.
public class MonsterHpBarPresenter : IInitializable, ILateTickable, IDisposable
{
    private readonly RunModel _runModel;
    private readonly MonsterModel _monsterModel;
    private readonly PartyModel _partyModel;
    private readonly MonsterHpRulesData _rules;
    private readonly RunRulesData _runRules;
    private readonly MonsterHpBarViewFactory _monsterHpBarViewFactory;
    private readonly MonsterHpBarViewRegistry _monsterHpBarViewRegistry;
    private readonly MonsterViewFactory _monsterViewFactory;
    private readonly CompositeDisposable _disposables = new();

    private readonly HashSet<MonsterBehavior> _strongBehaviorSet = new();
    // 몬스터 번호 → 마지막으로 공격 중이던 피드백 시각
    private readonly Dictionary<int, float> _lastAttackDict = new();
    private readonly List<MonsterHpBarCandidate> _candidateList = new(64);
    private readonly HashSet<int> _shownIdSet = new();
    private readonly HashSet<int> _aliveIdSet = new();
    private readonly List<int> _removeIdList = new();
    private float _clock;

    // 상한 때문에 못 보인 후보 수(지난 프레임)
    public int HiddenByCapCount { get; private set; }

    public MonsterHpBarPresenter(RunModel runModel, MonsterModel monsterModel, PartyModel partyModel,
        FeedbackRulesRepository feedbackRulesRepository, RunRulesData runRules, MonsterHpBarViewFactory monsterHpBarViewFactory,
        MonsterHpBarViewRegistry monsterHpBarViewRegistry, MonsterViewFactory monsterViewFactory)
    {
        _runModel = runModel;
        _monsterModel = monsterModel;
        _partyModel = partyModel;
        _rules = feedbackRulesRepository.Rules.MonsterHp;
        _runRules = runRules;
        _monsterHpBarViewFactory = monsterHpBarViewFactory;
        _monsterHpBarViewRegistry = monsterHpBarViewRegistry;
        _monsterViewFactory = monsterViewFactory;
    }

    public void Initialize()
    {
        foreach (var behaviorName in _rules.AlwaysShowBehaviors)
        {
            if (Enum.TryParse<MonsterBehavior>(behaviorName, true, out var behavior))
            {
                _strongBehaviorSet.Add(behavior);
            }
            else
            {
                GameLogger.LogError($"[MonsterHpBarPresenter] Unknown alwaysShowBehaviors {behaviorName}");
            }
        }

        _runModel.Phase.Where(phase => phase != RunPhase.Battle).Subscribe(_ => OnBattleLeft()).AddTo(_disposables);
        _runModel.RunCount.Skip(1).Subscribe(_ => OnRunStarted()).AddTo(_disposables);
    }

    public void Dispose() => _disposables.Dispose();

    // 강한 적 규칙으로 보이는 막대 수
    public int AlwaysShownCount
    {
        get
        {
            var count = 0;
            foreach (var monster in _monsterModel.MonsterList)
            {
                if (monster.IsAlive && _strongBehaviorSet.Contains(monster.Behavior) && _monsterHpBarViewRegistry.Contains(monster.Id))
                {
                    count++;
                }
            }

            return count;
        }
    }

    public void LateTick()
    {
        if (_runModel.Phase.CurrentValue != RunPhase.Battle)
        {
            return;
        }

        _clock += _runModel.IsPaused.CurrentValue ? 0f : Time.deltaTime;
        CollectCandidates(true);
        MonsterHpBarRule.SelectShown(_candidateList, _rules.MaxActive, _shownIdSet);
        HiddenByCapCount = _candidateList.Count - _shownIdSet.Count;
        ForgetRemovedMonsters();
        SyncViews();
    }

    // 지금 보이는데 조건 밖인 막대 수 + 조건 안이고 상한에 걸리지 않았는데 안 보이는 몬스터 수(판정 진입점, 상태를 바꾸지 않는다)
    public int CountRuleViolations()
    {
        if (_runModel.Phase.CurrentValue != RunPhase.Battle)
        {
            return _monsterHpBarViewRegistry.Count;
        }

        CollectCandidates(false);
        var violations = 0;
        var candidateIdSet = new HashSet<int>();
        foreach (var candidate in _candidateList)
        {
            candidateIdSet.Add(candidate.MonsterId);
        }

        foreach (var id in _monsterHpBarViewRegistry.MonsterIds)
        {
            if (!candidateIdSet.Contains(id))
            {
                violations++;
            }
        }

        if (_candidateList.Count <= _rules.MaxActive)
        {
            foreach (var id in candidateIdSet)
            {
                if (!_monsterHpBarViewRegistry.Contains(id))
                {
                    violations++;
                }
            }
        }

        return violations;
    }

    // 산 몬스터마다 강한 적·공격 중·머무름을 보고 후보 목록을 만든다. shouldRecordAttack이면 공격 중 시각을 적는다
    private void CollectCandidates(bool shouldRecordAttack)
    {
        _candidateList.Clear();
        _aliveIdSet.Clear();
        foreach (var monster in _monsterModel.MonsterList)
        {
            if (!monster.IsAlive)
            {
                continue;
            }

            _aliveIdSet.Add(monster.Id);
            var distance = GetNearestMemberDistance(monster.Position);
            var data = monster.Data;
            var isAttacking = distance < float.MaxValue && MonsterHpBarRule.IsAttacking(monster.Behavior, monster.Phase, distance,
                data.Radius + data.ContactGap, data.Behavior == MonsterBehavior.Projectile ? data.Projectile.Range : 0f);
            if (isAttacking && shouldRecordAttack)
            {
                _lastAttackDict[monster.Id] = _clock;
            }

            var lastAttack = _lastAttackDict.TryGetValue(monster.Id, out var time) ? time : float.NegativeInfinity;
            var isStrong = _strongBehaviorSet.Contains(monster.Behavior);
            if (MonsterHpBarRule.IsCandidate(isStrong, isAttacking, _clock, lastAttack, _rules.LingerSeconds))
            {
                _candidateList.Add(new MonsterHpBarCandidate(monster.Id, isStrong, distance));
            }
        }
    }

    // 산 대원이 없으면 float.MaxValue
    private float GetNearestMemberDistance(Vector2 position)
    {
        var nearest = float.MaxValue;
        foreach (var member in _partyModel.MemberList)
        {
            if (member.IsAlive)
            {
                nearest = Mathf.Min(nearest, Vector2.Distance(member.Position, position));
            }
        }

        return nearest;
    }

    private void ForgetRemovedMonsters()
    {
        _removeIdList.Clear();
        foreach (var id in _lastAttackDict.Keys)
        {
            if (!_aliveIdSet.Contains(id))
            {
                _removeIdList.Add(id);
            }
        }

        foreach (var id in _removeIdList)
        {
            _lastAttackDict.Remove(id);
        }
    }

    // 안 보일 막대는 풀로, 보일 몬스터는 막대를 만들거나 자리·채움을 맞춘다
    private void SyncViews()
    {
        _removeIdList.Clear();
        foreach (var id in _monsterHpBarViewRegistry.MonsterIds)
        {
            if (!_shownIdSet.Contains(id))
            {
                _removeIdList.Add(id);
            }
        }

        foreach (var id in _removeIdList)
        {
            _monsterHpBarViewFactory.Release(id);
        }

        foreach (var monster in _monsterModel.MonsterList)
        {
            if (!monster.IsAlive || !_shownIdSet.Contains(monster.Id))
            {
                continue;
            }

            if (!_monsterHpBarViewRegistry.TryGet(monster.Id, out var view))
            {
                view = _monsterHpBarViewFactory.Create(monster.Id);
            }

            view.UpdateView(GetBarPosition(monster), monster.Hp / monster.MaxHp);
        }
    }

    // 몬스터 머리 위: 위로 (몸 그림 높이 + offsetPx ÷ PPU). 몬스터 키가 24~51px로 달라 반지름 대신 그림 경계를 쓴다(20261117-screen-fixes T5)
    private Vector2 GetBarPosition(Monster monster)
    {
        var foot = IsoProjection.FloorToWorld(monster.Position, _runRules.UnitScale);
        return foot + Vector2.up * (_monsterViewFactory.GetHeadHeight(monster.Data) + _rules.OffsetPx / Const.PixelsPerUnit);
    }

    private void OnBattleLeft() => ReleaseAll();

    private void OnRunStarted() => ReleaseAll();

    // 막대를 모두 풀로, 마지막 공격 기록·상한 누락 수를 비운다
    private void ReleaseAll()
    {
        _removeIdList.Clear();
        _removeIdList.AddRange(_monsterHpBarViewRegistry.MonsterIds);
        foreach (var id in _removeIdList)
        {
            _monsterHpBarViewFactory.Release(id);
        }

        _lastAttackDict.Clear();
        HiddenByCapCount = 0;
    }
}
