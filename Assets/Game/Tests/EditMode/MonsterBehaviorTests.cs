using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

// F4 AC5~AC8 — 행동 5종과 적 투사체
public class MonsterBehaviorTests
{
    private const float Tolerance = 1e-3f;

    [Test]
    public void B3ProjectileRetreatStopsAtBoundaryAndSlidesAlongIt()
    {
        var context = new TestRunContext();
        var target = KeepOnly(context, 0);
        var limit = context.RunRules.FieldCells / (2f * context.RunRules.UnitScale);
        var start = new Vector2(limit * .5f, limit * .5f - 1f);
        target.Position = start - Vector2.right * 50f;
        var monster = context.CreateMonster(1, "mushroom", start);
        context.MonsterModel.Add(monster);
        context.MonsterBehaviorService.Tick(1f);
        Assert.AreEqual(limit, Mathf.Abs(monster.Position.x) + Mathf.Abs(monster.Position.y), .001f);
        Assert.Less(monster.Position.y, start.y);
        var beforeSlide = monster.Position;
        target.Position = beforeSlide - Vector2.right * 50f;
        context.MonsterBehaviorService.Tick(.1f);
        Assert.Greater(monster.Position.x, beforeSlide.x);
        Assert.Less(monster.Position.y, beforeSlide.y);
        Assert.AreEqual(limit, Mathf.Abs(monster.Position.x) + Mathf.Abs(monster.Position.y), .001f);
    }

    [Test]
    public void B5OutwardDashClipsFinalPositionWithoutChangingHitsForFourLegallyPlacedMembers()
    {
        // At 1920x1080 the legal party centre and real formation remain far from the exterior segment.
        // Exterior dash hits are unreachable in supported gameplay; the existing dash tests cover interior hit-once behavior.
        var clipped = new TestRunContext();
        var baseline = new TestRunContext();
        foreach (var context in new[] { clipped, baseline })
        {
            context.RunService.StartRun(1, 0f);
            var extent = TiltedViewMath.Footprint(PixelScaleMath.OrthographicSize(1080, 48, 2), 16f / 9f, .5f) / context.RunRules.UnitScale;
            var limit = context.RunRules.FieldCells / (2f * context.RunRules.UnitScale);
            context.PartyModel.SetCenter(new Vector2(limit - extent.x - extent.y, 0f));
            context.PartyMoveService.StartWave();
            var monster = context.CreateMonster(1, "boar", new Vector2(limit - 10f, 0f));
            monster.Phase = MonsterPhase.Dash;
            monster.PhaseClock = monster.Data.Charge.DashSeconds;
            monster.DashDirection = new Vector2(.8f, .6f);
            context.MonsterModel.Add(monster);
            var end = monster.Position + monster.DashDirection * monster.Data.Charge.DashSpeed * monster.PhaseClock;
            Assert.Greater(Mathf.Abs(end.x) + Mathf.Abs(end.y), limit);
            foreach (var member in context.PartyModel.MemberList)
            {
                var slot = member.Position - context.PartyModel.Center;
                Assert.LessOrEqual(Mathf.Abs(slot.x) + Mathf.Abs(slot.y), 102f + .001f);
                Assert.Greater(SegmentMath.Distance(monster.Position, end, member.Position), monster.Data.Charge.DashHitRadius);
            }
        }
        var rawMonster = baseline.MonsterModel.MonsterList[0];
        new ChargeBehavior(baseline.MonsterModel, baseline.PartyModel, baseline.PartyService)
            .Tick(rawMonster, baseline.PartyService.FindNearestAlive(rawMonster.Position), rawMonster.PhaseClock);
        var boundedMonster = clipped.MonsterModel.MonsterList[0];
        clipped.MonsterBehaviorService.Tick(boundedMonster.PhaseClock);
        for (var i = 0; i < 4; i++)
        {
            Assert.AreEqual(baseline.PartyModel.MemberList[i].Hp, clipped.PartyModel.MemberList[i].Hp, Tolerance);
        }
        Assert.AreEqual(rawMonster.DashHitMemberList.Count, boundedMonster.DashHitMemberList.Count);
        Assert.AreEqual(0, boundedMonster.DashHitMemberList.Count);
        var boundary = clipped.RunRules.FieldCells / (2f * clipped.RunRules.UnitScale);
        Assert.AreEqual(boundary, Mathf.Abs(boundedMonster.Position.x) + Mathf.Abs(boundedMonster.Position.y), .001f);
        Assert.Greater(Mathf.Abs(rawMonster.Position.x) + Mathf.Abs(rawMonster.Position.y), boundary);
    }

