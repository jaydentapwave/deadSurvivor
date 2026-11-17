using System.Collections.Generic;
using UnityEngine;

// 자동 전투(기획서 B5~B7, 예전 DSGame.EquipmentCombat·Combat 투사체 부분). CombatModel을 쓰는 유일한 곳이고,
// 대원의 최대 HP·유효 방어·보호막과 판·웨이브 시작·부활의 HP도 여기서 쓴다(설계 브리핑 "필드별 작성 권한").
// 난수를 쓰지 않는다(B8). RunService.Step의 고정 간격 안에서만 돈다.
public class CombatService
{
    // 범위 발동 고리 표시 시간(기획서 F절)
    private const float BurstSeconds = 0.25f;
    // 점 고리(단일 회복 대상·연쇄 명중) 반경, 바닥 단위(예전 Burst 값)
    private const float PointBurstRadius = 25f;
    // 회복 시계 비교 여유(예전 1e-4)
    private const float ClockTolerance = 1e-4f;

    private readonly CombatModel _combatModel;
    private readonly PartyModel _partyModel;
    private readonly PartyService _partyService;
    private readonly MonsterModel _monsterModel;
    private readonly MonsterService _monsterService;
    private readonly ItemRepository _itemRepository;
    private readonly CharacterRepository _characterRepository;
    private readonly EquipmentStatService _equipmentStatService;
    private readonly CombatRulesData _rules;

    private readonly List<PartyMember> _healTargetList = new();
    private readonly HashSet<int> _chainHitIdSet = new();

    public CombatService(CombatModel combatModel, PartyModel partyModel, PartyService partyService, MonsterModel monsterModel,
        MonsterService monsterService, ItemRepository itemRepository, CharacterRepository characterRepository,
        EquipmentStatService equipmentStatService, CombatRulesRepository combatRulesRepository)
    {
        _combatModel = combatModel;
        _partyModel = partyModel;
        _partyService = partyService;
        _monsterModel = monsterModel;
        _monsterService = monsterService;
        _itemRepository = itemRepository;
        _characterRepository = characterRepository;
        _equipmentStatService = equipmentStatService;
        _rules = combatRulesRepository.Rules;
    }

    // 한 판 시작(B7): 칸을 장착 목록으로 새로(쿨 0), 발동 수·부활 기록 0, 최대 HP 계산 후 HP = 최대 HP
    public void ResetRun()
    {
        var memberList = _partyModel.MemberList;
        var stateList = new List<MemberCombatState>(memberList.Count);
        for (var i = 0; i < memberList.Count; i++)
        {
            var state = new MemberCombatState();
            RebuildSlots(state, memberList[i]);
            stateList.Add(state);
        }

        _combatModel.SetMemberStates(stateList);
        _combatModel.ClearFireCounts();
        _combatModel.SetRevive(0, -1);
        _combatModel.ClearProjectiles();
        for (var i = 0; i < memberList.Count; i++)
        {
            RefreshStats(i);
            memberList[i].Hp = memberList[i].MaxHp;
        }
    }

    // 웨이브를 열 때(B7): 장착 목록이 바뀐 대원만 칸을 새로, 능력치 다시 계산, 산 대원 보호막 = 시작 보호막, 시계 0
    public void PrepareWave()
    {
        var memberList = _partyModel.MemberList;
        for (var i = 0; i < memberList.Count; i++)
        {
            var member = memberList[i];
            var state = _combatModel.MemberStateList[i];
            if (!IsSameItems(state.EquippedItemIdList, member.EquippedItemIdList))
            {
                RebuildSlots(state, member);
            }

            RefreshStats(i);
            member.Hp = Mathf.Min(member.Hp, member.MaxHp);
            if (member.IsAlive)
            {
                member.Shield = state.Stats.StartShield;
            }

            state.ResetClocks();
        }

        _combatModel.ClearProjectiles();
    }

