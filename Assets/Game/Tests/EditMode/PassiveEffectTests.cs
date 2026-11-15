using NUnit.Framework;
using UnityEngine;

// AC10 — 지속 효과(기획서 B6)
public class PassiveEffectTests
{
    [Test]
    public void Heal3HealsEvery3Seconds()
    {
        var context = Create(new[] { "t_heal3" });
        var member = context.PartyModel.MemberList[0];
        member.Hp = 100f;

        StepSeconds(context, 2.9f);
        Assert.AreEqual(100f, member.Hp, 1e-3f);
        StepSeconds(context, 0.1f);
        Assert.AreEqual(103f, member.Hp, 1e-3f);
        StepSeconds(context, 3f);
        Assert.AreEqual(106f, member.Hp, 1e-3f);
    }

    [Test]
    public void ShareUsesFasterProviderAndSwitchesWhenProviderFalls()
    {
        // 0: 성전(회복 4.8, 나눔), 1: 부적(회복 1, 나눔), 2: 받는 대원(아이템 없음)
        var context = Create(new[] { "t_scripture" }, new[] { "t_amulet" }, new string[0]);
        var members = context.PartyModel.MemberList;
        var receiver = members[2];
        receiver.Hp = 50f;

        StepSeconds(context, 3f);
        Assert.AreEqual(54.8f, receiver.Hp, 1e-3f);
        Assert.AreEqual(0, context.CombatModel.MemberStateList[2].ShareProvider);

        members[0].Hp = 0f;
        StepSeconds(context, 3f);
        Assert.AreEqual(1, context.CombatModel.MemberStateList[2].ShareProvider);
        Assert.AreEqual(55.8f, receiver.Hp, 1e-3f);
    }

    [Test]
    public void Shield20FillsEightSecondsAfterWaveStart()
    {
        var context = Create(new[] { "t_banner" });
        var member = context.PartyModel.MemberList[0];
        Assert.AreEqual(0f, member.Shield);

        StepSeconds(context, 7.9f);
        Assert.AreEqual(0f, member.Shield);
        StepSeconds(context, 0.1f);
        Assert.AreEqual(20f, member.Shield, 1e-4f);
    }

    [Test]
    public void StartShield15OnWaveOpen()
    {
        var context = Create(new[] { "t_helmet" });

        Assert.AreEqual(15f, context.PartyModel.MemberList[0].Shield, 1e-4f);
    }

    [Test]
    public void StartShield30AndShield20RefillsTo20NotCap()
    {
        var context = Create(new[] { "t_start30", "t_banner" });
        var member = context.PartyModel.MemberList[0];
        Assert.AreEqual(30f, member.Shield, 1e-4f);

        member.Shield = 0f;
        StepSeconds(context, 8f);
        Assert.AreEqual(20f, member.Shield, 1e-4f);
    }

    [Test]
    public void PulseHits10Every3SecondsWithinRadius100()
    {
        var context = Create(new[] { "t_orb" });
        var origin = context.PartyModel.MemberList[0].Position;
        var near = context.AddMonster(1, origin + new Vector2(50f, 0f));
        var far = context.AddMonster(2, origin + new Vector2(150f, 0f));
        var startHp = near.Hp;

        context.CombatService.Step(context.RunRules.SimStep);
        Assert.AreEqual(startHp - 10f, near.Hp, 1e-3f);
        // 쿨은 걸음 시간을 빼서 0 이하에서 돈다(1/30초 걸음의 실수 오차로 정확히 3초 걸음에 닿을지는 한 걸음 갈릴 수 있다)
        StepSeconds(context, 2.9f);
        Assert.AreEqual(startHp - 10f, near.Hp, 1e-3f);
        StepSeconds(context, 0.2f);
        Assert.AreEqual(startHp - 20f, near.Hp, 1e-3f);
        Assert.AreEqual(startHp, far.Hp);
    }

    private static TestRunContext Create(params string[][] itemIdsPerMember)
    {
        var context = new TestRunContext(CombatTestItems.CreateRepository(
            CombatTestItems.Passive("t_heal3", "heal", 3f),
            CombatTestItems.Passive("t_start30", "", 0f, "startShield", 30f)));
        context.SetupParty(itemIdsPerMember);
        return context;
    }

    private static void StepSeconds(TestRunContext context, float seconds)
    {
        context.StepCombat(Mathf.RoundToInt(seconds / context.RunRules.SimStep), context.RunRules.SimStep);
    }
}