    [Test]
    public void ContactApproachesThenHitsEveryInterval()
    {
        var context = new TestRunContext();
        var bruno = KeepOnly(context, 0);
        var slime = context.MonsterRepository.Get("slime");
        var damageScale = 1.5f;
        var monster = new Monster(1, slime, bruno.Position + Vector2.left * 100f, slime.Hp, slime.Damage * damageScale,
            context.MonsterRepository.SpawnRules.InitialAttackDelay);
        context.MonsterModel.Add(monster);

        var hitTimeList = StepAndRecordHits(context, bruno, 4f, out var hpLossList);

        // 77 거리를 속도 63으로 약 1.22초 만에 닿고(첫 대기 1초는 그 전에 끝남) 닿은 다음 걸음에 첫 피해, 그 뒤 1초마다
        Assert.AreEqual(slime.Radius + slime.ContactGap, Vector2.Distance(monster.Position, bruno.Position), 0.01f);
        Assert.AreEqual((100f - 23f) / slime.Speed, hitTimeList[0], 2f * context.RunRules.SimStep + Tolerance);
        Assert.GreaterOrEqual(hitTimeList.Count, 3);
        for (var i = 1; i < hitTimeList.Count; i++)
        {
            Assert.AreEqual(slime.Contact.Interval, hitTimeList[i] - hitTimeList[i - 1], context.RunRules.SimStep + Tolerance);
        }

        var expected = slime.Damage * damageScale * (1f - context.CharacterRepository.Get("bruno").Def);
        foreach (var loss in hpLossList)
        {
            Assert.AreEqual(expected, loss, Tolerance);
        }
    }

    [Test]
    public void ArmoredUsesLongerInterval()
    {
        var context = new TestRunContext();
        var bruno = KeepOnly(context, 0);
        var rock = context.MonsterRepository.Get("rock");
        context.MonsterModel.Add(context.CreateMonster(1, "rock", bruno.Position + Vector2.left * (rock.Radius + rock.ContactGap)));

        var hitTimeList = StepAndRecordHits(context, bruno, 4f, out _);

        Assert.AreEqual(3, hitTimeList.Count);
        Assert.AreEqual(1.8f, hitTimeList[1] - hitTimeList[0], context.RunRules.SimStep + Tolerance);
        Assert.AreEqual(1.8f, hitTimeList[2] - hitTimeList[1], context.RunRules.SimStep + Tolerance);
    }

    [Test]
    public void DeadMemberIsNotTargeted()
    {
        var context = new TestRunContext();
        context.PartyService.CreateParty();
        var members = context.PartyModel.MemberList;
        var bruno = members[0];
        bruno.Hp = 0f;
        // F2부터 CreateParty는 모든 대원을 원점에 둔다(자리는 진형이 웨이브 시작에 정한다). 산 대원을 브루노 오른쪽으로 벌려 둔다
        for (var i = 1; i < members.Count; i++)
        {
            members[i].Position = bruno.Position + Vector2.right * (48f * i);
        }

        var slime = context.MonsterRepository.Get("slime");
        var monster = context.CreateMonster(1, "slime", bruno.Position + Vector2.left * (slime.Radius + slime.ContactGap));
        context.MonsterModel.Add(monster);
        var target = context.PartyService.FindNearestAlive(monster.Position);
        var before = Vector2.Distance(monster.Position, target.Position);

        StepBehavior(context, 0.5f);

        Assert.AreNotSame(bruno, target);
        Assert.AreEqual(0f, bruno.Hp);
        Assert.Less(Vector2.Distance(monster.Position, target.Position), before);
    }