    public void ClearProjectiles()
    {
        _combatModel.ClearProjectiles();
        foreach (var state in _combatModel.MemberStateList)
        {
            state.AttackMotion.Stop();
            state.PendingActionList.Clear();
        }
    }

    // 무료 부활의 상태 초기화(기획서 E절). 조건 확인은 ReviveService가 한다
    public void ReviveMember(int memberIndex)
    {
        var member = _partyModel.MemberList[memberIndex];
        var state = _combatModel.MemberStateList[memberIndex];
        member.Hp = member.MaxHp;
        member.Shield = 0f;
        state.ResetClocks();
        foreach (var slot in state.SlotList)
        {
            slot.ResetCooldown();
        }

        _combatModel.SetRevive(_combatModel.ReviveCount.CurrentValue + 1, memberIndex);
    }

    // 한 걸음(RunService.Step ④): 나눔 제공자 → 대원 순서대로 지속 효과·발동 → 투사체 이동·명중 → 고리 시간
    public void Step(float deltaSeconds)
    {
        RefreshShareProviders();
        var memberList = _partyModel.MemberList;
        for (var i = 0; i < memberList.Count; i++)
        {
            var member = memberList[i];
            var state = _combatModel.MemberStateList[i];
            if (!member.IsAlive)
            {
                state.AttackMotion.Stop();
                state.PendingActionList.Clear();
                continue;
            }

            if (state.AttackMotion.Advance(deltaSeconds))
            {
                foreach (var action in state.PendingActionList)
                {
                    ResolveAction(member, action);
                }

                state.PendingActionList.Clear();
            }

            foreach (var slot in state.SlotList)
            {
                slot.FiredSeconds = Mathf.Max(0f, slot.FiredSeconds - deltaSeconds);
            }

            StepPassive(member, state, deltaSeconds);
            foreach (var slot in state.SlotList)
            {
                if (slot.Item.Mode != ItemMode.Passive)
                {
                    StepAction(member, state, slot, deltaSeconds);
                }
            }
        }

        StepProjectiles(deltaSeconds);
        foreach (var burst in _combatModel.BurstList)
        {
            burst.RemainingSeconds -= deltaSeconds;
        }

        _combatModel.RemoveEndedBursts();
    }

    private void RebuildSlots(MemberCombatState state, PartyMember member)
    {
        var slotList = new List<BattleItemSlot>(member.EquippedItemIdList.Count);
        foreach (var itemId in member.EquippedItemIdList)
        {
            slotList.Add(new BattleItemSlot(_itemRepository.Get(itemId)));
        }

        state.SetSlots(member.EquippedItemIdList, slotList);
    }

    // 대원 전체·칸별 능력치와 등급 배수, 파동 칸·나눔·보호막 채움 값을 다시 계산하고 최대 HP·유효 방어를 쓴다
    private void RefreshStats(int memberIndex)
    {
        var member = _partyModel.MemberList[memberIndex];
        var state = _combatModel.MemberStateList[memberIndex];
        var character = _characterRepository.Get(member.CharacterId);
        var equipped = state.EquippedItemIdList;
        state.Stats = _equipmentStatService.Calculate(memberIndex, character, equipped, -1);
        state.ShieldRefill = 0f;
        state.HasShare = false;
        for (var slotIndex = 0; slotIndex < state.SlotList.Count; slotIndex++)
        {
            var slot = state.SlotList[slotIndex];
            var item = slot.Item;
            slot.GradeMultiplier = _equipmentStatService.GetGradeMultiplier(memberIndex, slotIndex);
            slot.Stats = item.Mode == ItemMode.Passive
                ? state.Stats
                : _equipmentStatService.Calculate(memberIndex, character, equipped, slotIndex);
            var isBonusActive = _equipmentStatService.IsBonusActive(memberIndex, equipped, slotIndex);
            slot.IsPulse = isBonusActive && item.BonusEffect == ItemEffectKind.Pulse;
            if (isBonusActive && item.BonusEffect == ItemEffectKind.Shield)
            {
                state.ShieldRefill = Mathf.Max(state.ShieldRefill, item.BonusValue * slot.GradeMultiplier);
            }

            if (isBonusActive && item.BonusEffect == ItemEffectKind.Share)
            {
                state.HasShare = true;
            }
        }

        member.MaxHp = state.Stats.MaxHp;
        member.Defense = state.Stats.Defense;
    }

