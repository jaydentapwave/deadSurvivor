#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using VContainer;
using Object = UnityEngine.Object;

// 플레이 가능 판정용 진입점(기획서 H절, .ai/rules/playable-check.md). 판정 경로(tools/playable/*.json)의 식이 부른다.
// 장면 LifetimeScope 컨테이너에서 모델·Presenter를 꺼내 읽고, 버튼은 Presenter의 버튼 처리 메서드(onClick과 같은 길)를 부른다.
// 승패·단계를 써 넣지 않는다. 게임 코드는 이 클래스를 모른다.
public static class PlayableProbe
{
    private const string CloneSuffix = "(Clone)";
    private static int _lastPlacedMember = -1;
    private static string _lastPlacedItemId;

    [Flags]
    public enum BotMode
    {
        None = 0,
        // 조이스틱 입력과 같은 이동 명령으로 들판 가운데를 원으로 돈다(F2)
        Move = 1,
        // 보관함 전리품 자동 장착(F5가 채운다)
        Equip = 2,
        // 정비 화면이면 다음 웨이브를 누른다
        AutoNext = 4,
    }

    // 판정 진입점이 조이스틱 포인터 처리에 쓰는 포인터 번호
    private const int ProbePointerId = 0;

    // MarkPositions가 기억한 원정대 중심·대원 좌표·생존(F2 H절). 편집기 판정 전용 상태
    private static Vector2 _markCenter;
    private static readonly List<Vector2> _markPositionList = new();
    private static readonly List<bool> _markAliveList = new();

    // 장면이 아직 없으면 "None"
    public static string Screen
    {
        get
        {
            var scope = Object.FindAnyObjectByType<GameSceneLifetimeScope>();
            return scope == null || scope.Container == null ? "None" : scope.Container.Resolve<RunModel>().Phase.CurrentValue.ToString();
        }
    }

    public static int CardCount => Resolve<LobbyPresenter>().CardCount;
    public static bool CanDepart => Resolve<LobbyPresenter>().CanDepart;
    public static int Wave => Resolve<RunModel>().Wave.CurrentValue;
    public static int CompletedWave => Resolve<RunModel>().CompletedWave.CurrentValue;
    public static float WaveSeconds => Resolve<RunModel>().WaveSeconds.CurrentValue;
    public static float RemainingSeconds => Resolve<RunModel>().RemainingSeconds.CurrentValue;
    public static bool IsBurningTime => Resolve<RunModel>().IsBurningTime.CurrentValue;
    public static int AliveCount => Resolve<PartyService>().AliveCount;
    public static int Kills => Resolve<RunModel>().Kills.CurrentValue;
    public static int EnemyCount => Resolve<MonsterService>().AliveCount;
    public static string Outcome => Resolve<RunModel>().Outcome.CurrentValue.ToString();
    public static int WavesSurvived => Resolve<RunModel>().WavesSurvived.CurrentValue;
    public static float TotalBattleSeconds => Resolve<RunModel>().TotalBattleSeconds.CurrentValue;
    // Play 뒤 시작한 판 수
    public static int RunCount => Resolve<RunModel>().RunCount.CurrentValue;

    // 원정대가 데이터의 캐릭터 수만큼 있고 각자 장착 목록이 시작 아이템 하나뿐인가
    public static bool HasOnlyStartItems
    {
        get
        {
            var characterRepository = Resolve<CharacterRepository>();
            var memberList = Resolve<PartyModel>().MemberList;
            if (memberList.Count != characterRepository.CharacterDataList.Count)
            {
                return false;
            }

            foreach (var member in memberList)
            {
                var items = member.EquippedItemIdList;
                if (items.Count != 1 || items[0] != characterRepository.Get(member.CharacterId).StartItem)
                {
                    return false;
                }
            }

            return true;
        }
    }

    // F3 전투(기획서 H절)
    public static float MemberHp(int member) => Resolve<PartyModel>().MemberList[member].Hp;
    public static float MemberMaxHp(int member) => Resolve<PartyModel>().MemberList[member].MaxHp;
    public static float MemberShield(int member) => Resolve<PartyModel>().MemberList[member].Shield;
    public static bool IsMemberAlive(int member) => Resolve<PartyModel>().MemberList[member].IsAlive;
    public static int BattleItemCount(int member) => Resolve<CombatModel>().MemberStateList[member].SlotList.Count;
    public static string ItemState(int member, int slot) => Resolve<BattleItemListPresenter>().GetItemState(member, slot).ToString();
    // 이번 판 그 아이템의 원정대 전체 발동 수
    public static int FireCount(string itemId) => Resolve<CombatModel>().GetFireCount(itemId);
    public static bool CanRevive(int member) => Resolve<ReviveService>().CanRevive(member);
    // 없으면 −1
    public static int LastRevivedMember => Resolve<CombatModel>().LastRevivedMember;

    // 전투 아이템 칸(발동 목록)에 그 id가 있는가. F5의 IsPlacedItemActive가 이것을 부른다
    public static bool HasBattleItem(int member, string itemId)
    {
        foreach (var slot in Resolve<CombatModel>().MemberStateList[member].SlotList)
        {
            if (slot.Item.Id == itemId)
            {
                return true;
            }
        }

        return false;
    }

    // 쓰러진 대원 중 가장 앞 번호, 없으면 −1
    public static int FirstDownMember
    {
        get
        {
            var memberList = Resolve<PartyModel>().MemberList;
            for (var i = 0; i < memberList.Count; i++)
            {
                if (!memberList[i].IsAlive)
                {
                    return i;
                }
            }

            return -1;
        }
    }

    // 부활 버튼 onClick과 같은 길
    public static void PressRevive(int member) => Resolve<RevivePresenter>().OnReviveClicked(member);

    // 진형 id(F2)
    public static string Formation => Resolve<FormationModel>().CurrentId.CurrentValue;
    public static string PendingFormation => Resolve<FormationModel>().PendingId.CurrentValue;
    // 원정대 중심(바닥 좌표)
    public static float PartyX => Resolve<PartyModel>().Center.x;
    public static float PartyY => Resolve<PartyModel>().Center.y;
    // MarkPositions 뒤 중심 변화
    public static float PartyDeltaX => PartyX - _markCenter.x;
    public static float PartyDeltaY => PartyY - _markCenter.y;
    public static float PartyMovedSinceMark => (Resolve<PartyModel>().Center - _markCenter).magnitude;
    public static bool IsJoystickShown => Resolve<UI_JoystickView>().IsShown;

    // 살아 있는 대원과 (중심 + 지금 진형 자리)의 최대 거리
    public static float MaxSlotError
    {
        get
        {
            var partyModel = Resolve<PartyModel>();
            var formationService = Resolve<FormationService>();
            var memberList = partyModel.MemberList;
            var maxError = 0f;
            for (var i = 0; i < memberList.Count; i++)
            {
                if (memberList[i].IsAlive)
                {
                    var slotPosition = partyModel.Center + formationService.GetSlot(i);
                    maxError = Mathf.Max(maxError, Vector2.Distance(memberList[i].Position, slotPosition));
                }
            }

            return maxError;
        }
    }

    // 표시 때도 지금도 쓰러진 대원이 표시 뒤 움직인 최대 거리. 없으면 0
    public static float MaxDeadMoved
    {
        get
        {
            var memberList = Resolve<PartyModel>().MemberList;
            var maxMoved = 0f;
            for (var i = 0; i < memberList.Count && i < _markPositionList.Count; i++)
            {
                if (!_markAliveList[i] && !memberList[i].IsAlive)
                {
                    maxMoved = Mathf.Max(maxMoved, Vector2.Distance(memberList[i].Position, _markPositionList[i]));
                }
            }

            return maxMoved;
        }
    }

    // 카메라 중심과 IsoProjection(원정대 중심)의 월드 거리
    public static float CameraError
    {
        get
        {
            var expected = IsoProjection.FloorToWorld(Resolve<PartyModel>().Center, Resolve<RunRulesData>().UnitScale, Resolve<TerrainHeightGrid>());
            var camera = Resolve<Camera>().transform;
            var rig = Resolve<TiltedCameraRig>();
            var restored = camera.position + camera.forward * rig.Distance - camera.right * rig.ShakeOffset.x - camera.up * rig.ShakeOffset.y;
            var diff = restored - expected;
            return new Vector2(Vector3.Dot(diff, camera.right), Vector3.Dot(diff, camera.up)).magnitude;
        }
    }

    // F4 — 몬스터 5종·웨이브 목표값(F4 기획서 E절). 기록은 PlayableMetricsRecorder가 맡는다
    // 지금 살아 있는 몬스터의 행동 종류 수
    public static int AliveBehaviorKinds
    {
        get
        {
            var kindSet = new System.Collections.Generic.HashSet<MonsterBehavior>();
            foreach (var monster in Resolve<MonsterModel>().MonsterList)
            {
                if (monster.IsAlive)
                {
                    kindSet.Add(monster.Behavior);
                }
            }

            return kindSet.Count;
        }
    }

