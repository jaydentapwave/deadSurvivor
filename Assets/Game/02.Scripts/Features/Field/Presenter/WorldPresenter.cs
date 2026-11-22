using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;
using VContainer.Unity;

// 바닥 평면 모델 → 월드 그림(기획서 E·F절): 바닥 깔기(풀 4장 섞기·흙길, 아트 기준서 3.2·3.3), 원정대·적 그림 위치, 카메라 중심, 보이는 바닥 영역을 생성기에 알림.
// 그림 넣기(기획서 E절): 대원·몬스터 그림 초기화, 프레임 이동량 → 방향·움직임, 대원 HP 감소 → 흰 번쩍임.
// 쓰러짐·예고(아트 기준서 3.10): 쓰러진 뒤 흐른 시간(어둡게 하기, 게임 시간 그대로 흘러 판이 끝나도 다 어두워진다), 몬스터 예고 중 좌우 1px 흔들림.
// 움직임 시계는 전투 중·일시정지 아님일 때만 흐르고, 멈춘 프레임에는 방향·움직임을 그대로 둔다(판 시작 리셋·쓰러진 대원 정리는 늘).
// 이동량은 시뮬레이션(30Hz 고정 걸음)이 진행된 프레임에만 본다. 걸음이 없는 그리기 프레임은 이동량이 0이라 멈춤으로 보면
// 걷는 중에도 대기·hop 오프셋이 프레임마다 깜빡이므로, 그 프레임은 지난 방향·이동 여부를 그대로 쓴다.
public class WorldPresenter : IInitializable, ILateTickable
{
    private static readonly Vector2 FootPivot = new(0.5f, 0f);
    private static readonly Matrix4x4 FlipMatrix = Matrix4x4.Scale(new Vector3(-1f, 1f, 1f));

    private readonly CombatModel _combatModel;
    private readonly MoveRulesData _moveRules;
    private readonly PartyModel _partyModel;
    private readonly MonsterModel _monsterModel;
    private readonly MonsterService _monsterService;
    private readonly PartyMoveService _partyMoveService;
    private readonly CharacterRepository _characterRepository;
    private readonly RunRulesData _runRules;
    private readonly ViewRulesData _viewRules;
    private readonly GroundPathData _groundPath;
    private readonly FieldLayoutData _fieldLayout;
    private readonly TerrainHeightGrid _terrain;
    private readonly PlaceholderCombatData _combatData;
    private readonly PlaceholderSpriteFactory _placeholderSpriteFactory;
    private readonly MonsterViewFactory _monsterViewFactory;
    private readonly MonsterViewRegistry _monsterViewRegistry;
    private readonly SpriteAtlasService _spriteAtlasService;
    private readonly RunModel _runModel;
    private readonly FieldView _fieldView;
    private readonly Camera _camera;
    private readonly TiltedCameraRig _rig;
    private readonly Vector2[] _lastMemberFloors;
    private readonly Dictionary<int, Vector2> _lastMonsterFloors = new();
    private readonly Dictionary<int, int> _placementVersionDict = new();
    private readonly List<string> _placementObservationRows = new();
    private bool _isRecordingPlacements;
    public string PlacementObservations => string.Join("\n", _placementObservationRows);
    public int SpawnFootVisibleCount { get; private set; }
    public int SpawnBodyVisibleCount { get; private set; }
    // north/east/west/south; independent of appearance colour or alpha.
    public int[] SpawnFeetByDirection { get; } = new int[4];
    public int[] SpawnBodiesByDirection { get; } = new int[4];
    public FloorViewBounds VisibleFloor { get; private set; }
    private readonly IReadOnlyList<MemberView> _memberViewList;