    private static bool IsSameItems(IReadOnlyList<string> a, IReadOnlyList<string> b)
    {
        if (a.Count != b.Count)
        {
            return false;
        }

        for (var i = 0; i < a.Count; i++)
        {
            if (a[i] != b[i])
            {
                return false;
            }
        }

        return true;
    }

    // 받는 대원마다 나눔 제공자 = 나눔이 켜진 다른 산 대원 중 회복 ÷ 주기가 가장 큰 대원. 제공자나 주기가 바뀌면 나눔 시계 0
    private void RefreshShareProviders()
    {
        var memberList = _partyModel.MemberList;
        for (var receiver = 0; receiver < memberList.Count; receiver++)
        {
            var provider = -1;
            var bestRate = 0f;
            if (memberList[receiver].IsAlive)
            {
                for (var i = 0; i < memberList.Count; i++)
                {
                    var candidate = _combatModel.MemberStateList[i];
                    if (i == receiver || !memberList[i].IsAlive || !candidate.HasShare)
                    {
                        continue;
                    }

                    var rate = candidate.Stats.Heal / GetHealPeriod(candidate.Stats);
                    if (rate > bestRate)
                    {
                        bestRate = rate;
                        provider = i;
                    }
                }
            }

            var period = provider < 0 ? 0f : GetHealPeriod(_combatModel.MemberStateList[provider].Stats);
            var state = _combatModel.MemberStateList[receiver];
            if (state.ShareProvider != provider || !Mathf.Approximately(state.SharePeriod, period))
            {
                state.ShareProvider = provider;
                state.SharePeriod = period;
                state.ShareClock = 0f;
            }
        }
    }

    private float GetHealPeriod(CombatStats stats) => _rules.HealPeriod / (1f + stats.Haste);

    // 지속 효과(B6): 자기 회복, 나눔, 보호막, 파동
    private void StepPassive(PartyMember member, MemberCombatState state, float deltaSeconds)
    {
        if (state.Stats.Heal > 0f)
        {
            var healPeriod = GetHealPeriod(state.Stats);
            state.HealClock += deltaSeconds;
            while (state.HealClock + ClockTolerance >= healPeriod)
            {
                state.HealClock -= healPeriod;
                _partyService.Heal(member, state.Stats.Heal);
            }
        }

        if (state.ShareProvider >= 0)
        {
            var providerHeal = _combatModel.MemberStateList[state.ShareProvider].Stats.Heal;
            state.ShareClock += deltaSeconds;
            while (state.ShareClock + ClockTolerance >= state.SharePeriod)
            {
                state.ShareClock -= state.SharePeriod;
                _partyService.Heal(member, providerHeal);
            }
        }

        member.Shield = Mathf.Min(member.Shield, state.Stats.Shield);
        if (state.ShieldRefill > 0f)
        {
            state.ShieldClock += deltaSeconds;
            while (state.ShieldClock + ClockTolerance >= _rules.ShieldPeriod)
            {
                state.ShieldClock -= _rules.ShieldPeriod;
                member.Shield = Mathf.Max(member.Shield, state.ShieldRefill);
            }
        }
        else
        {
            state.ShieldClock = 0f;
        }

        foreach (var slot in state.SlotList)
        {
            if (!slot.IsPulse)
            {
                continue;
            }

            slot.PulseCooldown -= deltaSeconds;
            if (slot.PulseCooldown > 0f)
            {
                continue;
            }

            var item = slot.Item;
            slot.PulseCooldown = item.EffectInterval;
            // 발동 아이템 칸의 표시(마지막 주기·발동!)는 발동 쪽 것이다. 지속 아이템 칸만 파동으로 표시한다
            if (item.Mode == ItemMode.Passive)
            {
                slot.LastPeriod = item.EffectInterval;
                slot.FiredSeconds = _rules.FiredSeconds;
            }

            _combatModel.AddFireCount(item.Id);
            AddBurst(member.Position, item.EffectRadius, item.Id, BurstEffectKind.Pulse);
            var damage = item.BonusValue * slot.GradeMultiplier;
            foreach (var monster in _monsterModel.MonsterList)
            {
                if (monster.IsAlive && Vector2.Distance(member.Position, monster.Position) <= item.EffectRadius)
                {
                    _monsterService.ApplyDamage(monster, damage);
                }
            }
        }
    }

