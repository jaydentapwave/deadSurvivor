using NUnit.Framework;
using UnityEngine;

// AC10·AC11·AC13 — 임시 전투(기획서 E절)
public class PlaceholderCombatTests
{
    [Test]
    public void TenSecondsWithDefaultSettingsKillsAtLeastOne()
    {
        var context = new TestRunContext();
        context.RunService.StartRun(1, context.RunRules.DefaultEnemyCountScale);

        context.StepSeconds(10f);

        Assert.AreEqual(RunPhase.Battle, context.RunModel.Phase.CurrentValue);
        Assert.GreaterOrEqual(context.RunModel.Kills.CurrentValue, 1);
    }

    [Test]
    public void ContactDamagesMember()
    {
        var context = new TestRunContext();
        context.PartyService.CreateParty();
        var bruno = context.PartyModel.MemberList[0];
        var contact = context.CombatData.Enemy.Radius + context.CombatData.Enemy.ContactGap;
        context.MonsterModel.Add(new Monster(1, bruno.Position + Vector2.left * contact, context.CombatData.Enemy.Hp));

        context.MonsterService.TickMove(context.RunRules.SimStep);

        var expected = bruno.MaxHp - context.CombatData.Enemy.Damage * (1f - context.CharacterRepository.Get("bruno").Def);
        Assert.AreEqual(expected, bruno.Hp, 0.001f);

        // 쿨(attackInterval) 동안은 다시 때리지 않는다
        context.MonsterService.TickMove(context.RunRules.SimStep);
        Assert.AreEqual(expected, bruno.Hp, 0.001f);
    }

    [Test]
    public void MonsterApproachesAndStopsAtContact()
    {
        var context = new TestRunContext();
        context.PartyService.CreateParty();
        var bruno = context.PartyModel.MemberList[0];
        var monster = new Monster(1, bruno.Position + Vector2.left * 100f, context.CombatData.Enemy.Hp);
        context.MonsterModel.Add(monster);

        for (var i = 0; i < 90; i++)
        {
            context.MonsterService.TickMove(context.RunRules.SimStep);
        }

        var contact = context.CombatData.Enemy.Radius + context.CombatData.Enemy.ContactGap;
        Assert.AreEqual(contact, Vector2.Distance(monster.Position, bruno.Position), 0.01f);
        Assert.Less(bruno.Hp, bruno.MaxHp);
    }

    [Test]
    public void DeadMemberDoesNotAttackAndIsNotTargeted()
    {
        var context = new TestRunContext();
        context.PartyService.CreateParty();
        var members = context.PartyModel.MemberList;
        var bruno = members[0];
        bruno.Hp = 0f;
        // 브루노 바로 옆(브루노 사거리 안, 다른 대원 사거리 밖)에 적을 둔다
        var monster = new Monster(1, bruno.Position + new Vector2(-30f, -30f), context.CombatData.Enemy.Hp);
        context.MonsterModel.Add(monster);
        for (var i = 1; i < members.Count; i++)
        {
            members[i].Position += new Vector2(1000f, 1000f);
        }

        context.BasicWeaponService.Tick(context.RunRules.SimStep);
        Assert.AreEqual(context.CombatData.Enemy.Hp, monster.Hp);

        var target = context.PartyService.FindNearestAlive(monster.Position);
        Assert.AreNotSame(bruno, target);
        Assert.IsNotNull(target);
    }

    [Test]
    public void AliveMemberAttacksNearestMonsterInRange()
    {
        var context = new TestRunContext();
        context.PartyService.CreateParty();
        var lena = context.PartyModel.MemberList[1];
        var near = new Monster(1, lena.Position + Vector2.right * 100f, context.CombatData.Enemy.Hp);
        var far = new Monster(2, lena.Position + Vector2.right * 300f, context.CombatData.Enemy.Hp);
        var outOfRange = new Monster(3, lena.Position + Vector2.right * 400f, context.CombatData.Enemy.Hp);
        context.MonsterModel.Add(far);
        context.MonsterModel.Add(near);
        context.MonsterModel.Add(outOfRange);
        for (var i = 0; i < context.PartyModel.MemberList.Count; i++)
        {
            if (i != 1)
            {
                context.PartyModel.MemberList[i].Hp = 0f;
            }
        }

        context.BasicWeaponService.Tick(context.RunRules.SimStep);

        var damage = context.CombatData.Weapon.Damage * context.CharacterRepository.Get("lena").Atk;
        Assert.AreEqual(context.CombatData.Enemy.Hp - damage, near.Hp, 0.001f);
        Assert.AreEqual(context.CombatData.Enemy.Hp, far.Hp);
        Assert.AreEqual(context.CombatData.Enemy.Hp, outOfRange.Hp);

        // 다음 발사는 interval(0.7초) 뒤: 1/30초 걸음으로 21걸음째
        var steps = 0;
        while (near.Hp > context.CombatData.Enemy.Hp - 2f * damage + 0.001f && steps < 100)
        {
            context.BasicWeaponService.Tick(context.RunRules.SimStep);
            steps++;
        }

        Assert.AreEqual(context.CombatData.Weapon.Interval, steps * context.RunRules.SimStep, 0.001f);
    }

