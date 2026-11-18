using System;
using System.Collections.Generic;
using R3;

// 전투 실행 상태(기획서 데이터 스키마 "실행 중 상태", 저장 안 함). 1/30초마다 바뀌는 값이라 R3로 감싸지 않고
// Presenter가 프레임마다 읽는다. 부활 수만 정비 화면이 다시 그리도록 R3로 노출한다. 쓰는 쪽: CombatService만.
public class CombatModel : IDisposable
{
    private readonly List<MemberCombatState> _memberStateList = new();
    private readonly List<ProjectileState> _projectileList = new();
    private readonly List<BurstState> _burstList = new();
    private readonly Dictionary<string, int> _fireCountDict = new();
    private readonly ReactiveProperty<int> _reviveCount = new(0);

    // 대원 번호 순(PartyModel.MemberList와 같은 순서)
    public IReadOnlyList<MemberCombatState> MemberStateList => _memberStateList;
    public IReadOnlyList<ProjectileState> ProjectileList => _projectileList;
    public IReadOnlyList<BurstState> BurstList => _burstList;
    public ReadOnlyReactiveProperty<int> ReviveCount => _reviveCount;
    // 마지막으로 부활한 대원 번호, 없으면 −1
    public int LastRevivedMember { get; private set; } = -1;
    public int NextVisualId { get; private set; }

    public void SetMemberStates(IEnumerable<MemberCombatState> memberStates)
    {
        _memberStateList.Clear();
        _memberStateList.AddRange(memberStates);
    }

    public int GetFireCount(string itemId) => _fireCountDict.TryGetValue(itemId, out var count) ? count : 0;

    public void AddFireCount(string itemId) => _fireCountDict[itemId] = GetFireCount(itemId) + 1;

    public void ClearFireCounts() => _fireCountDict.Clear();

    public void SetRevive(int reviveCount, int lastRevivedMember)
    {
        LastRevivedMember = lastRevivedMember;
        _reviveCount.Value = reviveCount;
    }

    // 투사체·고리 그림 번호(판이 바뀌어도 이어서 늘린다 — 지난 판 그림이 등록부에 남아 있을 수 있다)
    public int TakeVisualId() => ++NextVisualId;

    public void AddProjectile(ProjectileState projectile) => _projectileList.Add(projectile);

    public void RemoveDoneProjectiles() => _projectileList.RemoveAll(projectile => projectile.IsDone);

    public void AddBurst(BurstState burst) => _burstList.Add(burst);

    public void RemoveEndedBursts() => _burstList.RemoveAll(burst => burst.RemainingSeconds <= 0f);

    public void ClearProjectiles()
    {
        _projectileList.Clear();
        _burstList.Clear();
    }

    public void Dispose() => _reviveCount.Dispose();
}

// 대원 하나의 전투 상태
public class MemberCombatState
{
    private readonly List<BattleItemSlot> _slotList = new();
    private readonly List<string> _equippedItemIdList = new();

    // 대원 전체 능력치(B3, 발동 아이템 없음)
    public CombatStats Stats { get; set; }
    public MemberAttackMotion AttackMotion { get; } = new();
    public List<PendingCombatAction> PendingActionList { get; } = new();
    // 주기 보호막 채움 값(B6, 상한 Stats.Shield와 따로)
    public float ShieldRefill { get; set; }
    // 나눔 추가 효과가 켜져 있는가(B6)
    public bool HasShare { get; set; }
    public float HealClock { get; set; }
    public float ShieldClock { get; set; }
    public float ShareClock { get; set; }
    // 나눔 제공자 대원 번호, 없으면 −1
    public int ShareProvider { get; set; } = -1;
    public float SharePeriod { get; set; }
    // 장착 순서 = 아이템 칸 번호
    public IReadOnlyList<BattleItemSlot> SlotList => _slotList;
    // 칸을 만들 때의 장착 목록(웨이브를 열 때 바뀌었는지 비교)
    public IReadOnlyList<string> EquippedItemIdList => _equippedItemIdList;

    public void SetSlots(IReadOnlyList<string> equippedItemIdList, IEnumerable<BattleItemSlot> slots)
    {
        _equippedItemIdList.Clear();
        _equippedItemIdList.AddRange(equippedItemIdList);
        _slotList.Clear();
        _slotList.AddRange(slots);
    }

    public void ResetClocks()
    {
        AttackMotion.Stop();
        PendingActionList.Clear();
        HealClock = 0f;
        ShieldClock = 0f;
        ShareClock = 0f;
        ShareProvider = -1;
        SharePeriod = 0f;
    }
}

// 아이템 칸 하나
public class BattleItemSlot
{
    public ItemData Item { get; }
    // 발동 쿨(발동 아이템)
    public float RemainingCooldown { get; set; }
    // 파동 쿨(추가 효과 pulse, B6). 발동 쿨과 따로 돈다
    public float PulseCooldown { get; set; }
    // 마지막 발동 주기(쿨타임 막대의 분모)
    public float LastPeriod { get; set; }
    // "발동!" 표시 남은 초
    public float FiredSeconds { get; set; }
    // 이 발동 아이템을 위한 능력치(B3). 지속 아이템은 대원 전체 값
    public CombatStats Stats { get; set; }
    // 등급 배수(B2)
    public float GradeMultiplier { get; set; }
    // 추가 효과 파동이 적용되는 칸(B6, 쿨이 도는 지속 칸)
    public bool IsPulse { get; set; }
    // 하단 칸에 보일 남은 쿨: 지속 아이템은 파동 쿨, 발동 아이템은 발동 쿨
    public float DisplayCooldown => Item.Mode == ItemMode.Passive ? PulseCooldown : RemainingCooldown;

    public BattleItemSlot(ItemData item)
    {
        Item = item;
    }

    public void ResetCooldown()
    {
        RemainingCooldown = 0f;
        PulseCooldown = 0f;
        LastPeriod = 0f;
        FiredSeconds = 0f;
    }
}

// 발동 때 확정한 능력치·방향·대상. 타격 전 쓰러지거나 웨이브가 끝나면 취소한다.
public class PendingCombatAction
{
    public BattleItemSlot Slot { get; }
    public float Range { get; }
    public float Power { get; }
    public UnityEngine.Vector2 Direction { get; }
    public Monster Target { get; }
    public List<PartyMember> HealTargetList { get; }

    public PendingCombatAction(BattleItemSlot slot, float range, float power, UnityEngine.Vector2 direction,
        Monster target, List<PartyMember> healTargetList)
    {
        Slot = slot;
        Range = range;
        Power = power;
        Direction = direction;
        Target = target;
        HealTargetList = healTargetList;
    }
}