    // 지금 날아가는 적 투사체 수
    public static int EnemyShotCount
    {
        get
        {
            var count = 0;
            foreach (var shot in Resolve<MonsterModel>().EnemyShotList)
            {
                if (shot.IsAlive)
                {
                    count++;
                }
            }

            return count;
        }
    }

    public static bool IsSpawnCountAsData => Recorder != null && Recorder.IsSpawnCountAsData;
    public static float BurningFrameMsP95 => Recorder != null ? Recorder.BurningFrameMsP95 : -1f;
    public static int MaxAliveEnemies => Recorder != null ? Recorder.MaxAliveEnemies : 0;

    // 기록기를 만들고 이후 모든 판을 기록한다. 웨이브 끝·판 끝마다 path에 파일 전체를 다시 쓴다
    public static void StartWaveMetrics(string path, bool shouldCollectFrameTimes = true) =>
        GetOrCreateRecorder().StartRecording(path, shouldCollectFrameTimes);

    // 다음에 그 웨이브 전투가 시작되면 Time.timeScale = 1, Application.runInBackground = true(한 번만)
    public static void UseRealTimeAtWave(int wave) => GetOrCreateRecorder().UseRealTimeAtWave(wave);

    // 마지막 기록 판의 그 웨이브 정규 생성 수(기록 없으면 −1)
    public static int SpawnedCountOf(int wave) => Recorder != null ? Recorder.SpawnedCountOf(wave) : -1;

    // 마지막 기록 판의 모든 웨이브가 끝까지 갔고 각 생성 수가 first → last 직선 목표 ± tolerance 안(M2)
    public static bool IsSpawnCountNear(int first, int last, float tolerance) =>
        Recorder != null && Recorder.IsSpawnCountNear(first, last, tolerance);

    // 웨이브 1부터 기록한 마지막 판에서 그 행동(contact·projectile·charge·armored·bomb)이 처음 나온 웨이브, 없으면 0(M3)
    public static int FirstWaveOf(string behavior) =>
        Recorder != null ? Recorder.FirstWaveOf(behavior) : 0;

    // F5 가방·전리품(기획서 H절). 읽기는 BagModel·Presenter를 읽기만, 조작은 BagPresenter 처리 메서드(사람이 누르는 길)만 부른다
    public static int StashCount => Resolve<BagModel>().StashList.Count;

    public static int BagItemCount(int member) => Resolve<BagModel>().GetBag(member).Count;

    // 원정대 전체 가방 아이템 수
    public static int EquippedItemCount
    {
        get
        {
            var bagModel = Resolve<BagModel>();
            var count = 0;
            for (var member = 0; member < bagModel.MemberCount; member++)
            {
                count += bagModel.GetBag(member).Count;
            }

            return count;
        }
    }

    // 가방·보관함 전체에서 가장 높은 등급 번호(아이템이 없으면 −1)
    public static int MaxItemGrade
    {
        get
        {
            var bagModel = Resolve<BagModel>();
            var maxGrade = -1;
            for (var member = 0; member < bagModel.MemberCount; member++)
            {
                foreach (var item in bagModel.GetBag(member))
                {
                    maxGrade = Mathf.Max(maxGrade, item.Grade);
                }
            }

            foreach (var item in bagModel.StashList)
            {
                maxGrade = Mathf.Max(maxGrade, item.Grade);
            }

            return maxGrade;
        }
    }

    // PlaceFirstStashItem이 놓은 대원(없으면 −1)과 아이템 id. 판정 경로 안에서만 쓰는 진입점 기록
    public static int LastPlacedMember => _lastPlacedMember;

    // 놓은 아이템이 다음 웨이브 그 대원의 발동 목록(전투 아이템 칸)에 있고, 칸 수가 가방 아이템 수와 같다
    public static bool IsPlacedItemActive => _lastPlacedMember >= 0 && HasBattleItem(_lastPlacedMember, _lastPlacedItemId)
        && BattleItemCount(_lastPlacedMember) == BagItemCount(_lastPlacedMember);

    // 보관함 아이템과 합성할 수 있는 살아 있는 대원 가방·보관함 아이템이 있다
    public static bool HasMergePair => FindMergePair(out _, out _);

    // 보관함 첫 아이템을 살아 있는 대원 순 × 회전 0~3 × y·x 순으로 처음 빈 칸 놓기가 되는 곳에 놓는다
    public static void PlaceFirstStashItem()
    {
        var bagModel = Resolve<BagModel>();
        if (bagModel.StashList.Count == 0)
        {
            GameLogger.LogWarning("[PlayableProbe] PlaceFirstStashItem: stash is empty");
            return;
        }

        var uid = bagModel.StashList[0].Uid;
        var itemId = bagModel.StashList[0].ItemId;
        var bagService = Resolve<BagService>();
        var bagRules = Resolve<BagRulesRepository>().Rules;
        var memberList = Resolve<PartyModel>().MemberList;
        for (var member = 0; member < memberList.Count; member++)
        {
            if (!memberList[member].IsAlive)
            {
                continue;
            }

            for (var rotation = 0; rotation < 4; rotation++)
            {
                for (var y = 0; y < bagRules.BagHeight; y++)
                {
                    for (var x = 0; x < bagRules.BagWidth; x++)
                    {
                        if (!bagService.CanPlace(bagModel, uid, member, x, y, rotation))
                        {
                            continue;
                        }

                        PressDrop(uid, rotation, BagTarget.Cell(member, x, y));
                        _lastPlacedMember = member;
                        _lastPlacedItemId = itemId;
                        return;
                    }
                }
            }
        }

        GameLogger.LogWarning($"[PlayableProbe] PlaceFirstStashItem: no empty place for {itemId}");
    }

    // HasMergePair의 첫 쌍을 같은 길(집기 → 대상에 놓기)로 합성한다
    public static void MergeFirstPair()
    {
        if (!FindMergePair(out var sourceUid, out var target))
        {
            GameLogger.LogWarning("[PlayableProbe] MergeFirstPair: no merge pair");
            return;
        }

        PressDrop(sourceUid, 0, target);
    }

    // 집기 → 회전 rotation번 맞추기 → 대상에 놓기(드래그 놓기와 같은 처리)
    internal static void PressDrop(int uid, int rotation, BagTarget target)
    {
        var bagPresenter = Resolve<BagPresenter>();
        bagPresenter.OnItemPressed(uid);
        for (var i = 0; i < 4 && bagPresenter.PickedRotation != rotation; i++)
        {
            bagPresenter.OnRotateClicked();
        }

        bagPresenter.OnItemDropped(target);
    }

    // 합성 대상을 놓을 대상으로: 가방 아이템이면 그 아이템이 차지한 첫 칸(놓기가 그 칸 아이템과 합성으로 간다), 보관함이면 그 항목
    internal static BagTarget GetMergeTarget(int targetUid)
    {
        var bagModel = Resolve<BagModel>();
        var target = bagModel.FindItem(targetUid, out var owner);
        if (owner < 0)
        {
            return BagTarget.StashItem(targetUid);
        }

        var cellList = new System.Collections.Generic.List<Vector2Int>();
        BagRules.GetItemCells(target, Resolve<ItemRepository>(), cellList);
        return BagTarget.Cell(owner, cellList[0].x, cellList[0].y);
    }

    // 보관함 앞에서부터, 대상 = 살아 있는 대원 가방(대원 순, 목록 순) → 보관함의 다른 아이템
    private static bool FindMergePair(out int sourceUid, out BagTarget target)
    {
        var bagModel = Resolve<BagModel>();
        var lastGrade = Resolve<GradeRepository>().LastGrade;
        var memberList = Resolve<PartyModel>().MemberList;
        foreach (var source in bagModel.StashList)
        {
            for (var member = 0; member < bagModel.MemberCount && member < memberList.Count; member++)
            {
                if (!memberList[member].IsAlive)
                {
                    continue;
                }

                foreach (var item in bagModel.GetBag(member))
                {
                    if (BagRules.CanMerge(source, item, lastGrade))
                    {
                        sourceUid = source.Uid;
                        target = GetMergeTarget(item.Uid);
                        return true;
                    }
                }
            }

            foreach (var item in bagModel.StashList)
            {
                if (BagRules.CanMerge(source, item, lastGrade))
                {
                    sourceUid = source.Uid;
                    target = BagTarget.StashItem(item.Uid);
                    return true;
                }
            }
        }

        sourceUid = -1;
        target = default;
        return false;
    }