    private readonly HashSet<int> _aliveMonsterIdSet = new();
    private readonly List<int> _releaseIdList = new();
    private Vector2 _viewHalfExtent;
    private readonly float[] _lastHps;
    private readonly string[] _visibleCharacterIds;
    private readonly FirstExpeditionService _firstExpedition;
    private int _lastEncounterSteps;
    private int _waitingSlot = -1;
    private readonly HitFlash[] _hitFlashes;
    private float _motionClock;
    private float _lastRemainingSeconds;
    private readonly bool[] _isMemberMovings;
    private readonly float[] _downedSeconds;
    private readonly float[] _walkClocks;
    private readonly HashSet<int> _movingMonsterIdSet = new();
    private int _lastRunCount = -1;
    private int _idleOffsetSeenMask;

    // 판정 진입점용(이번 판): 번쩍임 시작 수, 끝난 번쩍임 한 번이 켜져 있던 최대 시간(없으면 −1), 대원 0이 지난 대기 오프셋 종류 수
    public int MemberHitFlashTotal
    {
        get; private set;
    }
    public float MemberFlashMaxSeconds { get; private set; } = -1f;
    public int IdleOffsetsSeen => (_idleOffsetSeenMask & 1) + ((_idleOffsetSeenMask >> 1) & 1);

    public WorldPresenter(CombatModel combatModel, MoveRulesData moveRules, PartyModel partyModel, MonsterModel monsterModel, MonsterService monsterService,
        PartyMoveService partyMoveService, CharacterRepository characterRepository, RunRulesData runRules, GroundPathData groundPath, FieldLayoutData fieldLayout, PlaceholderCombatData combatData,
        PlaceholderSpriteFactory placeholderSpriteFactory, MonsterViewFactory monsterViewFactory,
        MonsterViewRegistry monsterViewRegistry, SpriteAtlasService spriteAtlasService, RunModel runModel, FieldView fieldView,
        Camera camera, IReadOnlyList<MemberView> memberViewList, ViewRulesData viewRules, TiltedCameraRig rig, TerrainHeightGrid terrain, FirstExpeditionService firstExpedition)
    {
        _combatModel = combatModel;
        _moveRules = moveRules;
        _partyModel = partyModel;
        _monsterModel = monsterModel;
        _monsterService = monsterService;
        _partyMoveService = partyMoveService;
        _characterRepository = characterRepository;
        _runRules = runRules;
        _viewRules = viewRules;
        _groundPath = groundPath;
        _fieldLayout = fieldLayout;
        _terrain = terrain;
        _combatData = combatData;
        _placeholderSpriteFactory = placeholderSpriteFactory;
        _monsterViewFactory = monsterViewFactory;
        _monsterViewRegistry = monsterViewRegistry;
        _spriteAtlasService = spriteAtlasService;
        _runModel = runModel;
        _fieldView = fieldView;
        _camera = camera;
        _rig = rig;
        _lastMemberFloors = new Vector2[memberViewList.Count];
        _memberViewList = memberViewList;
        _lastHps = new float[memberViewList.Count];
        _visibleCharacterIds = new string[memberViewList.Count];
        _firstExpedition = firstExpedition;
        _hitFlashes = new HitFlash[memberViewList.Count];
        _isMemberMovings = new bool[memberViewList.Count];
        _downedSeconds = new float[memberViewList.Count];
        _walkClocks = new float[memberViewList.Count];
    }

    public void Initialize()
    {
        _rig.Initialize(_viewRules.TiltDegrees, _viewRules.Distance, _viewRules.NearClip, _viewRules.FarClip, _viewRules.TargetFraction, _viewRules.MinScale);
        _fieldView.Initialize(_viewRules.TiltDegrees);
        _rig.SetFocus(IsoProjection.FloorToWorld(_partyModel.Center, _runRules.UnitScale, _terrain));
        _viewRules.SetProjection(_rig.Frame, _runRules.UnitScale, _terrain);
        // Atlas source pixels, lowest opaque row (including downed bruno), PPU 48.
        const float footHalfWidth = .625f;
        Shader.SetGlobalFloat("_HD2DActorDepthBias", TerrainHeightMath.ActorDepthBias(_terrain, footHalfWidth, _viewRules.SinTilt));
        BuildField();
        _fieldView.BuildLandscape(_fieldLayout, _terrain);
        foreach (var placement in _fieldLayout.placements)
        {
            if (placement.kind == "card")
            {
                _fieldView.AddCard(_spriteAtlasService.Get(placement.sprite, SpriteAtlasService.GroundAtlasPath), placement, RenderSettings.sun.transform.forward);
            }
        }

        _fieldView.FinishScenery(_fieldLayout);
        _fieldView.BuildProps(_fieldLayout,
            _spriteAtlasService.Get("flag_01", SpriteAtlasService.GroundAtlasPath),
            _spriteAtlasService.Get("shadow", SpriteAtlasService.CharactersAtlasPath));
        InitializeMemberViews();
        UpdateViewExtent();
    }