    // 발동 아이템 한 칸(B5)
    private void StepAction(PartyMember member, MemberCombatState state, BattleItemSlot slot, float deltaSeconds)
    {
        slot.RemainingCooldown -= deltaSeconds;
        if (slot.RemainingCooldown > 0f)
        {
            return;
        }

        var item = slot.Item;
        var range = _equipmentStatService.GetRange(item, slot.Stats);
        var character = _characterRepository.Get(member.CharacterId);
        var power = _equipmentStatService.GetPower(item, slot.GradeMultiplier, character, slot.Stats);
        Monster nearest = null;
        List<PartyMember> healTargetList = null;
        var direction = Vector2.zero;
        if (item.IsHealMode)
        {
            FindHealTargets(member.Position, range);
            if (_healTargetList.Count == 0)
            {
                return;
            }

            healTargetList = new List<PartyMember>(_healTargetList);
            direction = (healTargetList[0].Position - member.Position).normalized;
        }
        else
        {
            nearest = FindNearestMonster(member.Position, range, null);
            if (nearest == null)
            {
                return;
            }

            direction = (nearest.Position - member.Position).normalized;
        }

        MarkFired(slot);
        var action = new PendingCombatAction(slot, range, power, direction, nearest, healTargetList);
        // 그림 키가 없는 임시 도형은 종전처럼 즉시 실행한다.
        if (character.Motion == null)
        {
            ResolveAction(member, action);
            return;
        }

        if (!state.AttackMotion.IsPreparing)
        {
            var shortestPeriod = float.MaxValue;
            foreach (var equipped in state.SlotList)
            {
                if (equipped.Item.Mode != ItemMode.Passive)
                {
                    shortestPeriod = Mathf.Min(shortestPeriod, _equipmentStatService.GetPeriod(equipped.Item, equipped.Stats));
                }
            }

            // 목표 쪽으로 공격. 자기 치유는 마지막 공격 방향(초기 앞)을 유지한다.
            var facing = SpriteFacing.Next(state.AttackMotion.Facing, IsoProjection.FloorToWorld(direction, 1f), 0.001f, 0.25f, true);
            state.AttackMotion.Start(character.Motion, facing, shortestPeriod);
        }

        state.PendingActionList.Add(action);
    }

    private void ResolveAction(PartyMember member, PendingCombatAction action)
    {
        var slot = action.Slot;
        var item = slot.Item;
        var power = action.Power;
        var range = action.Range;
        if (item.IsHealMode)
        {
            foreach (var target in action.HealTargetList)
            {
                if (target.IsAlive)
                {
                    _partyService.Heal(target, power);
                    if (item.Mode == ItemMode.Heal)
                    {
                        AddBurst(target.Position, PointBurstRadius, item.Id, BurstEffectKind.Heal);
                    }
                }

                if (item.Mode == ItemMode.Heal)
                {
                    break;
                }
            }

            if (item.Mode == ItemMode.HealArea)
            {
                AddBurst(member.Position, range, item.Id, BurstEffectKind.HealArea);
            }

            return;
        }

        var direction = action.Direction;
        var nearest = action.Target;
        switch (item.Mode)
        {
            case ItemMode.Melee:
                HitCone(member.Position, direction, range, _rules.MeleeHalfAngle, power, item.Id);
                break;
            case ItemMode.Sweep:
                HitCone(member.Position, direction, range, _rules.SweepHalfAngle, power, item.Id);
                break;
            case ItemMode.Chain:
                if (nearest.IsAlive)
                {
                    HitChain(member.Position, nearest, slot.Stats.Chain, power, item.Id);
                }
                break;
            case ItemMode.Projectile:
                ShootProjectiles(member.Position, direction, slot.Stats, power, range, item.Id);
                break;
            case ItemMode.Explosion:
                var radius = _rules.ExplosionRadius * (1f + slot.Stats.Radius);
                _combatModel.AddProjectile(new ProjectileState(_combatModel.TakeVisualId(), member.Position, direction,
                    _rules.ProjectileSpeed, power, range, 0, true, radius, nearest.Position, item.Id));
                break;
            default:
                GameLogger.LogError($"[CombatService] {item.Id}: mode {item.Mode} has no action");
                break;
        }
    }