    // F6 전투 피드백(기획서 H절). Model·Registry·Presenter·Service를 읽기만 한다
    public static int DamageNumberCount => Resolve<DamageNumberViewRegistry>().Count;
    public static int DamageNumberShownTotal => Resolve<CombatFeedbackModel>().DamageNumberShownTotal;
    public static int DamageNumberPeak => Resolve<CombatFeedbackModel>().DamageNumberPeak;
    public static int DamageNumberMaxActive => Resolve<FeedbackRulesRepository>().Rules.DamageNumber.MaxActive;
    // 빨간 구간이 보이는 가장 앞 대원, 없으면 −1
    public static int MemberWithHpLoss => Resolve<HpLossPresenter>().MemberWithHpLoss;
    public static float HpLossRatio(int member) => Resolve<HpLossPresenter>().GetLossRatio(member);
    public static int ShakeCount => Resolve<CombatFeedbackModel>().ShakeCount;
    public static bool IsShaking => Resolve<HitReactionPresenter>().IsShaking;
    public static int HapticCount => Resolve<HapticService>().PlayCount;
    public static int MonsterHpBarCount => Resolve<MonsterHpBarViewRegistry>().Count;
    public static int AlwaysShownHpBarCount => Resolve<MonsterHpBarPresenter>().AlwaysShownCount;
    public static int MonsterHpBarRuleViolations => Resolve<MonsterHpBarPresenter>().CountRuleViolations();
    public static int MonsterHpBarHiddenByCap => Resolve<MonsterHpBarPresenter>().HiddenByCapCount;
    // MarkFrameStats 뒤 Time.unscaledDeltaTime 95백분위(ms), 표본 없으면 −1
    public static float FrameMsP95SinceMark => FrameSampler != null ? FrameSampler.FrameMsP95 : -1f;
    public static int FrameSamplesSinceMark => FrameSampler != null ? FrameSampler.SampleCount : 0;
    // MarkFrameStats 뒤 살아 있는 몬스터 최대 수
    public static int EnemyPeakSinceMark => FrameSampler != null ? FrameSampler.EnemyPeak : 0;
    // 그림 넣기(기획서 I절). Presenter·View를 읽기만 한다
    public static int CardPortraitCount => Resolve<UI_LobbyView>().PortraitCount;
    // 그림을 그리는 산 대원 수
    public static int MemberSpriteCount
    {
        get
        {
            var count = 0;
            var memberList = Resolve<PartyModel>().MemberList;
            var memberViewList = Resolve<IReadOnlyList<MemberView>>();
            for (var i = 0; i < memberList.Count && i < memberViewList.Count; i++)
            {
                if (memberList[i].IsAlive && memberViewList[i].gameObject.activeSelf && memberViewList[i].HasArt)
                {
                    count++;
                }
            }

            return count;
        }
    }

    // Missing static/walk/attack direction entries that require nearest-source substitution.
    public static int FacingFallbackCount => Resolve<IReadOnlyList<MemberView>>().Sum(view => view.FacingFallbackCount)
        + Object.FindObjectsByType<MonsterView>(FindObjectsSortMode.None).Sum(view => view.FacingFallbackCount);

    public static string MemberFacing(int member) => Resolve<IReadOnlyList<MemberView>>()[member].Facing.Label;
    public static int IdleOffsetsSeen => Resolve<WorldPresenter>().IdleOffsetsSeen;

    // 쓰러짐·예고(아트 기준서 3.10). 몸 그림 이름(아틀라스 복제의 "(Clone)"을 뗀 것)·몸 밝기(색의 r, 흰색 1)
    public static string MemberSpriteName(int member) =>
        Resolve<IReadOnlyList<MemberView>>()[member].BodySpriteName.Replace(CloneSuffix, string.Empty);
    public static float MemberBodyBrightness(int member) => Resolve<IReadOnlyList<MemberView>>()[member].BodyColor.r;
    // 대원 0~3의 몸 그림 이름을 쉼표로
    public static string MemberSpriteNames
    {
        get
        {
            var memberViewList = Resolve<IReadOnlyList<MemberView>>();
            var nameList = new List<string>();
            for (var i = 0; i < memberViewList.Count; i++)
            {
                nameList.Add(MemberSpriteName(i));
            }

            return string.Join(",", nameList);
        }
    }

    // 지금 그 예고 그림을 보이는 산 몬스터 수
    public static int WarningShownCount(string spriteName) => CountWarnings(spriteName, false);
    // 그중 몸 발(피벗)이 카메라 화면 안쪽 10% 여백 안에 있는 수(캡처에 찍히는 것)
    public static int WarningOnScreenCount(string spriteName) => CountWarnings(spriteName, true);

    // MarkWarnings 뒤 끝난 예고(그 그림)의 수·가장 긴 표시 시간(게임 시간 초, 없으면 −1)·끝난 뒤 산 채 평소 그림으로 돌아온 수·지난 흔들림 오프셋 종류 수
    public static int WarningEndedCount(string spriteName) => WarningSampler != null ? WarningSampler.GetEndedCount(spriteName) : 0;
    public static float WarningMaxSeconds(string spriteName) => WarningSampler != null ? WarningSampler.GetMaxSeconds(spriteName) : -1f;
    public static int WarningReturnedCount(string spriteName) => WarningSampler != null ? WarningSampler.GetReturnedCount(spriteName) : 0;
    public static int WarningShakeOffsetsSeen(string spriteName) =>
        WarningSampler != null ? WarningSampler.GetShakeOffsetsSeen(spriteName) : 0;

    // 캡처용: 그 예고가 화면 안쪽에 보이면 그 프레임에 게임 시간을 멈추고(timeScale 0) true. 명령 왕복(약 1초)이 예고(0.8초)보다 길어 wait 식 안에서 멈춘다
    public static bool PauseAtWarningOnScreen(string spriteName)
    {
        if (WarningOnScreenCount(spriteName) < 1)
        {
            return false;
        }

        Time.timeScale = 0f;
        return true;
    }

    // 멈춘 게임 시간을 실제 시간 seconds만큼 1배로 흘린 뒤 다시 멈춘다(MarkWarnings 뒤에만)
    public static void AdvanceGameTime(float seconds) => WarningSampler.AdvanceGameTime(seconds);

    // MarkWarnings 뒤 끝난 예고마다 "그림 이름 몬스터 번호 시작 끝 표시초"(게임 시간) 한 줄을 프로젝트 기준 경로에 쓴다
    public static void WriteWarningLog(string path) =>
        System.IO.File.WriteAllText(System.IO.Path.Combine(Application.dataPath, "..", path),
            WarningSampler != null ? WarningSampler.Log : string.Empty);

    // 예고 표본을 새로 모은다
    public static void MarkWarnings()
    {
        var sampler = WarningSampler;
        if (sampler == null)
        {
            sampler = new GameObject(nameof(PlayableWarningSampler)).AddComponent<PlayableWarningSampler>();
        }

        sampler.Mark();
    }

    private static PlayableWarningSampler WarningSampler => Object.FindAnyObjectByType<PlayableWarningSampler>();

    private static int CountWarnings(string spriteName, bool isOnScreenOnly)
    {
        var registry = Resolve<MonsterViewRegistry>();
        var camera = Resolve<Camera>();
        var count = 0;
        foreach (var monster in Resolve<MonsterModel>().MonsterList)
        {
            if (!monster.IsAlive || !registry.TryGet(monster.Id, out var view) || !view.IsWarning
                || view.WarningSpriteName.Replace(CloneSuffix, string.Empty) != spriteName)
            {
                continue;
            }

            var viewport = camera.WorldToViewportPoint(view.transform.position);
            if (!isOnScreenOnly || (viewport.x > 0.1f && viewport.x < 0.9f && viewport.y > 0.1f && viewport.y < 0.9f))
            {
                count++;
            }
        }

        return count;
    }
    public static int MemberHitFlashTotal => Resolve<WorldPresenter>().MemberHitFlashTotal;
    public static float MemberFlashMaxSeconds => Resolve<WorldPresenter>().MemberFlashMaxSeconds;

    // 바닥(바닥 넣기 T1·T5): 그림 이름이 spritePrefix로 시작하는 타일 수(isOnScreenOnly면 칸 가운데가 화면 안인 것만).
    // 빈 접두어는 모든 타일. 임시 마름모 = 풀(grass_)·흙길(path_) 그림이 아닌 타일
    public static int GroundTileCount(string spritePrefix, bool isOnScreenOnly)
    {
        var tilemap = Resolve<FieldView>().Tilemap;
        var camera = Resolve<Camera>();
        var count = 0;
        foreach (var position in tilemap.cellBounds.allPositionsWithin)
        {
            var sprite = tilemap.GetSprite(position);
            if (sprite == null || !sprite.name.StartsWith(spritePrefix, StringComparison.Ordinal))
            {
                continue;
            }

            var viewport = camera.WorldToViewportPoint(tilemap.GetCellCenterWorld(position));
            if (!isOnScreenOnly || (viewport.x > 0f && viewport.x < 1f && viewport.y > 0f && viewport.y < 1f))
            {
                count++;
            }
        }

        return count;
    }