    public void LateTick()
    {
        _rig.UpdateScreen(_camera.pixelWidth, _camera.pixelHeight);
        UpdateViewExtent();
        var center = IsoProjection.FloorToWorld(_partyModel.Center, _runRules.UnitScale, _terrain);
        _rig.SetFocus(center);
        _viewRules.SetProjection(_rig.Frame, _runRules.UnitScale, _terrain);
        VisibleFloor = new FloorViewBounds(_rig.Frame, center.y);
        var isRunning = _runModel.Phase.CurrentValue == RunPhase.Battle && !_runModel.IsPaused.CurrentValue;
        var lastClock = _motionClock;
        _motionClock = SpriteMotion.Advance(_motionClock, Time.deltaTime, isRunning);
        // 남은 시간은 시뮬레이션 걸음마다 줄어든다
        var encounterSteps = _firstExpedition.EncounterSteps;
        var hasStepped = _runModel.RemainingSeconds.CurrentValue != _lastRemainingSeconds
            || encounterSteps != _lastEncounterSteps;
        _lastEncounterSteps = encounterSteps;
        _lastRemainingSeconds = _runModel.RemainingSeconds.CurrentValue;
        var isRunStarted = _runModel.RunCount.CurrentValue != _lastRunCount;
        _lastRunCount = _runModel.RunCount.CurrentValue;
        if (isRunStarted)
        {
            _isRecordingPlacements = false;
            _placementObservationRows.Clear();
            SpawnFootVisibleCount = 0;
            SpawnBodyVisibleCount = 0;
        Array.Clear(SpawnFeetByDirection, 0, 4);
            Array.Clear(SpawnBodiesByDirection, 0, 4);
            _placementVersionDict.Clear();
            MemberHitFlashTotal = 0;
            MemberFlashMaxSeconds = -1f;
            _idleOffsetSeenMask = 0;
        }

        UpdateMemberViews(isRunning, hasStepped, _motionClock - lastClock, isRunStarted);
        foreach (var member in _memberViewList)
        {
            if (member.gameObject.activeInHierarchy)
            {
                member.ApplyProjection(_viewRules.Frame);
            }
        }
        SyncMonsterViews(isRunning, hasStepped);
    }

    private void BuildField()
    {
        var pathCellSet = new HashSet<Vector2Int>();
        foreach (var cell in _groundPath.Cells)
        {
            pathCellSet.Add(new Vector2Int(cell.X, cell.Y));
        }

        var tileDict = new Dictionary<string, Tile>();
        var cells = _runRules.FieldCells;
        var half = cells / 2;
        var tileChangeDatas = new TileChangeData[cells * cells];
        var index = 0;
        for (var y = -half; y < cells - half; y++)
        {
            for (var x = -half; x < cells - half; x++)
            {
                var cell = new Vector2Int(x, y);
                var choice = default(GroundTileChoice);
                if (pathCellSet.Contains(cell))
                {
                    choice = GroundTilePicker.PickPath(cell, pathCellSet);
                    if (!choice.HasTile)
                    {
                        GameLogger.LogError($"[WorldPresenter] Path cell {cell} has no tile for its neighbours, grass instead");
                    }
                }

                if (!choice.HasTile)
                {
                    var grassIndex = GroundTilePicker.PickGrass(x, y, GroundTilePicker.GrassCount);
                    choice = new GroundTileChoice(GroundTilePicker.GetGrassSpriteName(grassIndex), false);
                }

                tileChangeDatas[index] = new TileChangeData(new Vector3Int(x, y, 0), GetGroundTile(choice.SpriteName, tileDict),
                    Color.white, Matrix4x4.Scale(new Vector3(choice.IsFlipped ? -1f : 1f, 2f, 1f)));
                index++;
            }
        }

        _fieldView.Build(tileChangeDatas);
    }

