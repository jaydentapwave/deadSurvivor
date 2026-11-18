using System.Collections.Generic;
using UnityEngine;

// 돌진(기획서 A절, 예전 DSGame.Combat의 UpdateEnemy charger·Rally): Approach → Windup → Dash → Recover.
// 붙어 있어도 집결이 먼저라 접촉 공격을 하지 않는다(MA8). 대기는 Recover에 들 때 cooldown(A절).
public class ChargeBehavior : IMonsterBehavior
{
    private const float ContactTolerance = 0.001f;

    private readonly MonsterModel _monsterModel;
    private readonly PartyModel _partyModel;
    private readonly PartyService _partyService;

    // 가까운 순(같으면 목록 순)으로 줄 세운 집결 후보
    private readonly List<Monster> _rallyList = new();

    public ChargeBehavior(MonsterModel monsterModel, PartyModel partyModel, PartyService partyService)
    {
        _monsterModel = monsterModel;
        _partyModel = partyModel;
        _partyService = partyService;
    }

    public bool CanHandle(MonsterBehavior behavior) => behavior == MonsterBehavior.Charge;

    public void Tick(Monster monster, PartyMember target, float deltaSeconds)
    {
        var data = monster.Data;
        var charge = data.Charge;
        switch (monster.Phase)
        {
            case MonsterPhase.Windup:
                monster.PhaseClock -= deltaSeconds;
                if (monster.PhaseClock <= 0f)
                {
                    monster.Phase = MonsterPhase.Dash;
                    monster.PhaseClock = charge.DashSeconds;
                    monster.ClearDashHits();
                }

                return;
            case MonsterPhase.Dash:
                Dash(monster, charge, deltaSeconds);
                return;
            case MonsterPhase.Recover:
                monster.PhaseClock -= deltaSeconds;
                if (monster.PhaseClock <= 0f)
                {
                    monster.Phase = MonsterPhase.Approach;
                }

                return;
        }

        var toTarget = target.Position - monster.Position;
        var distance = toTarget.magnitude;
        if (distance < charge.TriggerRange && monster.AttackClock <= 0f)
        {
            Rally(monster);
            return;
        }

        var contactDistance = data.Radius + data.ContactGap;
        if (distance > contactDistance + ContactTolerance)
        {
            monster.Position += toTarget / distance * Mathf.Min(data.Speed * deltaSeconds, distance - contactDistance);
        }
    }

    // 고정한 방향으로 dashSpeed. 이번 걸음에 지나간 선분과 dashHitRadius 안의 산 대원에게 돌진 한 번에 한 번씩 피해
    private void Dash(Monster monster, MonsterChargeData charge, float deltaSeconds)
    {
        var from = monster.Position;
        monster.Position = from + monster.DashDirection * charge.DashSpeed * Mathf.Min(deltaSeconds, monster.PhaseClock);
        foreach (var member in _partyModel.MemberList)
        {
            if (!member.IsAlive || ContainsMember(monster.DashHitMemberList, member))
            {
                continue;
            }

            if (SegmentMath.Distance(from, monster.Position, member.Position) < charge.DashHitRadius)
            {
                _partyService.ApplyDamage(member, monster.Damage);
                monster.AddDashHit(member);
            }
        }

        monster.PhaseClock -= deltaSeconds;
        if (monster.PhaseClock <= 0f)
        {
            monster.Phase = MonsterPhase.Recover;
            monster.PhaseClock = charge.RecoverSeconds;
            monster.AttackClock = charge.Cooldown;
        }
    }

    // 자신 + rallyRadius 안의 Approach·대기 끝난 다른 돌진형을 가까운 순 최대 rallyMax마리. 각자 그 순간 가장 가까운 대원 쪽으로 방향 고정
    private void Rally(Monster leader)
    {
        var charge = leader.Data.Charge;
        _rallyList.Clear();
        foreach (var other in _monsterModel.MonsterList)
        {
            if (other == leader || !other.IsAlive || other.Behavior != MonsterBehavior.Charge || other.Phase != MonsterPhase.Approach
                || other.AttackClock > 0f)
            {
                continue;
            }

            var distance = Vector2.Distance(other.Position, leader.Position);
            if (distance > charge.RallyRadius)
            {
                continue;
            }

            var index = _rallyList.Count;
            while (index > 0 && Vector2.Distance(_rallyList[index - 1].Position, leader.Position) > distance)
            {
                index--;
            }

            _rallyList.Insert(index, other);
        }

        if (_rallyList.Count > charge.RallyMax)
        {
            _rallyList.RemoveRange(charge.RallyMax, _rallyList.Count - charge.RallyMax);
        }

        _rallyList.Insert(0, leader);
        foreach (var member in _rallyList)
        {
            var target = _partyService.FindNearestAlive(member.Position);
            if (target == null)
            {
                continue;
            }

            member.Phase = MonsterPhase.Windup;
            member.PhaseClock = member.Data.Charge.WindupSeconds;
            member.DashDirection = (target.Position - member.Position).normalized;
        }
    }

    private static bool ContainsMember(IReadOnlyList<PartyMember> memberList, PartyMember member)
    {
        foreach (var hit in memberList)
        {
            if (hit == member)
            {
                return true;
            }
        }

        return false;
    }
}