    public static int FieldFadedCount => Resolve<FieldOcclusionPresenter>().FadedCount;
    public static int FieldGroundVisibleCount => Resolve<FieldView>().GroundVisibleCount(Resolve<Camera>());
    public static int FieldPartVisibleCount(string kind) => Resolve<FieldView>().PartVisibleCount(kind, Resolve<Camera>());
    public static int FieldPartCount(string kind) => Resolve<FieldView>().PartCount(kind);

    public static int PlaceholderGroundTileCount =>
        GroundTileCount(string.Empty, false) - GroundTileCount("grass_", false) - GroundTileCount("path_", false);

    // 그림을 그리는 산 몬스터 수
    public static int MonsterSpriteCount => CountMonsterViews(true);

    // 임시 도형을 그리는 산 대원·몬스터 수
    public static int PlaceholderBodyCount
    {
        get
        {
            var count = CountMonsterViews(false);
            var memberList = Resolve<PartyModel>().MemberList;
            var memberViewList = Resolve<IReadOnlyList<MemberView>>();
            for (var i = 0; i < memberList.Count && i < memberViewList.Count; i++)
            {
                if (memberList[i].IsAlive && memberViewList[i].gameObject.activeSelf && !memberViewList[i].HasArt)
                {
                    count++;
                }
            }

            return count;
        }
    }

    private static int CountMonsterViews(bool hasArt)
    {
        var registry = Resolve<MonsterViewRegistry>();
        var count = 0;
        foreach (var monster in Resolve<MonsterModel>().MonsterList)
        {
            if (monster.IsAlive && registry.TryGet(monster.Id, out var view) && view.HasArt == hasArt)
            {
                count++;
            }
        }

        return count;
    }

    // T1~T5: 실제 풀에서 표시한 횟수와 현재 카메라 안 그림 수. 짧은 효과는 상태 숫자와 함께 확인한다.
    public static IEnumerable<IEffectFrameSource> EffectSources()
    {
        var registry = Resolve<ProjectileViewRegistry>();
        foreach (var id in registry.ProjectileIds)
        {
            registry.TryGetProjectile(id, out var view);
            yield return view;
        }
        foreach (var id in registry.BurstIds)
        {
            registry.TryGetBurst(id, out var view);
            yield return view;
        }
        var shots = Resolve<EnemyShotViewRegistry>();
        foreach (var id in shots.ShotIds)
        {
            shots.TryGet(id, out var view);
            yield return view;
        }
        foreach (var monster in Resolve<MonsterViewRegistry>().Views)
        {
            yield return monster.DashFrameSource;
            yield return monster.BlastFrameSource;
        }
    }

    public static bool IsTerrainSpawnQuarterCaptured => UnityEngine.Object.FindAnyObjectByType<PlayableTerrainSpawnSampler>()?.IsCaptured == true;
    public static void CaptureTerrainSpawnQuarter(int corner)
    {
        var sampler = UnityEngine.Object.FindAnyObjectByType<PlayableTerrainSpawnSampler>();
        if (sampler == null) { sampler = new GameObject("Exact terrain spawn observation").AddComponent<PlayableTerrainSpawnSampler>(); }
        sampler.Arm(corner);
    }

    public static bool IsTerrainWalkCaptured => UnityEngine.Object.FindAnyObjectByType<PlayableTerrainWalkSampler>()?.IsCaptured == true;
    public static void CaptureTerrainWalk(string directory)
    {
        var sampler = new GameObject("Terrain eight-frame walk evidence").AddComponent<PlayableTerrainWalkSampler>();
        sampler.Arm(directory);
    }

    public static float PartyCenterLimitWorld
    {
        get
        {
            var rules = Resolve<RunRulesData>();
            var extent = Resolve<MonsterModel>().ViewHalfExtent * rules.UnitScale;
            return rules.FieldCells * .5f - extent.x - extent.y;
        }
    }

    public static float TerrainCaptureThreshold(float oldThreshold) => PartyCenterLimitWorld - (10.75f - oldThreshold);
    public static Vector2 TerrainCaptureTarget(float x, float z) =>
        TiltedViewMath.RemapBoundaryPoint(new Vector2(x, z), 10.75f, PartyCenterLimitWorld);

    public static float PartyGroundHeight
    {
        get
        {
            var position = Resolve<PartyModel>().Center * Resolve<RunRulesData>().UnitScale;
            return TerrainHeightMath.Sample(Resolve<TerrainHeightGrid>(), position.x, position.y);
        }
    }

    public static float TerrainFootError
    {
        get
        {
            var grid = Resolve<TerrainHeightGrid>();
            var error = 0f;
            foreach (var view in Resolve<IReadOnlyList<MemberView>>())
            {
                if (view.gameObject.activeInHierarchy) { Check(view.transform.position); }
            }
            foreach (var view in Resolve<MonsterViewRegistry>().Views)
            {
                if (view.gameObject.activeInHierarchy) { Check(view.transform.position); }
            }
            return error;
            void Check(Vector3 foot)
            {
                error = Mathf.Max(error, Mathf.Abs(foot.y - TerrainHeightMath.Sample(grid, foot.x, foot.z)));
            }
        }
    }

    public static int SpawnFootVisibleCount => Resolve<WorldPresenter>().SpawnFootVisibleCount;
    public static int SpawnBodyVisibleCount => Resolve<WorldPresenter>().SpawnBodyVisibleCount;

    // Boundary fixture for terrain flow 9: offsets are preserved, rules still own motion.
    public static void PlaceTerrainPartyAtBoundary(int corner)
    {
        var party = Resolve<PartyModel>();
        var extent = Resolve<MonsterModel>().ViewHalfExtent;
        var rules = Resolve<RunRulesData>();
        var limit = rules.FieldCells / (2f * rules.UnitScale) - extent.x - extent.y;
        var signs = new[] { new Vector2(-1, -1), new Vector2(1, -1), new Vector2(1, 1), new Vector2(-1, 1) };
        var next = Vector2.Scale(signs[corner], new Vector2(limit * .75f, limit * .25f));
        var delta = next - party.Center;
        party.SetCenter(next);
        foreach (var member in party.MemberList)
        {
            member.Position += delta;
        }
        SetMove(0, 0);
    }