    private void MarkFired(BattleItemSlot slot)
    {
        var period = _equipmentStatService.GetPeriod(slot.Item, slot.Stats);
        slot.RemainingCooldown = period;
        slot.LastPeriod = period;
        slot.FiredSeconds = _rules.FiredSeconds;
        _combatModel.AddFireCount(slot.Item.Id);
    }

    // 사거리 안(자기 포함) 쓰러지지 않고 HP < 최대 HP인 동료, HP 비율 오름차순(같으면 편성 순서)
    private void FindHealTargets(Vector2 position, float range)
    {
        _healTargetList.Clear();
        foreach (var member in _partyModel.MemberList)
        {
            if (!member.IsAlive || member.Hp >= member.MaxHp || Vector2.Distance(member.Position, position) > range)
            {
                continue;
            }

            // 안정 삽입: 비율이 같으면 앞에 넣은 대원(편성 순서)이 앞
            var ratio = member.Hp / member.MaxHp;
            var index = _healTargetList.Count;
            while (index > 0 && _healTargetList[index - 1].Hp / _healTargetList[index - 1].MaxHp > ratio)
            {
                index--;
            }

            _healTargetList.Insert(index, member);
        }
    }

    // 사거리 안 산 적 중 가장 가까운 적(같으면 먼저 생긴 적 = 목록 앞). excludedIdSet에 든 적은 뺀다
    private Monster FindNearestMonster(Vector2 position, float range, HashSet<int> excludedIdSet)
    {
        Monster nearest = null;
        var nearestDistance = float.MaxValue;
        foreach (var monster in _monsterModel.MonsterList)
        {
            if (!monster.IsAlive || (excludedIdSet != null && excludedIdSet.Contains(monster.Id)))
            {
                continue;
            }

            var distance = Vector2.Distance(monster.Position, position);
            if (distance <= range && distance < nearestDistance)
            {
                nearestDistance = distance;
                nearest = monster;
            }
        }

        return nearest;
    }

    private void HitCone(Vector2 origin, Vector2 direction, float range, float halfAngle, float power, string itemId)
    {
        foreach (var monster in _monsterModel.MonsterList)
        {
            var offset = monster.Position - origin;
            if (monster.IsAlive && offset.magnitude <= range && Vector2.Angle(direction, offset) <= halfAngle)
            {
                _monsterService.ApplyDamage(monster, power);
            }
        }

        AddBurst(origin, range, itemId, BurstEffectKind.Melee, direction);
    }

    // 대상부터 chainBaseHits + 추가 연쇄 번, 마지막 맞은 적에서 chainHopRange 안 아직 안 맞은 가장 가까운 산 적으로
    private void HitChain(Vector2 origin, Monster first, int extraChain, float power, string itemId)
    {
        _chainHitIdSet.Clear();
        var next = first;
        var hitCount = _rules.ChainBaseHits + extraChain;
        for (var n = 0; n < hitCount && next != null; n++)
        {
            _chainHitIdSet.Add(next.Id);
            _monsterService.ApplyDamage(next, power);
            AddBurst(origin, PointBurstRadius, itemId, BurstEffectKind.Chain, endPosition: next.Position);
            origin = next.Position;
            next = FindNearestMonster(next.Position, _rules.ChainHopRange, _chainHitIdSet);
        }
    }

