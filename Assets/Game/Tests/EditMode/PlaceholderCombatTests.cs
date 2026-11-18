using NUnit.Framework;
using UnityEngine;

// AC10·AC11·AC13 — 임시 전투(기획서 E절). 기본 무기 시험 셋(10초 처치, 쓰러진 대원, 가장 가까운 적)은 F3이 ItemActivationTests로 옮겼다(F3 기획서 "기대 변경 허용"). F4: 임시 적 → slime 데이터(F4 기획서 "기존 테스트" 기대 변경 허용 목록)
public class PlaceholderCombatTests
{
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