    public static void WriteTerrainSpawnReport(string path)
    {
        var height = 0f;
        foreach (var data in Resolve<MonsterRepository>().MonsterDataList)
        {
            height = Mathf.Max(height, Resolve<MonsterViewFactory>().GetHeadHeight(data));
        }
        System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path));
        System.IO.File.WriteAllText(System.IO.Path.ChangeExtension(path, "csv"), Resolve<WorldPresenter>().PlacementObservations);
        System.IO.File.WriteAllText(path, string.Format(System.Globalization.CultureInfo.InvariantCulture,
            "{{\"SpawnFootVisibleCount\":{0},\"SpawnBodyVisibleCount\":{1},\"maxMonsterHeadHeight\":{2},\"TerrainFootError\":{3},\"PartyGroundHeight\":{4}}}",
            SpawnFootVisibleCount, SpawnBodyVisibleCount, height, TerrainFootError, PartyGroundHeight));
    }

    public static string EffectFrameSequence(string key) => Resolve<CombatWorldPresenter>().GetFrameSequence(key);

    public static int EffectShownCount(string spriteName) => Resolve<CombatWorldPresenter>().GetShownCount(spriteName);

    public static int EffectVisibleCount(string spriteName)
    {
        var count = 0;
        var registry = Resolve<ProjectileViewRegistry>();
        foreach (var id in registry.ProjectileIds)
        {
            registry.TryGetProjectile(id, out var view);
            if (IsEffectVisible(view, spriteName) && IsProjectileAwayFromParty(view))
            {
                count++;
            }
        }

        foreach (var id in registry.BurstIds)
        {
            registry.TryGetBurst(id, out var view);
            if (IsEffectVisible(view, spriteName))
            {
                count++;
            }
        }

        return count;
    }

    public static bool IsEffectCaptureReady => EffectSampler != null && EffectSampler.IsCaptured;

    private static PlayableEffectSampler EffectSampler => Object.FindAnyObjectByType<PlayableEffectSampler>();

    public static bool IsBattleFxReady => Object.FindAnyObjectByType<PlayableBattleFxSampler>() != null
        && Object.FindAnyObjectByType<PlayableBattleFxSampler>().IsReady;
    public static void ArmBattleFxCapture()
    {
        var sampler = Object.FindAnyObjectByType<PlayableBattleFxSampler>();
        if (sampler == null)
        {
            sampler = new GameObject(nameof(PlayableBattleFxSampler)).AddComponent<PlayableBattleFxSampler>();
        }
        sampler.Arm();
    }

    public static bool PauseAtBattleEffects()
    {
        if (Wave < 3)
        {
            return false;
        }
        var keys = new HashSet<string>();
        var registry = Resolve<ProjectileViewRegistry>();
        foreach (var id in registry.BurstIds)
        {
            registry.TryGetBurst(id, out var view);
            if (view.gameObject.activeInHierarchy)
            {
                keys.Add(view.EffectKey);
            }
        }
        if (!keys.Contains("slash_sword") || !keys.Contains("hit_star")
            || !(keys.Contains("heal_area") || keys.Contains("pulse") || keys.Contains("ground_boom")))
        {
            return false;
        }
        Time.timeScale = 0;
        System.IO.Directory.CreateDirectory("output/hd2d-fx");
        System.IO.File.WriteAllText("output/hd2d-fx/battle-context.txt", $"wave={Wave} unityFrame={Time.frameCount} keys={string.Join(",",keys)}\n");
        return true;
    }

    public static CombatEffectClipData EffectClip(string key) => Resolve<CombatEffectData>().GetClip(key);
    public static Camera EffectCamera => Resolve<Camera>();
    public static bool IsEffectSequenceReady => Object.FindAnyObjectByType<PlayableEffectSequenceSampler>() != null
        && Object.FindAnyObjectByType<PlayableEffectSequenceSampler>().IsCaptured;
    public static bool EffectSequenceHasAllFrames => Object.FindAnyObjectByType<PlayableEffectSequenceSampler>().HasAllFrames;

    public static void ArmEffectSequence(string key, string directory, float seconds = .35f)
    {
        var sampler = Object.FindAnyObjectByType<PlayableEffectSequenceSampler>();
        if (sampler == null)
        {
            sampler = new GameObject(nameof(PlayableEffectSequenceSampler)).AddComponent<PlayableEffectSequenceSampler>();
        }
        sampler.Arm(key, directory, seconds);
    }

    public static void ArmEffectCapture(string spriteName, string evidenceDirectory)
    {
        var sampler = EffectSampler;
        if (sampler == null)
        {
            sampler = new GameObject(nameof(PlayableEffectSampler)).AddComponent<PlayableEffectSampler>();
        }

        sampler.Arm(spriteName, evidenceDirectory);
        Time.timeScale = 1f;
    }

    // 효과 확인용 장비 입력: 정비 중 전리품 지급 API → 기존 집기/드롭 입력. HP·쿨·피해는 직접 쓰지 않는다.
    public static void GrantAndEquipEffectTrialItem(string itemId, int member)
    {
        if (Screen != nameof(RunPhase.Prep))
        {
            throw new InvalidOperationException("[PlayableProbe] Effect trial equipment requires Prep");
        }

        var bagService = Resolve<BagService>();
        var bagModel = Resolve<BagModel>();
        bagService.AddLoot(new[] { itemId });
        var uid = bagModel.LastGrantedUidList[0];
        var rules = Resolve<BagRulesRepository>().Rules;
        for (var rotation = 0; rotation < 4; rotation++)
        {
            for (var y = 0; y < rules.BagHeight; y++)
            {
                for (var x = 0; x < rules.BagWidth; x++)
                {
                    if (bagService.CanPlace(bagModel, uid, member, x, y, rotation))
                    {
                        PressDrop(uid, rotation, BagTarget.Cell(member, x, y));
                        return;
                    }
                }
            }
        }

        throw new InvalidOperationException($"[PlayableProbe] No equipment space for {itemId}");
    }

    // 투사체가 발사자 몸과 겹친 첫 프레임 대신, 궤적을 읽을 수 있는 비행 중 프레임을 잡는다.
    private static bool IsProjectileAwayFromParty(IEffectFrameSource source)
    {
        var renderer = source.ActiveRenderer;
        var rules = Resolve<RunRulesData>();
        foreach (var member in Resolve<PartyModel>().MemberList)
        {
            var foot = IsoProjection.FloorToWorld(member.Position, rules.UnitScale, Resolve<TerrainHeightGrid>());
            var camera = Resolve<Camera>();
            var center = camera.WorldToViewportPoint(renderer.bounds.center);
            foot = camera.WorldToViewportPoint(foot);
            var unitPixels = camera.pixelHeight / (camera.orthographicSize * 2f * Const.PixelsPerUnit);
            if (Mathf.Abs(center.x - foot.x) < 24f * unitPixels / camera.pixelWidth
                && center.y > foot.y && center.y < foot.y + 52f * unitPixels / camera.pixelHeight)
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsEffectVisible(IEffectFrameSource source, string spriteName)
    {
        if (!source.IsShown)
        {
            return false;
        }
        var point = Resolve<Camera>().WorldToViewportPoint(source.ActiveRenderer.bounds.center);
        return (source.SourceSprite.name == spriteName + "(Clone)" || source.SourceSprite.name.StartsWith(Resolve<CombatEffectData>().GetClip(spriteName).Sprite + "_", StringComparison.Ordinal)) && source.Alpha > 0f
            && point.z > 0f && point.x > 0f && point.x < 1f && point.y > 0f && point.y < 1f;
    }

    // MarkFrameStats 뒤 그리기 호출·set-pass 95백분위(그림 넣기 I절), 표본 없으면 −1
    public static float RenderBatchesP95 => FrameSampler != null ? FrameSampler.BatchesP95 : -1f;
    public static float SetPassCallsP95 => FrameSampler != null ? FrameSampler.SetPassCallsP95 : -1f;

    // 프레임 표본을 새로 모은다
    public static void MarkFrameStats()
    {
        var sampler = FrameSampler;
        if (sampler == null)
        {
            sampler = new GameObject(nameof(PlayableFrameSampler)).AddComponent<PlayableFrameSampler>();
        }

        sampler.Mark();
    }

    [System.Serializable]
    private class PerfRecord
    {
        public string verdict = "NOT_JUDGED";
        public string budget_source = "r6 7단계 미정";
        public float batches_p95;
        public float set_pass_p95;
        public float frame_ms_p95;
        public int enemy_count;
        public string quality;
        public string pipeline;
    }

    public static void WritePerfRecord(string path)
    {
        var record = new PerfRecord
        {
            batches_p95 = RenderBatchesP95,
            set_pass_p95 = SetPassCallsP95,
            frame_ms_p95 = FrameMsP95SinceMark,
            enemy_count = EnemyCount,
            quality = QualitySettings.names[QualitySettings.GetQualityLevel()],
            pipeline = UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline.name,
        };
        System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path));
        System.IO.File.WriteAllText(path, JsonUtility.ToJson(record, true));
    }

    private static PlayableFrameSampler FrameSampler => Object.FindAnyObjectByType<PlayableFrameSampler>();

    public static void PressDepart() => Resolve<LobbyPresenter>().OnDepartClicked();

    public static void PressNextWave() => Resolve<PrepPresenter>().OnNextWaveClicked();

    public static void PressToLobby() => Resolve<ResultPresenter>().OnToLobbyClicked();

    // 다음 판 설정(게임의 시작 입력). 로비 출발이 이 값을 쓴다
    public static void UseRunSettings(int seed, float enemyCountScale) => Resolve<RunSettingsModel>().Set(seed, enemyCountScale);

    // 입력 Presenter와 같은 이동 명령(화면 기준 x 오른쪽, y 위)
    public static void SetMove(float x, float y) => Resolve<PartyMoveService>().SetMoveInput(new Vector2(x, y));

    // 조이스틱 포인터(화면 0~1 비율, 왼쪽 아래 0,0). EventSystem처럼 UI_JoystickView의 포인터 처리 메서드를 부른다
    public static void JoystickDown(float x, float y) => Resolve<UI_JoystickView>().OnPointerDown(CreatePointer(x, y));

    public static void JoystickDrag(float x, float y) => Resolve<UI_JoystickView>().OnDrag(CreatePointer(x, y));

    public static void JoystickUp()
    {
        var view = Resolve<UI_JoystickView>();
        view.OnPointerUp(CreatePointer(view.CurrentPosition.x / Resolve<Camera>().pixelWidth,
            view.CurrentPosition.y / Resolve<Camera>().pixelHeight));
    }

    public static void PressFormation(string id) => Resolve<FormationPresenter>().OnFormationClicked(id);

    // 지금 중심·대원 좌표·생존을 기억한다(PartyDelta*, PartyMovedSinceMark, MaxDeadMoved의 기준)
    public static void MarkPositions()
    {
        _markCenter = Resolve<PartyModel>().Center;
        _markPositionList.Clear();
        _markAliveList.Clear();
        foreach (var member in Resolve<PartyModel>().MemberList)
        {
            _markPositionList.Add(member.Position);
            _markAliveList.Add(member.IsAlive);
        }
    }

    public static void StartBot(BotMode mode)
    {
        var bot = Object.FindAnyObjectByType<PlayableBot>();
        if (bot == null)
        {
            bot = new GameObject(nameof(PlayableBot)).AddComponent<PlayableBot>();
        }

        bot.SetMode(mode);
    }

    public static void StopBot()
    {
        var bot = Object.FindAnyObjectByType<PlayableBot>();
        if (bot != null)
        {
            // Move가 빠지면 0을 한 번 보낸다. 같은 식에서 바로 StartBot을 부르면 새 봇을 만들도록 즉시 지운다
            bot.SetMode(BotMode.None);
            Object.DestroyImmediate(bot.gameObject);
        }
    }

    private static PlayableMetricsRecorder Recorder => Object.FindAnyObjectByType<PlayableMetricsRecorder>();

    private static PlayableMetricsRecorder GetOrCreateRecorder()
    {
        var recorder = Recorder;
        return recorder != null ? recorder : new GameObject(nameof(PlayableMetricsRecorder)).AddComponent<PlayableMetricsRecorder>();
    }

    internal static T Resolve<T>()
    {
        var scope = Object.FindAnyObjectByType<GameSceneLifetimeScope>();
        if (scope == null || scope.Container == null)
        {
            throw new InvalidOperationException("[PlayableProbe] GameSceneLifetimeScope not found (Play Main.unity first)");
        }

        return scope.Container.Resolve<T>();
    }

    private static PointerEventData CreatePointer(float x, float y)
    {
        return new PointerEventData(EventSystem.current)
        {
            pointerId = ProbePointerId,
            // CLI calls run outside the player loop, where Screen can report the editor dock size.
            position = new Vector2(x * Resolve<Camera>().pixelWidth, y * Resolve<Camera>().pixelHeight),
        };
    }
}