    [Test]
    public void BurningTimeSpawnsExtra()
    {
        var withBurning = CountSpawnsInWave(6);
        var withoutBurning = CountSpawnsInWave(5);
        var context = new TestRunContext();
        var normalCount = context.CombatData.SpawnCount;

        Assert.AreEqual(normalCount, withoutBurning);
        Assert.Greater(withBurning, normalCount);

        // 창(10초) 동안 간격(일반 간격 × 0.5)마다 두 방향에서 1마리씩 = 짝수, 간격 수 × 2
        var waveData = context.WaveRepository.WaveDataList[5];
        var interval = (waveData.Duration - 4f) / (normalCount - 1) * context.CombatData.BurningIntervalScale;
        var ticks = Mathf.CeilToInt(context.RunRules.BurningSeconds / interval);
        Assert.AreEqual(normalCount + ticks * 2, withBurning);
    }

    [Test]
    public void NoEnemiesWhenEnemyCountScaleIsZero()
    {
        Assert.AreEqual(0, CountSpawnsInWave(6, 0f));
    }

    [Test]
    public void SameSeedSameResultAcrossFrameSplits()
    {
        var bySixtieth = new TestRunContext();
        var bySecond = new TestRunContext();
        bySixtieth.RunService.StartRun(7, 2f);
        bySecond.RunService.StartRun(7, 2f);

        for (var second = 0; second < 25; second++)
        {
            for (var frame = 0; frame < 60; frame++)
            {
                bySixtieth.RunService.AdvanceFrame(1f / 60f);
            }

            bySecond.RunService.AdvanceFrame(1f);
        }

        Assert.Greater(bySecond.RunModel.Kills.CurrentValue, 0);
        Assert.AreEqual(bySecond.RunModel.Kills.CurrentValue, bySixtieth.RunModel.Kills.CurrentValue);
        Assert.AreEqual(bySecond.RunModel.RemainingSeconds.CurrentValue, bySixtieth.RunModel.RemainingSeconds.CurrentValue);
        Assert.AreEqual(bySecond.MonsterModel.MonsterList.Count, bySixtieth.MonsterModel.MonsterList.Count);
        for (var i = 0; i < bySecond.PartyModel.MemberList.Count; i++)
        {
            Assert.AreEqual(bySecond.PartyModel.MemberList[i].Hp, bySixtieth.PartyModel.MemberList[i].Hp, $"member {i}");
        }
    }

    // 웨이브 하나를 생성만 돌려(무기·이동 없이) 나온 적 수를 센다
    private static int CountSpawnsInWave(int wave, float enemyCountScale = 1f)
    {
        var context = new TestRunContext();
        context.PartyService.CreateParty();
        var waveData = context.WaveRepository.WaveDataList[wave - 1];
        context.MonsterService.BeginRun(3);
        context.MonsterService.BeginWave(waveData.Duration, enemyCountScale);
        var burningStart = waveData.Duration * context.RunRules.BurningStartRatio;
        var remaining = waveData.Duration;
        while (remaining > 0f)
        {
            var delta = Mathf.Min(context.RunRules.SimStep, remaining);
            remaining -= delta;
            var elapsed = waveData.Duration - remaining;
            var isBurningTime = waveData.HasBurningTime && elapsed >= burningStart
                && elapsed < burningStart + context.RunRules.BurningSeconds;
            context.MonsterService.TickSpawn(delta, elapsed, isBurningTime, burningStart);
        }

        return context.MonsterModel.MonsterList.Count;
    }
}
