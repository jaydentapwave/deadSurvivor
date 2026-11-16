using System.Collections.Generic;
using UnityEngine;

// 자폭(기획서 A절, 예전 DSGame.Combat의 UpdateEnemy bomber·BomberMove): Approach ↔ Swarm → Fuse.
// 점화가 끝나면 blastRadius 안의 산 대원 모두에게 피해를 주고 HP 0이 된다. 걸음 끝 정리에서 빠져 처치 수에 들지 않는다(MA9).
public class BombBehavior : IMonsterBehavior
{
    private readonly MonsterModel _monsterModel;
    private readonly PartyModel _partyModel;
    private readonly PartyService _partyService;

    // flockRadius 안의 Fuse 아닌 다른 산 자폭형
    private readonly List<Monster> _mateList = new();

    public BombBehavior(MonsterModel monsterModel, PartyModel partyModel, PartyService partyService)
    {
        _monsterModel = monsterModel;
        _partyModel = partyModel;
        _partyService = partyService;
    }

    public bool CanHandle(MonsterBehavior behavior) => behavior == MonsterBehavior.Bomb;

    public void Tick(Monster monster, PartyMember target, float deltaSeconds)
    {
        var data = monster.Data;
        var bomb = data.Bomb;
        var distance = Vector2.Distance(monster.Position, target.Position);
        if (monster.Phase == MonsterPhase.Fuse || distance < bomb.FuseDistance)
        {
            Fuse(monster, bomb, deltaSeconds);
            return;
        }

        CollectMates(monster, bomb);
        if (monster.Phase != MonsterPhase.Swarm && distance < bomb.SwarmTriggerRange)
        {
            monster.Phase = MonsterPhase.Swarm;
            monster.PhaseClock = bomb.SwarmSeconds;
            foreach (var mate in _mateList)
            {
                mate.Phase = MonsterPhase.Swarm;
                mate.PhaseClock = bomb.SwarmSeconds;
            }
        }

        // 표적 방향 + 응집·정렬·분리. 무리 방향은 걸음마다 min(1, dt × steerRate)로 섞는다
        var steer = (target.Position - monster.Position).normalized;
        if (_mateList.Count > 0)
        {
            var center = Vector2.zero;
            var align = Vector2.zero;
            var separation = Vector2.zero;
            foreach (var mate in _mateList)
            {
                center += mate.Position;
                align += mate.FlockDirection;
                var mateDistance = Vector2.Distance(monster.Position, mate.Position);
                if (mateDistance < bomb.SeparationRadius)
                {
                    // 거의 겹치면 번호로 정한 방향으로 민다(예전 규칙 Direction(id × 2.4))
                    separation += mateDistance > 0.01f
                        ? (monster.Position - mate.Position) / mateDistance * (1f - mateDistance / bomb.SeparationRadius)
                        : new Vector2(Mathf.Cos(monster.Id * 2.4f), Mathf.Sin(monster.Id * 2.4f));
                }
            }

            steer += bomb.Cohesion * (center / _mateList.Count - monster.Position).normalized
                + bomb.Alignment * align / _mateList.Count + bomb.Separation * separation;
        }

        monster.FlockDirection = Vector2.Lerp(monster.FlockDirection, steer.normalized, Mathf.Min(1f, deltaSeconds * bomb.SteerRate));
        var speed = monster.Phase == MonsterPhase.Swarm ? bomb.SwarmSpeed : data.Speed;
        monster.Position += monster.FlockDirection * speed * deltaSeconds;
        if (monster.Phase == MonsterPhase.Swarm)
        {
            monster.PhaseClock -= deltaSeconds;
            if (monster.PhaseClock <= 0f)
            {
                monster.Phase = MonsterPhase.Approach;
            }
        }
    }

    // 멈춰서 fuseSeconds 뒤 폭발
    private void Fuse(Monster monster, MonsterBombData bomb, float deltaSeconds)
    {
        if (monster.Phase != MonsterPhase.Fuse)
        {
            monster.Phase = MonsterPhase.Fuse;
            monster.PhaseClock = bomb.FuseSeconds;
        }

        monster.PhaseClock -= deltaSeconds;
        if (monster.PhaseClock > 0f)
        {
            return;
        }

        foreach (var member in _partyModel.MemberList)
        {
            if (member.IsAlive && Vector2.Distance(monster.Position, member.Position) <= bomb.BlastRadius)
            {
                _partyService.ApplyDamage(member, monster.Damage);
            }
        }

        monster.Hp = 0f;
    }

    private void CollectMates(Monster monster, MonsterBombData bomb)
    {
        _mateList.Clear();
        foreach (var other in _monsterModel.MonsterList)
        {
            if (other != monster && other.IsAlive && other.Behavior == MonsterBehavior.Bomb && other.Phase != MonsterPhase.Fuse
                && Vector2.Distance(monster.Position, other.Position) < bomb.FlockRadius)
            {
                _mateList.Add(other);
            }
        }
    }
}
