using NUnit.Framework;

// AC14 — 판·웨이브 시작(기획서 B7)
public class CombatWaveTests
{
    [Test]
    public void NewRunStartsWithZeroCooldownsCountsAndFullHp()
    {
        var context = new TestRunContext();
        context.RunService.StartRun(3, 4f);
        context.StepSeconds(15f);
        Assert.Greater(context.CombatModel.GetFireCount("sword") + context.CombatModel.GetFireCount("shortbow"), 0);

        // 판을 끝낸다: 모두 쓰러뜨리고 한 걸음 → 패배 → 로비 → 새 판
        foreach (var member in context.PartyModel.MemberList)
        {
            member.Hp = 0f;
        }

        context.RunService.Step(context.RunRules.SimStep);
        Assert.AreEqual(RunPhase.Result, context.RunModel.Phase.CurrentValue);
        Assert.AreEqual(0, context.CombatModel.ProjectileList.Count);
        context.RunService.ReturnToLobby();
        Assert.AreEqual(0, context.CombatModel.ProjectileList.Count);
        context.RunService.StartRun(3, 4f);

        foreach (var itemId in new[] { "sword", "shortbow", "firestaff", "relic" })
        {
            Assert.AreEqual(0, context.CombatModel.GetFireCount(itemId), itemId);
        }

        Assert.AreEqual(0, context.CombatModel.ReviveCount.CurrentValue);
        Assert.AreEqual(-1, context.CombatModel.LastRevivedMember);
        for (var i = 0; i < context.PartyModel.MemberList.Count; i++)
        {
            var member = context.PartyModel.MemberList[i];
            Assert.AreEqual(member.MaxHp, member.Hp, $"member {i}");
            Assert.AreEqual(0f, context.CombatModel.MemberStateList[i].SlotList[0].RemainingCooldown, $"member {i}");
        }
    }

    [Test]
    public void WaveOpenKeepsCooldownsOfUnchangedMembersOnly()
    {
        var context = new TestRunContext();
        context.PartyService.CreateParty();
        context.CombatService.ResetRun();
        context.CombatService.PrepareWave();
        foreach (var state in context.CombatModel.MemberStateList)
        {
            state.SlotList[0].RemainingCooldown = 0.5f;
        }

        context.PartyModel.MemberList[1].Equip("sword");
        context.CombatService.PrepareWave();

        var states = context.CombatModel.MemberStateList;
        Assert.AreEqual(0.5f, states[0].SlotList[0].RemainingCooldown);
        Assert.AreEqual(2, states[1].SlotList.Count);
        Assert.AreEqual(0f, states[1].SlotList[0].RemainingCooldown);
        Assert.AreEqual(0f, states[1].SlotList[1].RemainingCooldown);
        Assert.AreEqual(0.5f, states[2].SlotList[0].RemainingCooldown);
    }

    [Test]
    public void MaxHpDropClampsHpAndWaveOpenDoesNotHeal()
    {
        var placement = new FakePlacementInfo();
        placement.SetGrade(0, 0, 2f);
        var context = new TestRunContext(CombatTestItems.CreateRepository(), placement);
        context.SetupParty(new[] { "t_chainmail" });
        var member = context.PartyModel.MemberList[0];
        // 브루노 400 + (45 + 30) × 2
        Assert.AreEqual(550f, member.MaxHp, 1e-3f);

        member.Hp = 300f;
        context.CombatService.PrepareWave();
        Assert.AreEqual(300f, member.Hp, "no heal between waves");

        placement.SetGrade(0, 0, 1f);
        member.Hp = 520f;
        context.CombatService.PrepareWave();
        Assert.AreEqual(475f, member.MaxHp, 1e-3f);
        Assert.AreEqual(475f, member.Hp, 1e-3f);
    }

    [Test]
    public void ProjectilesClearedWhenWaveEnds()
    {
        var context = new TestRunContext();
        context.RunService.StartRun(5, 1f);
        var sawProjectile = false;
        while (context.RunModel.Phase.CurrentValue == RunPhase.Battle)
        {
            context.RunService.Step(context.RunRules.SimStep);
            sawProjectile |= context.CombatModel.ProjectileList.Count > 0;
        }

        Assert.IsTrue(sawProjectile);
        Assert.AreEqual(RunPhase.Prep, context.RunModel.Phase.CurrentValue);
        Assert.AreEqual(0, context.CombatModel.ProjectileList.Count);
        Assert.AreEqual(0, context.CombatModel.BurstList.Count);
    }
}