// 판정용 자동 플레이. Move(F2)·Equip(F5)·AutoNext를 한다. 없는 동작이 들어오면 경고를 남기고 무시한다.
public class PlayableBot : MonoBehaviour
{
    private const PlayableProbe.BotMode SupportedModes =
        PlayableProbe.BotMode.Move | PlayableProbe.BotMode.AutoNext | PlayableProbe.BotMode.Equip;
    // 들판 가운데(원점, FieldView.Build)를 도는 원의 반지름(바닥 단위, F2 H절)
    private const float MoveCircleRadius = 600f;
    // 가운데에서 이 거리 미만이면 오른쪽으로 간다
    private const float MoveCenterDeadZone = 1f;
    // 바닥 방향 → 화면 방향(아이소 바닥은 세로가 절반으로 그려진다)
    private const float FloorToScreenVertical = 0.5f;

    public PlayableProbe.BotMode Mode => _mode;

    private PlayableProbe.BotMode _mode;
    // 자동 장착을 마친 정비 화면(판 번호, 끝난 웨이브). 한 정비 화면에 한 번
    private (int, int) _equippedPrep = (-1, -1);

    public void SetMode(PlayableProbe.BotMode mode)
    {
        var unsupported = mode & ~SupportedModes;
        if (unsupported != PlayableProbe.BotMode.None)
        {
            GameLogger.LogWarning($"[PlayableBot] Not implemented yet, ignored: {unsupported}");
        }

        var isMoveDropped = (_mode & PlayableProbe.BotMode.Move) != 0 && (mode & PlayableProbe.BotMode.Move) == 0;
        _mode = mode & SupportedModes;
        if (isMoveDropped)
        {
            PlayableProbe.SetMove(0f, 0f);
        }
    }

    private void Update()
    {
        var screen = PlayableProbe.Screen;
        // 자동 장착(F5 E절): 정비 화면에 들어온 뒤 한 번, AutoNext보다 먼저
        if ((_mode & PlayableProbe.BotMode.Equip) != 0 && screen == nameof(RunPhase.Prep))
        {
            EquipOnce();
        }

        if ((_mode & PlayableProbe.BotMode.Move) != 0 && screen == nameof(RunPhase.Battle))
        {
            SendCircleMove();
        }

        if ((_mode & PlayableProbe.BotMode.AutoNext) != 0 && screen == nameof(RunPhase.Prep))
        {
            PlayableProbe.PressNextWave();
        }
    }

    // 시계 방향 접선에 원 쪽으로 돌아가는 성분을 섞은 바닥 방향을 화면 방향으로 바꿔 세기 1로 넣는다
    private static void SendCircleMove()
    {
        var center = new Vector2(PlayableProbe.PartyX, PlayableProbe.PartyY);
        var distance = center.magnitude;
        var floorDirection = Vector2.right;
        if (distance >= MoveCenterDeadZone)
        {
            var outward = center / distance;
            var clockwise = new Vector2(outward.y, -outward.x);
            var correction = Mathf.Clamp((distance - MoveCircleRadius) / MoveCircleRadius, -1f, 1f);
            floorDirection = (clockwise - outward * correction).normalized;
        }

        var screenDirection = new Vector2(floorDirection.x, floorDirection.y * FloorToScreenVertical).normalized;
        PlayableProbe.SetMove(screenDirection.x, screenDirection.y);
    }

    // 부활 → 합성 → 점수 최대 빈 칸 놓기. 계획은 AutoEquipPlanner, 실행은 BagPresenter 처리 메서드
    private void EquipOnce()
    {
        var prep = (PlayableProbe.RunCount, PlayableProbe.CompletedWave);
        if (_equippedPrep == prep)
        {
            return;
        }

        _equippedPrep = prep;
        var planner = PlayableProbe.Resolve<AutoEquipPlanner>();
        foreach (var member in planner.GetReviveMemberList())
        {
            PlayableProbe.PressRevive(member);
        }

        foreach (var step in planner.Plan())
        {
            if (step.Kind == BagPlanStepKind.Merge)
            {
                PlayableProbe.PressDrop(step.Uid, 0, PlayableProbe.GetMergeTarget(step.TargetUid));
                continue;
            }

            PlayableProbe.PressDrop(step.Uid, step.Rotation, BagTarget.Cell(step.Member, step.X, step.Y));
        }
    }
}
// 판정용 프레임 시간·적 수·그리기 호출 표본(F6 기획서 H절 MarkFrameStats, 그림 넣기 기획서 I절). 표시 뒤 매 프레임
// Time.unscaledDeltaTime, 살아 있는 몬스터 수, 직전 프레임의 UnityStats.drawCalls(Unity 6.4에는 batches가 없다)·setPassCalls를 모은다
public class PlayableFrameSampler : MonoBehaviour
{
    private const float Percentile = 0.95f;

    private readonly List<float> _frameMsList = new();
    private readonly List<float> _batchesList = new();
    private readonly List<float> _setPassCallsList = new();
    private readonly List<float> _sortedList = new();
    private bool _isMarked;

    public int SampleCount => _frameMsList.Count;
    public int EnemyPeak { get; private set; }
    public float FrameMsP95 => GetP95(_frameMsList);
    public float BatchesP95 => GetP95(_batchesList);
    public float SetPassCallsP95 => GetP95(_setPassCallsList);

    public void Mark()
    {
        _frameMsList.Clear();
        _batchesList.Clear();
        _setPassCallsList.Clear();
        EnemyPeak = 0;
        _isMarked = true;
    }

    private void Update()
    {
        if (!_isMarked || PlayableProbe.Screen != nameof(RunPhase.Battle))
        {
            return;
        }

        _frameMsList.Add(Time.unscaledDeltaTime * 1000f);
        _batchesList.Add(UnityEditor.UnityStats.drawCalls);
        _setPassCallsList.Add(UnityEditor.UnityStats.setPassCalls);
        EnemyPeak = Mathf.Max(EnemyPeak, PlayableProbe.EnemyCount);
    }

    // 가장 가까운 순위(ceil(n × p)번째), 표본 없으면 −1(PlayableMetricsRecorder와 같은 백분위)
    private float GetP95(List<float> sampleList)
    {
        if (sampleList.Count == 0)
        {
            return -1f;
        }

        _sortedList.Clear();
        _sortedList.AddRange(sampleList);
        _sortedList.Sort();
        return _sortedList[Mathf.Clamp(Mathf.CeilToInt(_sortedList.Count * Percentile) - 1, 0, _sortedList.Count - 1)];
    }
}

// 판정용 예고 그림 표본(쓰러짐·예고 T2·T3). 표시 뒤 매 프레임 산 몬스터의 예고 그림 표시를 보고, 몬스터마다 표시 시작 시각(게임 시간)을 적는다.
// 예고가 끝나면(평소 그림으로 돌아오거나 몬스터가 사라짐) 그 그림 이름별로 표시 시간·끝난 수·돌아온 수를 모은다. 흔들림은 표시 중 몸 x 오프셋(픽셀)의 종류.
public class PlayableWarningSampler : MonoBehaviour
{
    private const string CloneSuffix = "(Clone)";