    private Tile GetGroundTile(string spriteName, Dictionary<string, Tile> tileDict)
    {
        if (!tileDict.TryGetValue(spriteName, out var tile))
        {
            tile = _placeholderSpriteFactory.CreateTile(_spriteAtlasService.Get(spriteName, SpriteAtlasService.GroundAtlasPath));
            tileDict.Add(spriteName, tile);
        }

        return tile;
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
            _memberViewList[i].SetVisible(false);
            if (i < characterDataList.Count)
            {
                BindCharacter(i, characterDataList[i].Id);
            }
        }
    }

    private void BindCharacter(int index, string characterId)
    {
        _visibleCharacterIds[index] = characterId;
        var view = _memberViewList[index];
        var data = _characterRepository.Get(characterId);
        // 그림 키가 있으면 앞·뒤·쓰러진 그림, 비었거나 하나라도 없으면 F1 임시 원
        var key = data.Sprite;
        Sprite frontSprite = null;
        Sprite backSprite = null;
        Sprite downedSprite = null;
        if (!string.IsNullOrEmpty(key))
        {
            frontSprite = _spriteAtlasService.Get(key + SpriteAtlasService.FrontSuffix);
            backSprite = _spriteAtlasService.Get(key + SpriteAtlasService.BackSuffix);
            downedSprite = _spriteAtlasService.Get(key + SpriteAtlasService.DownedSuffix);
        }

        var hasArt = frontSprite != null && backSprite != null && downedSprite != null;
        if (!hasArt)
        {
            frontSprite = _placeholderSpriteFactory.CreateCircle(view.BodyDiameterPixels,
                _placeholderSpriteFactory.ParseColor(data.Color));
            backSprite = frontSprite;
            downedSprite = frontSprite;
        }

        var hpBarSprite = _spriteAtlasService.GetUi("hp_bar_bg");
        view.Initialize(frontSprite, backSprite, downedSprite, hpBarSprite, _spriteAtlasService.GetUi("hp_bar_fill"), hasArt);
        if (hasArt)
        {
            view.InitializeDirections(_spriteAtlasService.GetDirections(key));
        }
        view.InitializeShadow();
        var motion = data.Motion;
        if (hasArt && motion != null)
        {
            view.InitializeClips(motion,
                _spriteAtlasService.GetDirectionFrames(key, "walk", motion.WalkFrames),
                _spriteAtlasService.GetDirectionFrames(key, "attack", motion.AttackFrames));
        }
    }

    // 카메라가 보는 월드 영역 → 바닥 단위 반 크기. 바뀔 때만 생성기에 알린다
    private void UpdateViewExtent()
    {
        var floorHalfExtent = _viewRules.GameplayHalfExtent(_camera.pixelWidth, _camera.pixelHeight) / _runRules.UnitScale;
        if (floorHalfExtent == _viewHalfExtent)
        {
            return;
        }

        _viewHalfExtent = floorHalfExtent;
        _monsterService.SetViewHalfExtent(floorHalfExtent);
        _partyMoveService.SetViewHalfExtent(floorHalfExtent);
    }

    public void BeginPlacementObservation()
    {
        _isRecordingPlacements = true;
        _placementObservationRows.Clear();
        _placementObservationRows.Add("time,id,version,type,footX,footY,bodyXMin,bodyYMin,bodyXMax,bodyYMax,footVisible,bodyVisible,direction");
        SpawnFootVisibleCount = 0;
        SpawnBodyVisibleCount = 0;
        Array.Clear(SpawnFeetByDirection, 0, 4);
        Array.Clear(SpawnBodiesByDirection, 0, 4);
    }

    private void ObservePlacement(Monster monster, MonsterView view)
    {
        if (_placementVersionDict.TryGetValue(monster.Id, out var version) && version == monster.PlacementVersion)
        {
            return;
        }
        _placementVersionDict[monster.Id] = monster.PlacementVersion;
        var relative = monster.Position - _partyModel.Center;
        var direction = SpawnAppearanceMath.Direction(relative);
        var foot = _camera.WorldToViewportPoint(view.transform.position);
        var isFootVisible = foot.z > 0 && foot.x >= 0 && foot.x <= 1 && foot.y >= 0 && foot.y <= 1;
        if (isFootVisible)
        {
            SpawnFootVisibleCount++;
            SpawnFeetByDirection[direction]++;
        }
        var body = view.BodyRenderer;
        var bounds = OcclusionMath.ProjectBounds(_camera, body.bounds);
        var isBodyVisible = body.enabled && body.gameObject.activeInHierarchy && _camera.WorldToViewportPoint(body.bounds.center).z > 0
            && bounds.Overlaps(new Rect(0, 0, 1, 1));
        if (isBodyVisible)
        {
            SpawnBodyVisibleCount++;
            SpawnBodiesByDirection[direction]++;
        }
        view.BeginAppearance(isFootVisible || isBodyVisible);
        if (_isRecordingPlacements)
        {
            _placementObservationRows.Add(string.Format(System.Globalization.CultureInfo.InvariantCulture,
                "{0},{1},{2},{3},{4},{5},{6},{7},{8},{9},{10},{11},{12}", Time.time, monster.Id, monster.PlacementVersion,
                monster.Data.Id, foot.x, foot.y, bounds.xMin, bounds.yMin, bounds.xMax, bounds.yMax, isFootVisible, isBodyVisible, new[] { "north", "east", "west", "south" }[direction]));
        }
    }

    private void UpdateMemberViews(bool isRunning, bool hasStepped, float deltaSeconds, bool isRunStarted)
    {
        var memberList = _partyModel.MemberList;
        var count = Math.Min(memberList.Count, _memberViewList.Count);
        for (var i = 0; i < _memberViewList.Count; i++)
        {
            var view = _memberViewList[i];
            if (i >= count)
            {
                if (i == count && _firstExpedition.WaitingCharacterId is string waitingId)
                {
                    if (_waitingSlot != i || _visibleCharacterIds[i] != waitingId)
                    {
                        BindCharacter(i, waitingId);
                        _waitingSlot = i;
                    }

                    var waitingPosition = IsoProjection.FloorToWorldSnapped(
                        _firstExpedition.EncounterPosition, _runRules.UnitScale, _viewRules.SinTilt, _terrain);
                    view.SetVisible(true);
                    view.UpdateView(waitingPosition, 1f, true, 0f);
                    view.HideHealthBar();
                    view.SetGround(_terrain, TerrainHeightMath.Normal(_terrain, waitingPosition.x, waitingPosition.z), _fieldView.TerrainShadowMaterial);
                    continue;
                }

                view.SetVisible(false);
                if (_waitingSlot == i)
                {
                    _waitingSlot = -1;
                }

                continue;
            }

            var member = memberList[i];
            var didCharacterChange = _visibleCharacterIds[i] != member.CharacterId;
            if (didCharacterChange)
            {
                BindCharacter(i, member.CharacterId);
            }

            var position = IsoProjection.FloorToWorldSnapped(member.Position, _runRules.UnitScale, _viewRules.SinTilt, _terrain);
            var delta = member.Position - _lastMemberFloors[i];
            _lastMemberFloors[i] = member.Position;
            view.SetVisible(true);
            _downedSeconds[i] = member.IsAlive ? 0f : _downedSeconds[i] + Time.deltaTime;
            view.UpdateView(position, member.Hp / member.MaxHp, member.IsAlive, _downedSeconds[i]);
            view.SetGround(_terrain, TerrainHeightMath.Normal(_terrain, position.x, position.z), _fieldView.TerrainShadowMaterial);
            if (isRunStarted || didCharacterChange || _waitingSlot == i)
            {
                if (_waitingSlot == i)
                {
                    _waitingSlot = -1;
                }

                // 판 시작: 앞·오른쪽, 첫 표시의 자리 점프는 방향을 바꾸지 않는다
                _lastHps[i] = member.Hp;
                _hitFlashes[i].Stop();
                _isMemberMovings[i] = false;
                _walkClocks[i] = 0f;
                view.ApplyAnimation(false, 0f, _combatModel.MemberStateList[i].AttackMotion);
                view.ApplyMotion(SpriteFacing.Front, Vector2Int.zero, false);
                continue;
            }

            if (!member.IsAlive)
            {
                // 쓰러진 대원: 방향은 쓰러질 때 그대로, 번쩍임 끔·오프셋 0(시계와 상관없이)
                _lastHps[i] = member.Hp;
                _hitFlashes[i].Stop();
                view.ApplyAnimation(false, 0f, _combatModel.MemberStateList[i].AttackMotion);
                view.ApplyMotion(view.Facing, Vector2Int.zero, false);
                continue;
            }

            if (!isRunning)
            {
                // 멈춘 시계: 방향·오프셋·번쩍임은 마지막 값. 그동안의 HP 변화(부활·정비 회복)는 피격이 아니다
                _lastHps[i] = member.Hp;
                if (_runModel.Phase.CurrentValue != RunPhase.Battle)
                {
                    view.ApplyAnimation(false, 0f, _combatModel.MemberStateList[i].AttackMotion);
                }

                continue;
            }

            UpdateMemberMotion(i, view, member.Hp, hasStepped, delta, deltaSeconds);
        }
    }

    // 번쩍임은 먼저 흘리고(이번 프레임에 끝나면 기록) 그다음 새 피격으로 시작한다 — 시작한 프레임에는 반드시 켜진 채 그려진다
    private void UpdateMemberMotion(int index, MemberView view, float hp, bool hasStepped, Vector2 delta, float deltaSeconds)
    {
        var flash = _hitFlashes[index];
        if (flash.Advance(deltaSeconds))
        {
            RecordFlashSeconds(flash.Elapsed);
        }

        if (hp < _lastHps[index])
        {
            if (flash.IsOn)
            {
                RecordFlashSeconds(flash.Elapsed);
            }

            flash.Start(view.HitFlashSeconds);
            MemberHitFlashTotal++;
        }

        _lastHps[index] = hp;
        _hitFlashes[index] = flash;

        var facing = view.Facing;
        if (hasStepped)
        {
            var minMove = view.MinMovePixels / Const.PixelsPerUnit;
            facing = SpriteFacing.Next(view.Facing, _viewRules.Direction(_lastMemberFloors[index] - delta, delta), minMove, _viewRules.FacingHoldDegrees);
            _isMemberMovings[index] = delta.sqrMagnitude >= Mathf.Pow(minMove / _runRules.UnitScale, 2);
            if (_isMemberMovings[index])
            {
                // 주기를 실제 이동 거리에 묶는다. 정속에서는 source fps, 따라붙기·감속 때 같은 비율로 재생.
                _walkClocks[index] += delta.magnitude / _moveRules.MoveSpeed;
            }
            else
            {
                _walkClocks[index] = 0f;
            }
        }

        var idleOffsetY = 0;
        var attack = _combatModel.MemberStateList[index].AttackMotion;
        if (!_isMemberMovings[index] && !attack.IsPlaying)
        {
            idleOffsetY = SpriteMotion.GetIdleOffsetY(_motionClock, view.IdlePeriodSeconds, index * view.IdleStaggerSeconds);
            if (index == 0)
            {
                _idleOffsetSeenMask |= 1 << idleOffsetY;
            }
        }

        if (attack.IsPlaying)
        {
            facing = attack.Facing;
        }

        var offset = new Vector2Int(SpriteMotion.GetHitOffsetX(flash.IsOn, facing.IsFlipped), idleOffsetY);
        view.ApplyMotion(facing, offset, flash.IsOn);
        view.ApplyAnimation(_isMemberMovings[index], _walkClocks[index], attack);
    }

    private void RecordFlashSeconds(float seconds) => MemberFlashMaxSeconds = Mathf.Max(MemberFlashMaxSeconds, seconds);

    // 모델의 산 적마다 그림 하나. 새 적은 풀에서 꺼내고 사라진 적의 그림은 풀로 돌린다
    private void SyncMonsterViews(bool isRunning, bool hasStepped)
    {
        _aliveMonsterIdSet.Clear();
        foreach (var monster in _monsterModel.MonsterList)
        {
            if (!monster.IsAlive)
            {
                continue;
            }

            _aliveMonsterIdSet.Add(monster.Id);
            var isNew = !_monsterViewRegistry.TryGet(monster.Id, out var view);
            if (isNew)
            {
                view = _monsterViewFactory.Create(monster, null);
            }

            var position = IsoProjection.FloorToWorldSnapped(monster.Position, _runRules.UnitScale, _viewRules.SinTilt, _terrain);
            // 새로 꺼낸 그림은 이동량 0으로 본다(풀에 있던 자리에서 뛰지 않게)
            var delta = isNew ? Vector2.zero : monster.Position - _lastMonsterFloors[monster.Id];
            _lastMonsterFloors[monster.Id] = monster.Position;
            view.UpdateView(position);
            view.SetTerrain(_terrain, TerrainHeightMath.Normal(_terrain, position.x, position.z), _fieldView.TerrainShadowMaterial);
            if (isRunning)
            {
                UpdateMonsterMotion(view, monster, hasStepped, delta);
            }
            view.ApplyProjection(_viewRules.Frame);
            ObservePlacement(monster, view);
            view.AdvanceAppearance(isRunning ? Time.unscaledDeltaTime : 0f);
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
            _movingMonsterIdSet.Remove(id);
            _lastMonsterFloors.Remove(id);
            _placementVersionDict.Remove(id);
        }
    }

    // 8방향과 이동 중 hop/squash, 예고 중 좌우 1px 흔들림. hop 시작은 (번호 × stagger) mod 주기로 어긋낸다(결정적)
    private void UpdateMonsterMotion(MonsterView view, Monster monster, bool hasStepped, Vector2 delta)
    {
        var facing = view.Facing;
        if (hasStepped)
        {
            var minMove = view.MinMovePixels / Const.PixelsPerUnit;
            facing = SpriteFacing.Next(view.Facing, _viewRules.Direction(monster.Position - delta, delta), minMove, _viewRules.FacingHoldDegrees);
            if (delta.sqrMagnitude >= Mathf.Pow(minMove / _runRules.UnitScale, 2))
            {
                _movingMonsterIdSet.Add(monster.Id);
            }
            else
            {
                _movingMonsterIdSet.Remove(monster.Id);
            }
        }

        var offsetY = 0;
        if (_movingMonsterIdSet.Contains(monster.Id))
        {
            var startOffset = Mathf.Repeat(monster.Id * view.StaggerSeconds, view.MovePeriodSeconds);
            offsetY = SpriteMotion.GetMoveOffsetY(monster.Data.MoveStyle, _motionClock, view.MovePeriodSeconds, startOffset);
        }

        var offsetX = SpriteMotion.GetShakeOffsetX(view.IsWarning, _motionClock, view.ShakePeriodSeconds);
        view.ApplyMotion(facing, new Vector2Int(offsetX, offsetY));
    }
}
