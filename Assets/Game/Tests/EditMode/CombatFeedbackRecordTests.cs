using NUnit.Framework;
using UnityEngine;

// F6 AC5 — 피해 적용이 기록을 남기고 값은 그대로(기획서 A절). 계산이 그대로인지는 기존 PartyServiceTests·CombatDamageTests·
// MonsterBehaviorTests·CombatDeterminismTests가 기대값 변경 없이 본다
public class CombatFeedbackRecordTests
{
    [Test]
    public void MemberDamageLeavesOneRecordWithTakenAndHp()
    {
        var context = new TestRunContext();
        context.PartyService.CreateParty();
        var member = context.PartyModel.MemberList[1];
        var hpBefore = member.Hp;

        context.PartyService.ApplyDamage(member, 10f);

        var hitList = context.CombatFeedbackModel.MemberHitList;
        Assert.AreEqual(1, hitList.Count);
        Assert.AreEqual(1, hitList[0].MemberIndex);
        Assert.AreEqual(10f * (1f - member.Defense), hitList[0].Taken, 1e-5f);
        Assert.AreEqual(hpBefore, hitList[0].HpBefore, 1e-5f);
        Assert.AreEqual(member.Hp, hitList[0].HpAfter, 1e-5f);
        Assert.AreEqual(hpBefore - 10f * (1f - member.Defense), member.Hp, 1e-4f);
        Assert.IsTrue(context.CombatFeedbackModel.HasPendingHitReaction);
    }

    [Test]
    public void ShieldAbsorbedHitIsRecordedWithSameHp()
    {
        var context = new TestRunContext();
        context.PartyService.CreateParty();
        var member = context.PartyModel.MemberList[0];
        member.Shield = 1000f;
        var hp = member.Hp;

        context.PartyService.ApplyDamage(member, 10f);

        var hit = context.CombatFeedbackModel.MemberHitList[0];
        Assert.Greater(hit.Taken, 0f);
        Assert.AreEqual(hp, hit.HpBefore);
        Assert.AreEqual(hp, hit.HpAfter);
        Assert.AreEqual(hp, member.Hp);
    }

    [Test]
    public void MonsterDamageLeavesOneRecordWithAmountAndPosition()
    {
        var context = new TestRunContext();
        var monster = context.AddMonster(5, new Vector2(30f, -12f), 100f);

        context.MonsterService.ApplyDamage(monster, 30f);

        Assert.AreEqual(70f, monster.Hp, 1e-5f);
        var hitList = context.CombatFeedbackModel.MonsterHitList;
        Assert.AreEqual(1, hitList.Count);
        Assert.AreEqual(5, hitList[0].MonsterId);
        Assert.AreEqual(30f, hitList[0].Amount, 1e-5f);
        Assert.AreEqual(new Vector2(30f, -12f), hitList[0].Position);
        Assert.AreSame(monster.Data, hitList[0].Data);
    }

    [Test]
    public void DownedMonsterIsIgnoredAndNotRecorded()
    {
        var context = new TestRunContext();
        var monster = context.AddMonster(5, Vector2.zero, 100f);
        monster.Hp = 0f;

        context.MonsterService.ApplyDamage(monster, 30f);

        Assert.AreEqual(0f, monster.Hp);
        Assert.AreEqual(0, context.CombatFeedbackModel.MonsterHitList.Count);
    }

    [Test]
    public void WeaponDamageInCombatEqualsRecordedSum()
    {
        var context = new TestRunContext();
        context.SetupParty(new[] { "sword" });
        var member = context.PartyModel.MemberList[0];
        // HP 1e6(기본값)은 float 간격이 0.0625라 합 비교가 흐려진다. 90걸음 동안 쓰러지지 않을 1000
        var monster = context.AddMonster(1, member.Position + Vector2.right * 10f, 1000f);
        var startHp = monster.Hp;

        context.StepCombat(90, context.RunRules.SimStep);

        var recorded = 0f;
        foreach (var hit in context.CombatFeedbackModel.MonsterHitList)
        {
            Assert.AreEqual(1, hit.MonsterId);
            recorded += hit.Amount;
        }

        Assert.Greater(context.CombatFeedbackModel.MonsterHitList.Count, 0);
        Assert.AreEqual(startHp - monster.Hp, recorded, 1e-2f);
    }
}
