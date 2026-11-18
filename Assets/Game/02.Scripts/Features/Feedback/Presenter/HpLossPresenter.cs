using System;
using System.Collections.Generic;
using R3;
using UnityEngine;
using VContainer.Unity;

// 원정대 피해 기록 → 머리 위·하단 HP 막대의 빨간 구간(F6 기획서 C절). 원정대 기록을 비우는 쪽은 여기뿐이다.
// 쓰러진 대원의 머리 위 막대는 F1이 숨기므로 빨간 구간도 숨긴다(하단 막대는 줄어들 때까지 보인다).
public class HpLossPresenter : IInitializable, ILateTickable, IDisposable
{
    private readonly SpriteAtlasService _spriteAtlasService;
    private readonly RunModel _runModel;
    private readonly PartyModel _partyModel;
    private readonly CombatFeedbackModel _combatFeedbackModel;
    private readonly HpLossRulesData _rules;
    private readonly PlaceholderSpriteFactory _placeholderSpriteFactory;
    private readonly IReadOnlyList<MemberView> _memberViewList;
    private readonly UI_BattleHudView _battleHudView;
    private readonly CompositeDisposable _disposables = new();

    private readonly List<HpLossTrail> _trailList = new();

    public HpLossPresenter(RunModel runModel, PartyModel partyModel, CombatFeedbackModel combatFeedbackModel,
        FeedbackRulesRepository feedbackRulesRepository, PlaceholderSpriteFactory placeholderSpriteFactory, SpriteAtlasService spriteAtlasService,
        IReadOnlyList<MemberView> memberViewList, UI_BattleHudView battleHudView)
    {
        _spriteAtlasService = spriteAtlasService;
        _runModel = runModel;
        _partyModel = partyModel;
        _combatFeedbackModel = combatFeedbackModel;
        _rules = feedbackRulesRepository.Rules.HpLoss;
        _placeholderSpriteFactory = placeholderSpriteFactory;
        _memberViewList = memberViewList;
        _battleHudView = battleHudView;
    }

    public void Initialize()
    {
        var color = _placeholderSpriteFactory.ParseColor(_rules.Color);
        foreach (var memberView in _memberViewList)
        {
            var barPixels = memberView.HpBarPixels;
            var sprite = _spriteAtlasService.GetUi("U7_bar_fill");
            memberView.HpLossBarView.Initialize(sprite, color, (Vector2)barPixels / Const.PixelsPerUnit);
            _trailList.Add(new HpLossTrail());
        }

        foreach (var statusView in _battleHudView.MemberStatusViews)
        {
            statusView.HpLossView.Initialize(color);
        }

        _runModel.Phase.Where(phase => phase != RunPhase.Battle).Subscribe(_ => OnBattleLeft()).AddTo(_disposables);
        _runModel.RunCount.Skip(1).Subscribe(_ => OnRunStarted()).AddTo(_disposables);
    }

    public void Dispose() => _disposables.Dispose();

    // 빨간 구간이 보이는 가장 앞 대원, 없으면 −1
    public int MemberWithHpLoss
    {
        get
        {
            for (var i = 0; i < _trailList.Count && i < _partyModel.MemberList.Count; i++)
            {
                if (_trailList[i].Length > 0f)
                {
                    return i;
                }
            }

            return -1;
        }
    }

    public void LateTick()
    {
        if (_runModel.Phase.CurrentValue != RunPhase.Battle)
        {
            return;
        }

        var memberList = _partyModel.MemberList;
        foreach (var hit in _combatFeedbackModel.MemberHitList)
        {
            var maxHp = memberList[hit.MemberIndex].MaxHp;
            _trailList[hit.MemberIndex].Hit(hit.HpBefore / maxHp, hit.HpAfter / maxHp, _rules.HoldSeconds);
        }

        _combatFeedbackModel.ClearMemberHits();
        var deltaSeconds = _runModel.IsPaused.CurrentValue ? 0f : Time.deltaTime;
        var count = Math.Min(memberList.Count, _trailList.Count);
        for (var i = 0; i < count; i++)
        {
            var member = memberList[i];
            var ratio = member.Hp / member.MaxHp;
            var trail = _trailList[i];
            trail.Tick(deltaSeconds, ratio, _rules.ShrinkSeconds);
            _memberViewList[i].HpLossBarView.UpdateUI(ratio, member.IsAlive ? trail.LossFrom : ratio);
            if (i < _battleHudView.MemberStatusViews.Count)
            {
                _battleHudView.MemberStatusViews[i].HpLossView.UpdateUI(ratio, trail.LossFrom);
            }
        }
    }

    // 빨간 구간 길이 비율(LossFrom − 지금 비율)
    public float GetLossRatio(int member) => _trailList[member].Length;

    private void OnBattleLeft()
    {
        _combatFeedbackModel.ClearMemberHits();
        ResetTrails();
    }

    private void OnRunStarted()
    {
        _combatFeedbackModel.ClearMemberHits();
        ResetTrails();
    }

    private void ResetTrails()
    {
        var memberList = _partyModel.MemberList;
        for (var i = 0; i < _trailList.Count; i++)
        {
            var ratio = i < memberList.Count ? memberList[i].Hp / memberList[i].MaxHp : 1f;
            _trailList[i].Reset(ratio);
            _memberViewList[i].HpLossBarView.UpdateUI(ratio, ratio);
            if (i < _battleHudView.MemberStatusViews.Count)
            {
                _battleHudView.MemberStatusViews[i].HpLossView.UpdateUI(ratio, ratio);
            }
        }
    }
}