    [Test]
    public void ProjectileKeepsDistanceBand()
    {
        var mushroom = new TestRunContext().MonsterRepository.Get("mushroom");
        var range = mushroom.Projectile.Range;
        foreach (var start in new[] { 400f, 100f })
        {
            var context = new TestRunContext();
            var bruno = KeepOnly(context, 0);
            bruno.Hp = 1e6f;
            var monster = context.CreateMonster(1, "mushroom", bruno.Position + Vector2.left * start);
            context.MonsterModel.Add(monster);

            StepBehavior(context, 6f);

            var distance = Vector2.Distance(monster.Position, bruno.Position);
            Assert.LessOrEqual(distance, range * mushroom.Projectile.ApproachRatio + Tolerance, $"start {start}");
            Assert.GreaterOrEqual(distance, range * mushroom.Projectile.RetreatRatio - Tolerance, $"start {start}");
        }
    }

    [Test]
    public void ProjectileFiresEveryInterval()
    {
        var context = new TestRunContext();
        var bruno = KeepOnly(context, 0);
        var monster = context.CreateMonster(1, "mushroom", bruno.Position + Vector2.left * 200f);
        context.MonsterModel.Add(monster);
        var shotTimeList = new List<float>();
        var steps = Mathf.RoundToInt(5f / context.RunRules.SimStep);
        for (var i = 1; i <= steps; i++)
        {
            var before = context.MonsterModel.EnemyShotList.Count;
            context.MonsterBehaviorService.Tick(context.RunRules.SimStep);
            if (context.MonsterModel.EnemyShotList.Count > before)
            {
                shotTimeList.Add(i * context.RunRules.SimStep);
            }
        }

        // 대기 0으로 놓아 첫 걸음에 한 발, 그 뒤 2초마다(접촉 피해 없음)
        Assert.AreEqual(3, shotTimeList.Count);
        Assert.AreEqual(2f, shotTimeList[1] - shotTimeList[0], context.RunRules.SimStep + Tolerance);
        Assert.AreEqual(bruno.MaxHp, bruno.Hp);
        var shot = context.MonsterModel.EnemyShotList[0];
        Assert.AreEqual(monster.Damage, shot.Damage);
        Assert.AreEqual(252f * 1.5f, shot.MaxTravel, Tolerance);
    }

    [Test]
    public void EnemyShotHitsFirstMemberOnly()
    {
        var context = new TestRunContext();
        context.PartyService.CreateParty();
        var members = context.PartyModel.MemberList;
        members[0].Position = new Vector2(100f, 0f);
        members[1].Position = new Vector2(110f, 0f);
        members[2].Position = new Vector2(1000f, 1000f);
        members[3].Position = new Vector2(-1000f, 1000f);
        var shot = new EnemyShot(1, Vector2.zero, Vector2.right, 180f, 8f, 378f, 15f, "#f2d3ff");
        context.MonsterModel.AddEnemyShot(shot);

        for (var i = 0; i < 60 && shot.IsAlive; i++)
        {
            context.EnemyShotService.Tick(context.RunRules.SimStep);
        }

        context.MonsterModel.RemoveDeadShots();
        Assert.Less(members[0].Hp, members[0].MaxHp);
        Assert.AreEqual(members[1].MaxHp, members[1].Hp);
        Assert.AreEqual(0, context.MonsterModel.EnemyShotList.Count);
    }