    private readonly Dictionary<int, (string spriteName, float startTime)> _shownDict = new();
    private readonly Dictionary<string, (int ended, int returned, float maxSeconds)> _resultDict = new();
    private readonly Dictionary<string, HashSet<int>> _shakeOffsetSetDict = new();
    private readonly HashSet<int> _seenIdSet = new();
    private readonly List<int> _endedIdList = new();
    private readonly System.Text.StringBuilder _logBuilder = new();
    private bool _isMarked;
    private float _pauseAtUnscaledTime = -1f;

    public string Log => _logBuilder.ToString();

    public void AdvanceGameTime(float seconds)
    {
        _pauseAtUnscaledTime = Time.unscaledTime + seconds;
        Time.timeScale = 1f;
    }

    public void Mark()
    {
        _shownDict.Clear();
        _resultDict.Clear();
        _shakeOffsetSetDict.Clear();
        _logBuilder.Clear();
        _isMarked = true;
    }

    public int GetEndedCount(string spriteName) => _resultDict.TryGetValue(spriteName, out var result) ? result.ended : 0;
    public int GetReturnedCount(string spriteName) => _resultDict.TryGetValue(spriteName, out var result) ? result.returned : 0;
    public float GetMaxSeconds(string spriteName) => _resultDict.TryGetValue(spriteName, out var result) ? result.maxSeconds : -1f;
    public int GetShakeOffsetsSeen(string spriteName) =>
        _shakeOffsetSetDict.TryGetValue(spriteName, out var offsetSet) ? offsetSet.Count : 0;

    private void Update()
    {
        if (_pauseAtUnscaledTime >= 0f && Time.unscaledTime >= _pauseAtUnscaledTime)
        {
            Time.timeScale = 0f;
            _pauseAtUnscaledTime = -1f;
        }

        if (!_isMarked)
        {
            return;
        }

        // 전투가 아니면 몬스터가 모두 치워졌다 — 표시 중이던 예고는 지금 끝난 것으로 센다(다음 전투까지 미루면 정비 시간이 더해진다)
        if (PlayableProbe.Screen != nameof(RunPhase.Battle))
        {
            EndAllShown();
            return;
        }

        var registry = PlayableProbe.Resolve<MonsterViewRegistry>();
        _seenIdSet.Clear();
        foreach (var monster in PlayableProbe.Resolve<MonsterModel>().MonsterList)
        {
            if (!monster.IsAlive || !registry.TryGet(monster.Id, out var view))
            {
                continue;
            }

            _seenIdSet.Add(monster.Id);
            var isShown = _shownDict.TryGetValue(monster.Id, out var shown);
            if (view.IsWarning)
            {
                var spriteName = view.WarningSpriteName.Replace(CloneSuffix, string.Empty);
                if (!isShown)
                {
                    _shownDict.Add(monster.Id, (spriteName, Time.time));
                }

                if (!_shakeOffsetSetDict.TryGetValue(spriteName, out var offsetSet))
                {
                    offsetSet = new HashSet<int>();
                    _shakeOffsetSetDict.Add(spriteName, offsetSet);
                }

                offsetSet.Add(Mathf.RoundToInt(view.BodyOffset.x * Const.PixelsPerUnit));
            }
            else if (isShown)
            {
                EndWarning(monster.Id, shown, true);
            }
        }

        _endedIdList.Clear();
        foreach (var id in _shownDict.Keys)
        {
            if (!_seenIdSet.Contains(id))
            {
                _endedIdList.Add(id);
            }
        }

        foreach (var id in _endedIdList)
        {
            EndWarning(id, _shownDict[id], false);
        }
    }

    private void EndAllShown()
    {
        _endedIdList.Clear();
        _endedIdList.AddRange(_shownDict.Keys);
        foreach (var id in _endedIdList)
        {
            EndWarning(id, _shownDict[id], false);
        }
    }

    private void EndWarning(int monsterId, (string spriteName, float startTime) shown, bool isReturned)
    {
        _shownDict.Remove(monsterId);
        _resultDict.TryGetValue(shown.spriteName, out var result);
        result.ended++;
        result.returned += isReturned ? 1 : 0;
        result.maxSeconds = Mathf.Max(result.maxSeconds, Time.time - shown.startTime);
        _resultDict[shown.spriteName] = result;
        _logBuilder.AppendLine(
            $"{shown.spriteName} monster={monsterId} start={shown.startTime:F3} end={Time.time:F3} shown={Time.time - shown.startTime:F3} returned={isReturned}");
    }
}
// 짧은 효과는 CLI 폴링 주기와 발동 주기가 겹칠 수 있어 프레임 끝에서 포착한다.
[DefaultExecutionOrder(10000)]
public class PlayableEffectSampler : MonoBehaviour
{
    public bool IsCaptured { get; private set; }
    private string _spriteName;
    private string _evidenceDirectory;

    public void Arm(string spriteName, string evidenceDirectory)
    {
        _spriteName = spriteName;
        _evidenceDirectory = evidenceDirectory;
        IsCaptured = false;
    }

    private void LateUpdate()
    {
        if (_spriteName == null || IsCaptured)
        {
            return;
        }

        var count = PlayableProbe.EffectVisibleCount(_spriteName);
        if (count < 1)
        {
            return;
        }

        Time.timeScale = 0f;
        IsCaptured = true;
        var directory = System.IO.Path.GetFullPath(_evidenceDirectory);
        System.IO.Directory.CreateDirectory(directory);
        System.IO.File.AppendAllText(System.IO.Path.Combine(directory, _spriteName + ".txt"),
            $"utc={DateTime.UtcNow:O} frame={Time.frameCount} gameTime={Time.time:F4} visible={count} shown={PlayableProbe.EffectShownCount(_spriteName)}\n");
    }
}
// 실제 게임에서 첫 프레임을 기다린 뒤 1/60s 간격 0.35s를 프레임 끝에서 기록한다.
[DefaultExecutionOrder(11000)]
public class PlayableEffectSequenceSampler : MonoBehaviour
{
    public bool IsCaptured { get; private set; }
    public bool HasAllFrames { get; private set; }
    private string _key;
    private string _prefix;
    private string _directory;
    private int _frameCount;
    private int _ticks;
    private float _oldCaptureDelta;
    private float _oldScale;
    private bool _isArmed;
    private readonly HashSet<int> _seenSet = new();
    private readonly List<int> _seenOrder = new();
    private readonly Dictionary<Sprite, (bool binary, int core)> _pixelDict = new();

    public void Arm(string key, string directory, float seconds)
    {
        StopAllCoroutines();
        RestoreClock();
        var clip = PlayableProbe.EffectClip(key);
        _key = key;
        _prefix = clip.Sprite + "_";
        _frameCount = clip.Frames;
        _ticks = Mathf.RoundToInt(seconds * 60);
        _directory = System.IO.Path.GetFullPath(directory);
        System.IO.Directory.CreateDirectory(_directory);
        _oldCaptureDelta = Time.captureDeltaTime;
        _oldScale = Time.timeScale;
        _isArmed = true;
        IsCaptured = false;
        HasAllFrames = false;
        _seenSet.Clear();
        _seenOrder.Clear();
        Time.captureDeltaTime = 1f / 60f;
        Time.timeScale = 1;
        StartCoroutine(Capture());
    }

