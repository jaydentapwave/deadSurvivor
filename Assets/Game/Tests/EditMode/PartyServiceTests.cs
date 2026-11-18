using NUnit.Framework;

// AC12 — 편성과 받는 피해
public class PartyServiceTests
{
    [Test]
    public void PartyFollowsDataOrderWithFullHpAndStartItemOnly()
    {
        var context = new TestRunContext();
        context.PartyService.CreateParty();

        var members = context.PartyModel.MemberList;
        CollectionAssert.AreEqual(new[] { "bruno", "lena", "mira", "bolt" },
            new[] { members[0].CharacterId, members[1].CharacterId, members[2].CharacterId, members[3].CharacterId });
        foreach (var member in members)
        {
            var data = context.CharacterRepository.Get(member.CharacterId);
            Assert.AreEqual(data.Hp, member.Hp);
            Assert.AreEqual(data.Hp, member.MaxHp);
            CollectionAssert.AreEqual(new[] { data.StartItem }, member.EquippedItemIdList);
        }
    }

    [Test]
    public void DamageIsReducedByDefense()
    {
        var context = new TestRunContext();
        context.PartyService.CreateParty();
        var bruno = context.PartyModel.MemberList[0];
        var lena = context.PartyModel.MemberList[1];

        context.PartyService.ApplyDamage(bruno, 100f);
        context.PartyService.ApplyDamage(lena, 100f);

        // 브루노 방어 0.15 → 85, 레나 방어 0 → 100(HP 110)
        Assert.AreEqual(400f - 85f, bruno.Hp, 0.001f);
        Assert.AreEqual(10f, lena.Hp, 0.001f);
    }

    [Test]
    public void HpDoesNotGoBelowZero()
    {
        var context = new TestRunContext();
        context.PartyService.CreateParty();
        var lena = context.PartyModel.MemberList[1];

        context.PartyService.ApplyDamage(lena, 1000f);

        Assert.AreEqual(0f, lena.Hp);
        Assert.IsFalse(lena.IsAlive);
        Assert.AreEqual(3, context.PartyService.AliveCount);
    }
}