    [Test]
    public void EnemyShotExpiresAtMaxTravel()
    {
        var context = new TestRunContext();
        KeepOnly(context, 0).Position = new Vector2(0f, 1000f);
        var shot = new EnemyShot(1, Vector2.zero, Vector2.right, 180f, 8f, 378f, 15f, "#f2d3ff");
        context.MonsterModel.AddEnemyShot(shot);
        var lastTravel = 0f;
        for (var i = 0; i < 100 && shot.IsAlive; i++)
        {
            lastTravel = shot.Travel;
            context.EnemyShotService.Tick(context.RunRules.SimStep);
        }

        // 누적 이동이 378에 닿는 걸음에 사라진다(그 앞 걸음까지는 살아 있다)
        Assert.IsFalse(shot.IsAlive);
        Assert.Less(lastTravel, 378f);
        Assert.GreaterOrEqual(shot.Travel, 378f);
    }

    [Test]
    public void EnemyShotsNotCountedAsThreat()
    {
        var context = new TestRunContext();
        context.PartyService.CreateParty();
        context.MonsterModel.Add(context.CreateMonster(1, "mushroom", new Vector2(500f, 0f)));
        context.MonsterModel.AddEnemyShot(new EnemyShot(1, Vector2.zero, Vector2.right, 180f, 8f, 378f, 15f, "#f2d3ff"));
        context.MonsterModel.AddEnemyShot(new EnemyShot(2, Vector2.zero, Vector2.up, 180f, 8f, 378f, 15f, "#f2d3ff"));

        Assert.AreEqual(1, context.MonsterService.AliveCount);
    }

    [Test]
    public void ChargeRalliesUpToMax()
    {
        var context = new TestRunContext();
        var bruno = KeepOnly(context, 0);
        var leader = context.CreateMonster(1, "boar", bruno.Position + Vector2.left * 200f);
        context.MonsterModel.Add(leader);
        // 지도자 둘레 10, 20, …, 200 거리에 20마리. 가까운 15마리만 함께 모인다
        for (var i = 1; i <= 20; i++)
        {
            context.MonsterModel.Add(context.CreateMonster(1 + i, "boar", leader.Position + Vector2.down * (10f * i)));
        }

        var charge = new ChargeBehavior(context.MonsterModel, context.PartyModel, context.PartyService);
        charge.Tick(leader, bruno, context.RunRules.SimStep);

        var windupCount = 0;
        foreach (var monster in context.MonsterModel.MonsterList)
        {
            if (monster.Phase == MonsterPhase.Windup)
            {
                windupCount++;
                Assert.AreEqual(1f, monster.DashDirection.magnitude, Tolerance);
            }
        }

        Assert.AreEqual(16, windupCount);
        Assert.AreEqual(MonsterPhase.Windup, context.MonsterModel.MonsterList[15].Phase);
        Assert.AreEqual(MonsterPhase.Approach, context.MonsterModel.MonsterList[16].Phase);
    }

    [Test]
    public void ChargePhasesInOrder()
    {
        var context = new TestRunContext();
        var bruno = KeepOnly(context, 0);
        bruno.Hp = 1e6f;
        var monster = context.CreateMonster(1, "boar", bruno.Position + Vector2.left * 200f);
        context.MonsterModel.Add(monster);
        var phaseTimeList = RecordPhaseChanges(context, monster, 3f);

        var dt = context.RunRules.SimStep;
        Assert.AreEqual(MonsterPhase.Windup, phaseTimeList[0].Key);
        Assert.AreEqual(MonsterPhase.Dash, phaseTimeList[1].Key);
        Assert.AreEqual(MonsterPhase.Recover, phaseTimeList[2].Key);
        Assert.AreEqual(MonsterPhase.Approach, phaseTimeList[3].Key);
        Assert.AreEqual(0.8f, phaseTimeList[1].Value - phaseTimeList[0].Value, dt + Tolerance);
        Assert.AreEqual(0.7f, phaseTimeList[2].Value - phaseTimeList[1].Value, dt + Tolerance);
        Assert.AreEqual(0.8f, phaseTimeList[3].Value - phaseTimeList[2].Value, dt + Tolerance);
    }