    private System.Collections.IEnumerator Capture()
    {
        IEffectFrameSource selected = null;
        var started = false;
        var ended = false;
        var rectangle = new RectInt();
        var recorded = 0;
        var log = new System.Text.StringBuilder("sample,unityFrame,gameTime,frame,sprite,alpha,glowCore,sourceAlphaBinary,visible\n");
        var camera = PlayableProbe.EffectCamera;
        while (_isArmed && recorded < _ticks)
        {
            yield return new WaitForEndOfFrame();
            if (!started)
            {
                foreach (var candidate in PlayableProbe.EffectSources())
                {
                    if (!Matches(candidate) || candidate.FrameNumber != 1 || !HasEnoughFlight(candidate))
                    {
                        continue;
                    }
                    var viewport = camera.WorldToViewportPoint(candidate.ActiveRenderer.bounds.center);
                    if (viewport.z <= 0 || viewport.x <= .05f || viewport.x >= .95f || viewport.y <= .1f || viewport.y >= .9f)
                    {
                        continue;
                    }
                    selected = candidate;
                    started = true;
                    var center = camera.WorldToScreenPoint(candidate.ActiveRenderer.bounds.center);
                    var width = Mathf.Min(640, Screen.width);
                    var height = Mathf.Min(480, Screen.height);
                    rectangle = new RectInt(Mathf.Clamp(Mathf.RoundToInt(center.x - width / 2f), 0, Screen.width - width),
                        Mathf.Clamp(Mathf.RoundToInt(center.y - height / 2f), 0, Screen.height - height), width, height);
                    break;
                }
                if (!started)
                {
                    continue;
                }
            }
            var visible = !ended && Matches(selected);
            if (!visible)
            {
                ended = true;
            }
            var index = visible ? selected.FrameNumber : 0;
            var source = visible ? ReadPixels(selected.SourceSprite) : (binary: true, core: 0);
            var coreIntensity = visible && selected.ActiveRenderer.sharedMaterial.HasProperty("_CoreIntensity") ? selected.ActiveRenderer.sharedMaterial.GetFloat("_CoreIntensity") : 1;
            var core = coreIntensity > 2 ? source.core : 0;
            if (visible)
            {
                _seenSet.Add(index);
                if (_seenOrder.Count == 0 || _seenOrder[_seenOrder.Count - 1] != index)
                {
                    _seenOrder.Add(index);
                }
            }
            var alpha = visible ? Mathf.RoundToInt(selected.Alpha * 255) : 0;
            log.AppendLine($"{recorded},{Time.frameCount},{Time.time:F6},{index},{(visible ? selected.SourceSprite.name : "none")},{alpha},{core},{source.binary},{(visible ? 1 : 0)}");
            var full = ScreenCapture.CaptureScreenshotAsTexture();
            var crop = new Texture2D(rectangle.width, rectangle.height, TextureFormat.RGBA32, false);
            crop.SetPixels(full.GetPixels(rectangle.x, rectangle.y, rectangle.width, rectangle.height));
            crop.Apply();
            System.IO.File.WriteAllBytes(System.IO.Path.Combine(_directory, $"{recorded:D3}.png"), crop.EncodeToPNG());
            Object.Destroy(full);
            Object.Destroy(crop);
            recorded++;
        }
        HasAllFrames = _seenOrder.Take(_frameCount).SequenceEqual(Enumerable.Range(1, _frameCount));
        System.IO.File.WriteAllText(System.IO.Path.Combine(_directory, "frames.csv"), log.ToString());
        System.IO.File.WriteAllText(System.IO.Path.Combine(_directory, "state.txt"),
            $"key={_key} samples={recorded} expectedFrames={_frameCount} seen={string.Join(",", _seenSet.OrderBy(i=>i))} hasAllFrames={HasAllFrames}\n");
        RestoreClock();
        Time.timeScale = 0f; // Keep the measured frame stable until the next sampler is armed.
        IsCaptured = true;
    }

    // A real projectile that hits its target before frame N cannot evidence a full clip.
    // Select a new flight with enough remaining range; never extend its actual lifetime.
    private bool HasEnoughFlight(IEffectFrameSource source)
    {
        if (source is not ProjectileView projectileView || _frameCount <= 1) { return true; }
        var registry = PlayableProbe.Resolve<ProjectileViewRegistry>();
        var clip = PlayableProbe.EffectClip(_key);
        var seconds = (_frameCount - 1f) / clip.Fps + 1f / 30f;
        foreach (var projectile in PlayableProbe.Resolve<CombatModel>().ProjectileList)
        {
            if (!registry.TryGetProjectile(projectile.Id, out var view) || view != projectileView) { continue; }
            var remaining = projectile.Range - projectile.Traveled;
            if (projectile.IsExplosion) { remaining = Mathf.Min(remaining, Vector2.Distance(projectile.Position, projectile.TargetPosition)); }
            return remaining > projectile.Speed * seconds;
        }
        return false;
    }

    private bool Matches(IEffectFrameSource source) => source != null && source.IsShown
        && source.SourceSprite != null && source.SourceSprite.name.StartsWith(_prefix, StringComparison.Ordinal);

    private static int FrameNumber(Sprite sprite)
    {
        var name = sprite.name.Replace("(Clone)", string.Empty);
        return int.Parse(name.Substring(name.LastIndexOf('_') + 1));
    }

    private (bool binary, int core) ReadPixels(Sprite sprite)
    {
        if (_pixelDict.TryGetValue(sprite, out var info))
        {
            return info;
        }
        var rect = sprite.textureRect;
        var texture = sprite.texture;
        var target = RenderTexture.GetTemporary((int)rect.width, (int)rect.height, 0, RenderTextureFormat.ARGB32);
        var previous = RenderTexture.active;
        var readback = new Texture2D(target.width, target.height, TextureFormat.RGBA32, false);
        try
        {
            Graphics.Blit(texture, target, new Vector2(rect.width / texture.width, rect.height / texture.height), new Vector2(rect.x / texture.width, rect.y / texture.height));
            RenderTexture.active = target;
            readback.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
            readback.Apply();
            var binary = true;
            var core = 0;
            foreach (var color in readback.GetPixels32())
            {
                binary &= color.a == 0 || color.a == 255;
                var max = Mathf.Max(color.r, Mathf.Max(color.g, color.b));
                var min = Mathf.Min(color.r, Mathf.Min(color.g, color.b));
                if (color.a == 255 && max >= .96f * 255 && (max - min) / Mathf.Max(1f, max) < .12f)
                {
                    core++;
                }
            }
            info = (binary, core);
            _pixelDict.Add(sprite, info);
            return info;
        }
        finally
        {
            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(target);
            Object.Destroy(readback);
        }
    }

    private void RestoreClock()
    {
        if (_isArmed)
        {
            Time.captureDeltaTime = _oldCaptureDelta;
            Time.timeScale = _oldScale;
            _isArmed = false;
        }
    }
    private void OnDestroy() => RestoreClock();
}

[DefaultExecutionOrder(12000)]
public class PlayableBattleFxSampler : MonoBehaviour
{
    public bool IsReady { get; private set; }
    private bool _isArmed;
    public void Arm()
    {
        IsReady = false;
        _isArmed = true;
        Time.timeScale = 1;
    }
    private void LateUpdate()
    {
        if (_isArmed && PlayableProbe.PauseAtBattleEffects())
        {
            IsReady = true;
            _isArmed = false;
        }
    }
}
#endif

// Eight consecutive simulation-frame captures; uses the normal movement input/service.
public class PlayableTerrainWalkSampler : MonoBehaviour
{
    public bool IsCaptured { get; private set; }
    private float _oldCaptureDelta;
    public void Arm(string directory)
    {
        _oldCaptureDelta = Time.captureDeltaTime;
        Time.captureDeltaTime = 1f / 30f;
        Time.timeScale = 1f;
        PlayableProbe.SetMove(0, -1);
        StartCoroutine(Capture(directory));
    }
    private System.Collections.IEnumerator Capture(string directory)
    {
        System.IO.Directory.CreateDirectory(directory);
        var log = new System.Text.StringBuilder("frame,partyX,partyZ,height,footError,cameraError\n");
        for (var i = 0; i < 8; i++)
        {
            yield return new WaitForEndOfFrame();
            var texture = ScreenCapture.CaptureScreenshotAsTexture();
            System.IO.File.WriteAllBytes(System.IO.Path.Combine(directory, $"walk-{i + 1:00}.png"), texture.EncodeToPNG());
            Destroy(texture);
            var p = PlayableProbe.Resolve<PartyModel>().Center * PlayableProbe.Resolve<RunRulesData>().UnitScale;
            log.AppendLine(string.Format(System.Globalization.CultureInfo.InvariantCulture, "{0},{1},{2},{3},{4},{5}", i + 1, p.x, p.y,
                PlayableProbe.PartyGroundHeight, PlayableProbe.TerrainFootError, PlayableProbe.CameraError));
        }
        System.IO.File.WriteAllText(System.IO.Path.Combine(directory, "walk.csv"), log.ToString());
        PlayableProbe.SetMove(0, 0);
        Time.captureDeltaTime = _oldCaptureDelta;
        Time.timeScale = 0f;
        IsCaptured = true;
    }
    private void OnDestroy() { Time.captureDeltaTime = _oldCaptureDelta; }
}

// CLI latency must not turn four 7.5-second intervals into a longer observation.
[DefaultExecutionOrder(20000)]
public class PlayableTerrainSpawnSampler : MonoBehaviour
{
    public bool IsCaptured { get; private set; }
    private float _elapsed;
    private float _oldCaptureDelta;
    public void Arm(int corner)
    {
        PlayableProbe.PlaceTerrainPartyAtBoundary(corner);
        _elapsed = 0;
        if (corner == 0) { PlayableProbe.Resolve<WorldPresenter>().BeginPlacementObservation(); }
        IsCaptured = false;
        _oldCaptureDelta = Time.captureDeltaTime;
        Time.captureDeltaTime = 1f / 30f;
        Time.timeScale = 1;
    }
    private void LateUpdate()
    {
        if (IsCaptured || Time.timeScale == 0) { return; }
        _elapsed += Time.deltaTime;
        if (_elapsed < 7.5f - .0001f) { return; }
        Time.timeScale = 0;
        Time.captureDeltaTime = _oldCaptureDelta;
        IsCaptured = true;
    }
    private void OnDestroy() { Time.captureDeltaTime = _oldCaptureDelta; }
}