    // 1 + 갈래 발을 (n − 갈래 × 0.5) × projectileSpread 각도로
    private void ShootProjectiles(Vector2 origin, Vector2 direction, CombatStats stats, float power, float range, string itemId)
    {
        for (var n = 0; n <= stats.ExtraShots; n++)
        {
            var angle = (n - stats.ExtraShots * 0.5f) * _rules.ProjectileSpread;
            var cos = Mathf.Cos(angle);
            var sin = Mathf.Sin(angle);
            var shotDirection = new Vector2(direction.x * cos - direction.y * sin, direction.x * sin + direction.y * cos);
            _combatModel.AddProjectile(new ProjectileState(_combatModel.TakeVisualId(), origin, shotDirection,
                _rules.ProjectileSpeed, power, range, stats.Pierce, false, 0f, Vector2.zero, itemId));
        }
    }

    // 투사체 이동·명중(예전 Simulate 투사체 부분)
    private void StepProjectiles(float deltaSeconds)
    {
        foreach (var projectile in _combatModel.ProjectileList)
        {
            // 사거리를 넘는 몫은 날지 않는다(넘은 자리의 적을 맞히지 않게)
            var travel = Mathf.Min(projectile.Speed * deltaSeconds, projectile.Range - projectile.Traveled);
            var from = projectile.Position;
            projectile.Position = from + projectile.Direction * travel;
            projectile.Traveled += travel;
            if (projectile.IsExplosion)
            {
                if (Vector2.Distance(projectile.Position, projectile.TargetPosition) < travel + _rules.ExplosionTriggerGap
                    || projectile.Traveled >= projectile.Range)
                {
                    Explode(projectile);
                }

                continue;
            }

            foreach (var monster in _monsterModel.MonsterList)
            {
                if (!monster.IsAlive || projectile.HitMonsterIdSet.Contains(monster.Id))
                {
                    continue;
                }

                if (GetSegmentDistance(from, projectile.Position, monster.Position) < _monsterService.GetRadius(monster))
                {
                    _monsterService.ApplyDamage(monster, projectile.Damage);
                    projectile.HitMonsterIdSet.Add(monster.Id);
                    if (projectile.HitMonsterIdSet.Count > projectile.Pierce)
                    {
                        projectile.IsDone = true;
                        break;
                    }
                }
            }

            if (projectile.Traveled >= projectile.Range)
            {
                projectile.IsDone = true;
            }
        }

        _combatModel.RemoveDoneProjectiles();
    }

    private void Explode(ProjectileState projectile)
    {
        foreach (var monster in _monsterModel.MonsterList)
        {
            if (monster.IsAlive && Vector2.Distance(projectile.Position, monster.Position) < projectile.ExplosionRadius)
            {
                _monsterService.ApplyDamage(monster, projectile.Damage);
            }
        }

        AddBurst(projectile.Position, projectile.ExplosionRadius, projectile.ItemId, BurstEffectKind.Explosion);
        projectile.IsDone = true;
    }

    private void AddBurst(Vector2 position, float radius, string itemId, BurstEffectKind kind,
        Vector2 direction = default, Vector2 endPosition = default)
    {
        _combatModel.AddBurst(new BurstState(_combatModel.TakeVisualId(), position, radius, BurstSeconds, itemId, kind, direction, endPosition));
    }

    private static float GetSegmentDistance(Vector2 from, Vector2 to, Vector2 point)
    {
        var segment = to - from;
        var lengthSqr = segment.sqrMagnitude;
        if (lengthSqr <= 0f)
        {
            return Vector2.Distance(from, point);
        }

        var t = Mathf.Clamp01(Vector2.Dot(point - from, segment) / lengthSqr);
        return Vector2.Distance(from + segment * t, point);
    }
}