    [Test]
    public void DashHitsEachMemberOnce()
    {
        var context = new TestRunContext();
        context.PartyService.CreateParty();
        var members = context.PartyModel.MemberList;
        members[0].Position = new Vector2(60f, 0f);
        members[1].Position = new Vector2(120f, 10f);
        members[2].Position = new Vector2(0f, 1000f);
        members[3].Position = new Vector2(0f, -1000f);
        var monster = context.CreateMonster(1, "boar", Vector2.zero);
        monster.Phase = MonsterPhase.Dash;
        monster.PhaseClock = monster.Data.Charge.DashSeconds;
        monster.DashDirection = Vector2.right;
        context.MonsterModel.Add(monster);
        var charge = new ChargeBehavior(context.MonsterModel, context.PartyModel, context.PartyService);

        while (monster.Phase == MonsterPhase.Dash)
        {
            charge.Tick(monster, members[0], context.RunRules.SimStep);
        }

        for (var i = 0; i < 2; i++)
        {
            var expected = members[i].MaxHp - monster.Damage * (1f - context.CharacterRepository.Get(members[i].CharacterId).Def);
            Assert.AreEqual(expected, members[i].Hp, Tolerance, $"member {i}");
        }

        Assert.AreEqual(members[2].MaxHp, members[2].Hp);
        Assert.AreEqual(460f * 0.7f, monster.Position.x, 460f * context.RunRules.SimStep);
    }

    [Test]
    public void ChargeCooldownAfterRecover()
    {
        var context = new TestRunContext();
        var bruno = KeepOnly(context, 0);
        bruno.Hp = 1e6f;
        var monster = context.CreateMonster(1, "boar", bruno.Position + Vector2.left * 100f);
        context.MonsterModel.Add(monster);
        var phaseTimeList = RecordPhaseChanges(context, monster, 6f);

        // Windup → Dash → Recover → Approach → Windup. 대기(cooldown 2초)는 Recover에 들 때 걸린다(기획서 A절)
        var recoverStart = phaseTimeList[2].Value;
        Assert.AreEqual(MonsterPhase.Recover, phaseTimeList[2].Key);
        Assert.AreEqual(MonsterPhase.Windup, phaseTimeList[4].Key);
        Assert.AreEqual(monster.Data.Charge.Cooldown, phaseTimeList[4].Value - recoverStart, context.RunRules.SimStep + Tolerance);
    }

    [Test]
    public void BombSwarmsTogether()
    {
        var context = new TestRunContext();
        var bruno = KeepOnly(context, 0);
        var near = context.CreateMonster(1, "puffer", bruno.Position + Vector2.left * 290f);
        var mate = context.CreateMonster(2, "puffer", near.Position + Vector2.left * 200f);
        var far = context.CreateMonster(3, "puffer", near.Position + Vector2.left * 1000f);
        context.MonsterModel.Add(near);
        context.MonsterModel.Add(mate);
        context.MonsterModel.Add(far);

        context.MonsterBehaviorService.Tick(context.RunRules.SimStep);

        var bomb = near.Data.Bomb;
        Assert.AreEqual(MonsterPhase.Swarm, near.Phase);
        Assert.AreEqual(MonsterPhase.Swarm, mate.Phase);
        Assert.AreEqual(MonsterPhase.Approach, far.Phase);
        Assert.AreEqual(bomb.SwarmSeconds - context.RunRules.SimStep, near.PhaseClock, Tolerance);

        // 무리 속도: 첫 걸음은 무리 방향을 min(1, dt × 7)만큼 섞어 225 × 그 비율
        var expectedStep = bomb.SwarmSpeed * context.RunRules.SimStep * Mathf.Min(1f, context.RunRules.SimStep * bomb.SteerRate);
        Assert.AreEqual(290f - expectedStep, Vector2.Distance(near.Position, bruno.Position), 0.5f);
    }

