using System;
using System.Collections.Generic;
using R3;
using UnityEngine;
using VContainer.Unity;

// 정비 화면 진형 판넬(V3, F2 기획서 F절). 정비에 들어올 때와 진형 버튼을 누를 때 UpdateUI 한 번.
public class FormationPresenter : IInitializable, IDisposable
{
    private readonly RunModel _runModel;
    private readonly FormationModel _formationModel;
    private readonly FormationService _formationService;
    private readonly FormationRepository _formationRepository;
    private readonly PartyModel _partyModel;
    private readonly CharacterRepository _characterRepository;
    private readonly UI_FormationView _formationView;
    private readonly SpriteAtlasService _spriteAtlasService;
    private readonly List<Sprite> _portraitList = new();
    private readonly CompositeDisposable _disposables = new();
    private readonly List<string> _memberNameList = new();
    private readonly List<bool> _memberAliveList = new();

    public FormationPresenter(RunModel runModel, FormationModel formationModel, FormationService formationService,
        FormationRepository formationRepository, PartyModel partyModel, CharacterRepository characterRepository,
        UI_FormationView formationView, SpriteAtlasService spriteAtlasService)
    {
        _runModel = runModel;
        _formationModel = formationModel;
        _formationService = formationService;
        _formationRepository = formationRepository;
        _partyModel = partyModel;
        _characterRepository = characterRepository;
        _formationView = formationView;
        _spriteAtlasService = spriteAtlasService;
    }

    public void Initialize()
    {
        var formationDataList = _formationRepository.FormationDataList;
        var buttons = _formationView.FormationButtons;
        if (formationDataList.Count > buttons.Count)
        {
            GameLogger.LogError($"[FormationPresenter] formations {formationDataList.Count} > buttons {buttons.Count}");
        }

        for (var i = 0; i < formationDataList.Count && i < buttons.Count; i++)
        {
            var id = formationDataList[i].Id;
            buttons[i].OnClickAsObservable().Subscribe(_ => OnFormationClicked(id)).AddTo(_disposables);
        }

        _runModel.Phase.Where(phase => phase == RunPhase.Prep).Subscribe(_ => OnPrepEntered()).AddTo(_disposables);
    }

    public void Dispose() => _disposables.Dispose();

    // 진형 버튼 처리(판정 진입점 PressFormation도 이 메서드를 부른다)
    public void OnFormationClicked(string id)
    {
        _formationService.SelectPending(id);
        UpdateView();
    }

    private void OnPrepEntered() => UpdateView();

    private void UpdateView()
    {
        _memberNameList.Clear();
        _memberAliveList.Clear();
        _portraitList.Clear();
        foreach (var member in _partyModel.MemberList)
        {
            _memberNameList.Add(_characterRepository.Get(member.CharacterId).Name);
            _memberAliveList.Add(member.IsAlive);
            _portraitList.Add(_spriteAtlasService.Get(_characterRepository.Get(member.CharacterId).Sprite + SpriteAtlasService.FrontSuffix));
        }

        var pendingId = _formationModel.PendingId.CurrentValue;
        _formationRepository.TryGet(pendingId, out var pending);
        _formationView.UpdateUI(_formationRepository.FormationDataList, pendingId, pending.Slots, _memberNameList,
            _memberAliveList, _portraitList);
    }
}
