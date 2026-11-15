#if UNITY_EDITOR
using System;
using UnityEngine;
using VContainer;
using Object = UnityEngine.Object;

// 플레이 가능 판정용 진입점(기획서 H절, .ai/rules/playable-check.md). 판정 경로(tools/playable/*.json)의 식이 부른다.
// 장면 LifetimeScope 컨테이너에서 모델·Presenter를 꺼내 읽고, 버튼은 Presenter의 버튼 처리 메서드(onClick과 같은 길)를 부른다.
// 승패·단계를 써 넣지 않는다. 게임 코드는 이 클래스를 모른다.
public static class PlayableProbe
{
    private static int _lastPlacedMember = -1;
    private static string _lastPlacedItemId;

    [Flags]
    public enum BotMode
    {
        None = 0,
        // 조이스틱 입력과 같은 이동 명령(F2가 채운다)
        Move = 1,
        // 보관함 전리품 자동 장착(F5가 채운다)
        Equip = 2,
        // 정비 화면이면 다음 웨이브를 누른다
        AutoNext = 4,
    }

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

    public static void PressDepart() => Resolve<LobbyPresenter>().OnDepartClicked();

    public static void PressNextWave() => Resolve<PrepPresenter>().OnNextWaveClicked();

    public static void PressToLobby() => Resolve<ResultPresenter>().OnToLobbyClicked();

    // 다음 판 설정(게임의 시작 입력). 로비 출발이 이 값을 쓴다
    public static void UseRunSettings(int seed, float enemyCountScale) => Resolve<RunSettingsModel>().Set(seed, enemyCountScale);

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
            Object.Destroy(bot.gameObject);
        }
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
}

// 판정용 자동 플레이. F1은 AutoNext만 한다. 없는 동작(Move·Equip)이 들어오면 경고를 남기고 무시한다.
public class PlayableBot : MonoBehaviour
{
    private const PlayableProbe.BotMode SupportedModes = PlayableProbe.BotMode.AutoNext | PlayableProbe.BotMode.Equip;

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

        _mode = mode & SupportedModes;
    }

    private void Update()
    {
        // 자동 장착(F5 E절): 정비 화면에 들어온 뒤 한 번, AutoNext보다 먼저
        if ((_mode & PlayableProbe.BotMode.Equip) != 0 && PlayableProbe.Screen == nameof(RunPhase.Prep))
        {
            EquipOnce();
        }

        if ((_mode & PlayableProbe.BotMode.AutoNext) == 0)
        {
            return;
        }

        if (PlayableProbe.Screen == nameof(RunPhase.Prep))
        {
            PlayableProbe.PressNextWave();
        }
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
#endif
