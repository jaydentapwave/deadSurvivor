using NUnit.Framework;

// AC15 — 같은 시드·같은 장착이면 프레임 나누기와 상관없이 같은 결과(기획서 B8). 투사체(관통·갈래)·폭발·연쇄·회복·나눔·보호막 포함
public class CombatDeterminismTests
{
    private static readonly string[] CountedItemIds =
        { "sword", "shortbow", "firestaff", "relic", "t_wand", "t_censer", "t_orb" };

    [Test]
    public void SameSeedSameResultAcrossFrameSplits()
    {
        var bySixtieth = CreateRun();
        var bySecond = CreateRun();

        for (var second = 0; second < 25; second++)
        {
            for (var frame = 0; frame < 60; frame++)
            {
                bySixtieth.RunService.AdvanceFrame(1f / 60f);
            }

            bySecond.RunService.AdvanceFrame(1f);
        }

        Assert.Greater(bySecond.RunModel.Kills.CurrentValue, 0);
        Assert.Greater(bySecond.CombatModel.GetFireCount("t_wand"), 0, "chain fired");
        Assert.Greater(bySecond.CombatModel.GetFireCount("firestaff"), 0, "explosion fired");
        Assert.Greater(bySecond.CombatModel.MemberStateList[2].ShieldRefill, 0f, "shield item active");
        Assert.AreEqual(bySecond.RunModel.Kills.CurrentValue, bySixtieth.RunModel.Kills.CurrentValue);
        Assert.AreEqual(bySecond.MonsterModel.MonsterList.Count, bySixtieth.MonsterModel.MonsterList.Count);
        foreach (var itemId in CountedItemIds)
        {
            Assert.AreEqual(bySecond.CombatModel.GetFireCount(itemId), bySixtieth.CombatModel.GetFireCount(itemId), itemId);
        }

        for (var i = 0; i < bySecond.PartyModel.MemberList.Count; i++)
        {
            Assert.AreEqual(bySecond.PartyModel.MemberList[i].Hp, bySixtieth.PartyModel.MemberList[i].Hp, $"hp {i}");
            Assert.AreEqual(bySecond.PartyModel.MemberList[i].Shield, bySixtieth.PartyModel.MemberList[i].Shield, $"shield {i}");
        }
    }

    // 시작 아이템 + 대원마다 시험 아이템을 더 들고 웨이브를 다시 연다(장착이 바뀐 대원만 칸을 새로 만든다)
    private static TestRunContext CreateRun()
    {
        var context = new TestRunContext(CombatTestItems.CreateRepository(), new FakePlacementInfo { IsAllAdjacent = true });
        context.RunService.StartRun(7, 2f);
        var members = context.PartyModel.MemberList;
        members[0].Equip("t_wand");
        members[0].Equip("t_orb");
        members[1].Equip("t_pierce");
        members[1].Equip("t_split");
        members[2].Equip("t_helmet");
        members[2].Equip("t_banner");
        members[3].Equip("t_censer");
        members[3].Equip("t_scripture");
        context.CombatService.PrepareWave();
        return context;
    }
}
