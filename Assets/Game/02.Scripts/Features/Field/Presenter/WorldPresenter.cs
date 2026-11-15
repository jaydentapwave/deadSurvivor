using System;
using System.Collections.Generic;
using UnityEngine;
using VContainer.Unity;

// 바닥 평면 모델 → 월드 그림(기획서 E·F절): 바닥 깔기, 원정대·적 그림 위치, 카메라 중심, 보이는 바닥 영역을 생성기에 알림.
public class WorldPresenter : IInitializable, ILateTickable
{
    private static readonly Vector2 HpBarPivot = new(0f, 0.5f);
    private static readonly Vector2 FootPivot = new(0.5f, 0f);
    private static readonly Vector2 CenterPivot = new(0.5f, 0.5f);

    private readonly PartyModel _partyModel;
    private readonly MonsterModel _monsterModel;
    private readonly MonsterService _monsterService;
    private readonly CharacterRepository _characterRepository;
    private readonly RunRulesData _runRules;
    private readonly PlaceholderCombatData _combatData;
    private readonly PlaceholderSpriteFactory _placeholderSpriteFactory;
    private readonly MonsterViewFactory _monsterViewFactory;
    private readonly MonsterViewRegistry _monsterViewRegistry;
    private readonly FieldView _fieldView;
    private readonly Camera _camera;
    private readonly IReadOnlyList<MemberView> _memberViewList;

    private readonly HashSet<int> _aliveMonsterIdSet = new();
    private readonly List<int> _releaseIdList = new();
    private Sprite _monsterSprite;
    private Vector2 _viewHalfExtent;

    public WorldPresenter(PartyModel partyModel, MonsterModel monsterModel, MonsterService monsterService,
        CharacterRepository characterRepository, RunRulesData runRules, PlaceholderCombatData combatData,
        PlaceholderSpriteFactory placeholderSpriteFactory, MonsterViewFactory monsterViewFactory,
        MonsterViewRegistry monsterViewRegistry, FieldView fieldView, Camera camera, IReadOnlyList<MemberView> memberViewList)
    {
        _partyModel = partyModel;
        _monsterModel = monsterModel;
        _monsterService = monsterService;
        _characterRepository = characterRepository;
        _runRules = runRules;
        _combatData = combatData;
        _placeholderSpriteFactory = placeholderSpriteFactory;
        _monsterViewFactory = monsterViewFactory;
        _monsterViewRegistry = monsterViewRegistry;
        _fieldView = fieldView;
        _camera = camera;
        _memberViewList = memberViewList;
    }

    public void Initialize()
    {
        BuildField();
        InitializeMemberViews();
        var monsterPixels = Mathf.Max(1, Mathf.RoundToInt(_combatData.Enemy.Radius * 2f * _runRules.UnitScale * Const.PixelsPerUnit));
        _monsterSprite = _placeholderSpriteFactory.CreateDiamond(monsterPixels, monsterPixels,
            _placeholderSpriteFactory.ParseColor(_combatData.Enemy.Color), FootPivot);
        UpdateViewExtent();
    }

    public void LateTick()
    {
        UpdateViewExtent();
        var center = IsoProjection.FloorToWorld(_partyModel.Center, _runRules.UnitScale);
        _camera.transform.position = new Vector3(center.x, center.y, _camera.transform.position.z);
        UpdateMemberViews();
        SyncMonsterViews();
    }

    private void BuildField()
    {
        var cellSize = _fieldView.CellSize;
        var widthPixels = Mathf.RoundToInt(cellSize.x * Const.PixelsPerUnit);
        var heightPixels = Mathf.RoundToInt(cellSize.y * Const.PixelsPerUnit);
        var firstTile = _placeholderSpriteFactory.CreateTile(
            _placeholderSpriteFactory.CreateDiamond(widthPixels, heightPixels, _fieldView.FirstTileColor, CenterPivot));
        var secondTile = _placeholderSpriteFactory.CreateTile(
            _placeholderSpriteFactory.CreateDiamond(widthPixels, heightPixels, _fieldView.SecondTileColor, CenterPivot));
        _fieldView.Build(_runRules.FieldCells, firstTile, secondTile);
    }

    private void InitializeMemberViews()
    {
        var characterDataList = _characterRepository.CharacterDataList;
        if (characterDataList.Count > _memberViewList.Count)
        {
            GameLogger.LogError($"[WorldPresenter] characters {characterDataList.Count} > member views {_memberViewList.Count}");
        }

        for (var i = 0; i < _memberViewList.Count; i++)
        {
            var view = _memberViewList[i];
            view.SetVisible(false);
            if (i >= characterDataList.Count)
            {
                continue;
            }

            var bodySprite = _placeholderSpriteFactory.CreateCircle(view.BodyDiameterPixels,
                _placeholderSpriteFactory.ParseColor(characterDataList[i].Color));
            var hpBarSprite = _placeholderSpriteFactory.CreateBox(view.HpBarPixels.x, view.HpBarPixels.y, Color.white, HpBarPivot);
            view.Initialize(bodySprite, hpBarSprite);
        }
    }

    // 카메라가 보는 월드 영역 → 바닥 단위 반 크기. 바뀔 때만 생성기에 알린다
    private void UpdateViewExtent()
    {
        var halfHeight = _camera.orthographicSize;
        var worldHalfExtent = new Vector2(halfHeight * _camera.aspect, halfHeight);
        var floorHalfExtent = IsoProjection.WorldToFloor(worldHalfExtent, _runRules.UnitScale);
        if (floorHalfExtent == _viewHalfExtent)
        {
            return;
        }

        _viewHalfExtent = floorHalfExtent;
        _monsterService.SetViewHalfExtent(floorHalfExtent);
    }

    private void UpdateMemberViews()
    {
        var memberList = _partyModel.MemberList;
        var count = Math.Min(memberList.Count, _memberViewList.Count);
        for (var i = 0; i < _memberViewList.Count; i++)
        {
            var view = _memberViewList[i];
            if (i >= count)
            {
                view.SetVisible(false);
                continue;
            }

            var member = memberList[i];
            view.SetVisible(true);
            view.UpdateView(IsoProjection.FloorToWorld(member.Position, _runRules.UnitScale), member.Hp / member.MaxHp,
                member.IsAlive);
        }
    }

    // 모델의 산 적마다 그림 하나. 새 적은 풀에서 꺼내고 사라진 적의 그림은 풀로 돌린다
    private void SyncMonsterViews()
    {
        _aliveMonsterIdSet.Clear();
        foreach (var monster in _monsterModel.MonsterList)
        {
            if (!monster.IsAlive)
            {
                continue;
            }

            _aliveMonsterIdSet.Add(monster.Id);
            if (!_monsterViewRegistry.TryGet(monster.Id, out var view))
            {
                view = _monsterViewFactory.Create(monster.Id, _monsterSprite, null);
            }

            view.UpdateView(IsoProjection.FloorToWorld(monster.Position, _runRules.UnitScale));
        }

        _releaseIdList.Clear();
        foreach (var id in _monsterViewRegistry.MonsterIds)
        {
            if (!_aliveMonsterIdSet.Contains(id))
            {
                _releaseIdList.Add(id);
            }
        }

        foreach (var id in _releaseIdList)
        {
            _monsterViewFactory.Release(id);
        }
    }
}
