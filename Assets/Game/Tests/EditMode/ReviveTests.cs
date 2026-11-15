using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

// AC12·AC13 — 쓰러짐과 무료 부활(기획서 E절)
public class ReviveTests
{
    [Test]
    public void DownedMemberSlotsDoNotTick()
    {
        var context = Create(new[] { "t_sword" });
        var member = context.PartyModel.MemberList[0];
        context.AddMonster(1, member.Position + new Vector2(30f, 0f));
        var slot = context.CombatModel.MemberStateList[0].SlotList[0];
        slot.RemainingCooldown = 0.5f;
        member.Hp = 0f;

        context.StepCombat(30, context.RunRules.SimStep);

        Assert.AreEqual(0.5f, slot.RemainingCooldown);
        Assert.AreEqual(0, context.CombatModel.GetFireCount("t_sword"));
    }

    [Test]
    public void DownedMemberIsNotHealedNorShareProviderNorReceiver()
    {
        // 0: 성물(단일 회복), 1: 성전(나눔 제공자), 2: 쓰러짐, 3: 부적(나눔)
        var context = Create(new[] { "t_relic" }, new[] { "t_scripture" }, new string[0], new[] { "t_amulet" });
        var members = context.PartyModel.MemberList;
        members[2].Hp = 0f;

        context.StepCombat(100, context.RunRules.SimStep);

        Assert.AreEqual(0f, members[2].Hp);
        Assert.AreEqual(-1, context.CombatModel.MemberStateList[2].ShareProvider, "downed receiver");
        Assert.AreEqual(0, context.CombatModel.GetFireCount("t_relic"), "no hurt living member");

        members[1].Hp = 0f;
        context.CombatService.Step(context.RunRules.SimStep);
        Assert.AreEqual(3, context.CombatModel.MemberStateList[0].ShareProvider, "downed provider replaced");
    }

    [Test]
    public void CanChangeEquipmentOnlyWhenAlive()
    {
        var context = Create(new[] { "t_sword" }, new[] { "t_bow" });
        context.PartyModel.MemberList[1].Hp = 0f;

        Assert.IsTrue(context.PartyService.CanChangeEquipment(0));
        Assert.IsFalse(context.PartyService.CanChangeEquipment(1));
    }

    [Test]
    public void ReviveInPrepRestoresHpShieldAndCooldownsRepeatedly()
    {
        var context = Create(new[] { "t_sword", "t_bow" }, new[] { "t_bow" });
        var member = context.PartyModel.MemberList[0];
        var state = context.CombatModel.MemberStateList[0];
        context.RunModel.SetPhase(RunPhase.Prep);

        for (var round = 1; round <= 2; round++)
        {
            member.Hp = 0f;
            member.Shield = 7f;
            foreach (var slot in state.SlotList)
            {
                slot.RemainingCooldown = 0.4f;
                slot.LastPeriod = 1f;
            }

            Assert.IsTrue(context.ReviveService.Revive(0), $"round {round}");

            Assert.AreEqual(member.MaxHp, member.Hp);
            Assert.AreEqual(0f, member.Shield);
            foreach (var slot in state.SlotList)
            {
                Assert.AreEqual(0f, slot.RemainingCooldown);
                Assert.AreEqual(0f, slot.LastPeriod);
            }

            Assert.AreEqual(0, context.CombatModel.LastRevivedMember);
            Assert.AreEqual(round, context.CombatModel.ReviveCount.CurrentValue);
        }
    }

    [Test]
    public void ReviveIsRejectedInBattleAndForLivingMember()
    {
        var context = Create(new[] { "t_sword" }, new[] { "t_bow" });
        var members = context.PartyModel.MemberList;
        members[0].Hp = 0f;
        context.RunModel.SetPhase(RunPhase.Battle);

        LogAssert.Expect(LogType.Warning, new Regex(@"\[ReviveService\] Cannot revive member 0"));
        Assert.IsFalse(context.ReviveService.Revive(0));
        Assert.AreEqual(0f, members[0].Hp);

        context.RunModel.SetPhase(RunPhase.Prep);
        var hp = members[1].Hp;
        LogAssert.Expect(LogType.Warning, new Regex(@"\[ReviveService\] Cannot revive member 1"));
        Assert.IsFalse(context.ReviveService.Revive(1));
        Assert.AreEqual(hp, members[1].Hp);
        Assert.AreEqual(0, context.CombatModel.ReviveCount.CurrentValue);
        Assert.AreEqual(-1, context.CombatModel.LastRevivedMember);
    }

    private static TestRunContext Create(params string[][] itemIdsPerMember)
    {
        var context = new TestRunContext(CombatTestItems.CreateRepository());
        context.SetupParty(itemIdsPerMember);
        return context;
    }
}
