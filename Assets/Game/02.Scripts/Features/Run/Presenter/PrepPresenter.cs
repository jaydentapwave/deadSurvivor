using System;
using System.Collections.Generic;
using R3;
using UnityEngine;
using VContainer.Unity;

// 정비 화면(V3): 정비로 바뀌면 완료 웨이브와 원정대 자리를 채우고, 다음 웨이브 버튼으로 다음 웨이브를 연다
public class PrepPresenter : IInitializable, IDisposable
{
    private readonly RunModel _runModel;
    private readonly RunService _runService;
    private readonly PartyModel _partyModel;
    private readonly CombatModel _combatModel;
    private readonly CharacterRepository _characterRepository;
    private readonly UI_PrepView _prepView;
    private readonly CompositeDisposable _disposables = new();
    private readonly List<string> _memberLineList = new();

    public PrepPresenter(RunModel runModel, RunService runService, PartyModel partyModel, CombatModel combatModel,
        CharacterRepository characterRepository, UI_PrepView prepView)
    {
        _runModel = runModel;
        _runService = runService;
        _partyModel = partyModel;
        _combatModel = combatModel;
        _characterRepository = characterRepository;
        _prepView = prepView;
    }

    public void Initialize()
    {
        _runModel.Phase.Where(phase => phase == RunPhase.Prep).Subscribe(_ => OnPrepEntered()).AddTo(_disposables);
        _prepView.NextWaveButton.OnClickAsObservable().Subscribe(_ => OnNextWaveClicked()).AddTo(_disposables);
        // 부활 뒤 대원 자리 HP를 바로 다시 그린다(F3 E절)
        _combatModel.ReviveCount.Where(_ => _runModel.Phase.CurrentValue == RunPhase.Prep).Subscribe(_ => OnMemberRevived())
            .AddTo(_disposables);
    }

    public void Dispose() => _disposables.Dispose();

    public void OnNextWaveClicked() => _runService.NextWave();

    private void OnMemberRevived() => OnPrepEntered();

    private void OnPrepEntered()
    {
        _memberLineList.Clear();
        foreach (var member in _partyModel.MemberList)
        {
            var characterName = _characterRepository.Get(member.CharacterId).Name;
            var hpLine = member.IsAlive ? $"HP {Mathf.CeilToInt(member.Hp)}/{Mathf.CeilToInt(member.MaxHp)}" : "쓰러짐";
            _memberLineList.Add($"{characterName}  {hpLine}");
        }

        _prepView.UpdateUI(_runModel.CompletedWave.CurrentValue, _memberLineList);
    }
}