    [Test]
    public void BombExplodesAfterFuse()
    {
        var context = new TestRunContext();
        context.PartyService.CreateParty();
        var members = context.PartyModel.MemberList;
        members[1].Position = members[0].Position + Vector2.right * 30f;
        members[2].Position = members[0].Position + Vector2.up * 1000f;
        members[3].Position = members[0].Position + Vector2.down * 1000f;
        var monster = context.CreateMonster(1, "puffer", members[0].Position + Vector2.left * 50f);
        context.MonsterModel.Add(monster);
        var kills = 0;
        var fuseSteps = 0;

        while (context.MonsterModel.MonsterList.Count > 0 && fuseSteps < 100)
        {
            kills += context.MonsterService.RemoveDead();
            context.MonsterService.TickMove(context.RunRules.SimStep);
            fuseSteps++;
        }

        // 점화 1.2초 = 36걸음(실수 누적으로 한 걸음 늦을 수 있다)
        Assert.AreEqual(36, fuseSteps, 1);
        Assert.AreEqual(0, kills);
        Assert.Less(members[0].Hp, members[0].MaxHp);
        Assert.Less(members[1].Hp, members[1].MaxHp);
        Assert.AreEqual(members[2].MaxHp, members[2].Hp);
    }

    [Test]
    public void BombKilledDuringFuseDoesNotExplode()
    {
        var context = new TestRunContext();
        var bruno = KeepOnly(context, 0);
        var monster = context.CreateMonster(1, "puffer", bruno.Position + Vector2.left * 50f);
        context.MonsterModel.Add(monster);
        context.MonsterService.TickMove(context.RunRules.SimStep);
        Assert.AreEqual(MonsterPhase.Fuse, monster.Phase);

        // ④ 무기에 쓰러짐 → 처치로 빠진다
        monster.Hp = 0f;
        var kills = context.MonsterService.RemoveDead();
        StepBehavior(context, 2f);

        Assert.AreEqual(1, kills);
        Assert.AreEqual(bruno.MaxHp, bruno.Hp);
    }

    // index 대원만 살리고 나머지는 쓰러뜨린다
    private static PartyMember KeepOnly(TestRunContext context, int index)
    {
        context.PartyService.CreateParty();
        var members = context.PartyModel.MemberList;
        for (var i = 0; i < members.Count; i++)
        {
            if (i != index)
            {
                members[i].Hp = 0f;
            }
        }

        return members[index];
    }

    private static void StepBehavior(TestRunContext context, float seconds)
    {
        var steps = Mathf.RoundToInt(seconds / context.RunRules.SimStep);
        for (var i = 0; i < steps; i++)
        {
            context.MonsterBehaviorService.Tick(context.RunRules.SimStep);
        }
    }

    // 행동만 돌리며 대원 HP가 준 걸음 시각과 준 양
    private static List<float> StepAndRecordHits(TestRunContext context, PartyMember member, float seconds, out List<float> hpLossList)
    {
        var hitTimeList = new List<float>();
        hpLossList = new List<float>();
        var steps = Mathf.RoundToInt(seconds / context.RunRules.SimStep);
        for (var i = 1; i <= steps; i++)
        {
            var before = member.Hp;
            context.MonsterBehaviorService.Tick(context.RunRules.SimStep);
            if (member.Hp < before)
            {
                hitTimeList.Add(i * context.RunRules.SimStep);
                hpLossList.Add(before - member.Hp);
            }
        }

        return hitTimeList;
    }

    // 행동만 돌리며 상태가 바뀐 걸음 시각(바뀐 상태, 시각)
    private static List<KeyValuePair<MonsterPhase, float>> RecordPhaseChanges(TestRunContext context, Monster monster, float seconds)
    {
        var phaseTimeList = new List<KeyValuePair<MonsterPhase, float>>();
        var last = monster.Phase;
        var steps = Mathf.RoundToInt(seconds / context.RunRules.SimStep);
        for (var i = 1; i <= steps; i++)
        {
            context.MonsterBehaviorService.Tick(context.RunRules.SimStep);
            if (monster.Phase != last)
            {
                last = monster.Phase;
                phaseTimeList.Add(new KeyValuePair<MonsterPhase, float>(last, i * context.RunRules.SimStep));
            }
        }

        return phaseTimeList;
    }
}
