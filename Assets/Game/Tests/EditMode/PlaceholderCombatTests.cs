using NUnit.Framework;
using UnityEngine;

// AC10·AC11·AC13 — 임시 전투(기획서 E절). F4: 임시 적 → slime 데이터(F4 기획서 "기존 테스트" 기대 변경 허용 목록)
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
        var slime = context.MonsterRepository.Get("slime");
        context.PartyService.CreateParty();
        var bruno = context.PartyModel.MemberList[0];
        var contact = slime.Radius + slime.ContactGap;
        context.MonsterModel.Add(context.CreateMonster(1, "slime", bruno.Position + Vector2.left * contact));

        context.MonsterService.TickMove(context.RunRules.SimStep);

        var expected = bruno.MaxHp - slime.Damage * (1f - context.CharacterRepository.Get("bruno").Def);
        Assert.AreEqual(expected, bruno.Hp, 0.001f);

        // 쿨(attackInterval) 동안은 다시 때리지 않는다
        context.MonsterService.TickMove(context.RunRules.SimStep);
        Assert.AreEqual(expected, bruno.Hp, 0.001f);
    }

    [Test]
    public void MonsterApproachesAndStopsAtContact()
    {
        var context = new TestRunContext();
        var slime = context.MonsterRepository.Get("slime");
        context.PartyService.CreateParty();
        var bruno = context.PartyModel.MemberList[0];
        var monster = context.CreateMonster(1, "slime", bruno.Position + Vector2.left * 100f);
        context.MonsterModel.Add(monster);

        for (var i = 0; i < 90; i++)
        {
            context.MonsterService.TickMove(context.RunRules.SimStep);
        }

        var contact = slime.Radius + slime.ContactGap;
        Assert.AreEqual(contact, Vector2.Distance(monster.Position, bruno.Position), 0.01f);
        Assert.Less(bruno.Hp, bruno.MaxHp);
    }

    [Test]
    public void DeadMemberDoesNotAttackAndIsNotTargeted()
    {
        var context = new TestRunContext();
        var slime = context.MonsterRepository.Get("slime");
        context.PartyService.CreateParty();
        var members = context.PartyModel.MemberList;
        var bruno = members[0];
        bruno.Hp = 0f;
        // 브루노 바로 옆(브루노 사거리 안, 다른 대원 사거리 밖)에 적을 둔다
        var monster = context.CreateMonster(1, "slime", bruno.Position + new Vector2(-30f, -30f));
        context.MonsterModel.Add(monster);
        for (var i = 1; i < members.Count; i++)
        {
            members[i].Position += new Vector2(1000f, 1000f);
        }

        context.BasicWeaponService.Tick(context.RunRules.SimStep);
        Assert.AreEqual(slime.Hp, monster.Hp);

        var target = context.PartyService.FindNearestAlive(monster.Position);
        Assert.AreNotSame(bruno, target);
        Assert.IsNotNull(target);
    }

    [Test]
    public void AliveMemberAttacksNearestMonsterInRange()
    {
        var context = new TestRunContext();
        var slime = context.MonsterRepository.Get("slime");
        context.PartyService.CreateParty();
        var lena = context.PartyModel.MemberList[1];
        var near = context.CreateMonster(1, "slime", lena.Position + Vector2.right * 100f);
        var far = context.CreateMonster(2, "slime", lena.Position + Vector2.right * 300f);
        var outOfRange = context.CreateMonster(3, "slime", lena.Position + Vector2.right * 400f);
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
        Assert.AreEqual(slime.Hp - damage, near.Hp, 0.001f);
        Assert.AreEqual(slime.Hp, far.Hp);
        Assert.AreEqual(slime.Hp, outOfRange.Hp);

        // 다음 발사는 interval(0.7초) 뒤: 1/30초 걸음으로 21걸음째
        var steps = 0;
        while (near.Hp > slime.Hp - 2f * damage + 0.001f && steps < 100)
        {
            context.BasicWeaponService.Tick(context.RunRules.SimStep);
            steps++;
        }

        Assert.AreEqual(context.CombatData.Weapon.Interval, steps * context.RunRules.SimStep, 0.001f);
    }

    // F4: 추가로 나오는 적은 slime·boar이고 정규 생성 수에 들지 않는다(기대 변경 허용 목록)
    [Test]
    public void BurningTimeSpawnsExtra()
    {
        var withoutBurning = new TestRunContext();
        withoutBurning.SpawnWholeWave(withoutBurning.BeginWaveOnly(5, 3));
        var context = new TestRunContext();
        var waveData = context.BeginWaveOnly(6, 3);
        context.SpawnWholeWave(waveData);

        Assert.AreEqual(withoutBurning.WaveRepository.WaveDataList[4].SpawnCount, withoutBurning.MonsterModel.MonsterList.Count);
        Assert.AreEqual(0, withoutBurning.MonsterModel.BurningSpawnedCount);
        Assert.AreEqual(waveData.SpawnCount, context.MonsterModel.SpawnedCount);
        Assert.Greater(context.MonsterModel.BurningSpawnedCount, 0);
        Assert.AreEqual(waveData.SpawnCount + context.MonsterModel.BurningSpawnedCount, context.MonsterModel.MonsterList.Count);
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
        context.SpawnWholeWave(context.BeginWaveOnly(wave, 3, enemyCountScale));
        return context.MonsterModel.MonsterList.Count;
    }
}
