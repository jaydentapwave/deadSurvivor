using System;
using System.Collections.Generic;
using R3;
using UnityEngine;
using VContainer.Unity;

// 로비(V1): 캐릭터 카드를 채우고 출발 버튼으로 새 판을 시작한다(판 설정의 지금 값으로)
public class LobbyPresenter : IInitializable, IDisposable
{
    private readonly RunModel _runModel;
    private readonly RunSettingsModel _runSettingsModel;
    private readonly RunService _runService;
    private readonly CharacterRepository _characterRepository;
    private readonly ItemRepository _itemRepository;
    private readonly PlaceholderSpriteFactory _placeholderSpriteFactory;
    private readonly UI_LobbyView _lobbyView;
    private readonly CompositeDisposable _disposables = new();

    public int CardCount { get; private set; }
    public bool CanDepart => _runModel.Phase.CurrentValue == RunPhase.Lobby && CardCount > 0 && _lobbyView.IsDepartInteractable;

    public LobbyPresenter(RunModel runModel, RunSettingsModel runSettingsModel, RunService runService,
        CharacterRepository characterRepository, ItemRepository itemRepository,
        PlaceholderSpriteFactory placeholderSpriteFactory, UI_LobbyView lobbyView)
    {
        _runModel = runModel;
        _runSettingsModel = runSettingsModel;
        _runService = runService;
        _characterRepository = characterRepository;
        _itemRepository = itemRepository;
        _placeholderSpriteFactory = placeholderSpriteFactory;
        _lobbyView = lobbyView;
    }

    public void Initialize()
    {
        var characterDataList = _characterRepository.CharacterDataList;
        if (characterDataList.Count > _lobbyView.CardSlotCount)
        {
            GameLogger.LogError($"[LobbyPresenter] characters {characterDataList.Count} > card slots {_lobbyView.CardSlotCount}");
        }

        var startItemNameList = new List<string>(characterDataList.Count);
        var colorList = new List<Color>(characterDataList.Count);
        foreach (var data in characterDataList)
        {
            startItemNameList.Add(_itemRepository.Get(data.StartItem).Name);
            colorList.Add(_placeholderSpriteFactory.ParseColor(data.Color));
        }

        _lobbyView.UpdateUI(characterDataList, startItemNameList, colorList);
        CardCount = Mathf.Min(characterDataList.Count, _lobbyView.CardSlotCount);
        _lobbyView.DepartButton.OnClickAsObservable().Subscribe(_ => OnDepartClicked()).AddTo(_disposables);
    }

    public void Dispose() => _disposables.Dispose();

    public void OnDepartClicked() => _runService.StartRun(_runSettingsModel.Seed, _runSettingsModel.EnemyCountScale);
}
