using NUnit.Framework;

// AC11 — 배치 정보(기획서 A3): 실제 BagService·BagPlacementInfo와 F3 EquipmentStatService로 F3 AC7과 같은 값
public class BagPlacementInfoTests
{
    private const int Lena = 1;

    [Test]
    public void UncommonShortbowDamage()
    {
        var context = Setup();
        var bowUid = BagTestItems.BagItemAt(context, Lena, 0).Uid;
        var copyUid = BagTestItems.AddToStash(context, "b_bow");
        Assert.IsTrue(context.BagService.Merge(copyUid, bowUid, out _));

        Assert.AreEqual(1.4f, context.EquipmentStatService.GetGradeMultiplier(Lena, 0), 1e-6f);
        Assert.AreEqual(21.84f, BowPower(context), 1e-4f);
    }

    [Test]
    public void WhetstoneNextToShortbow()
    {
        var context = Setup();
        var whetUid = BagTestItems.AddToStash(context, "b_whet");
        Assert.IsTrue(context.BagService.Drop(whetUid, Lena, 0, 1, 0, out _));

        Assert.IsTrue(context.EquipmentStatService.IsBonusApplied(Lena, Equipped(context), 1, 0));
        Assert.AreEqual(18.72f, BowPower(context), 1e-4f);
    }

    [Test]
    public void WhetstoneOneCellAway()
    {
        var context = Setup();
        var whetUid = BagTestItems.AddToStash(context, "b_whet");
        Assert.IsTrue(context.BagService.Drop(whetUid, Lena, 0, 2, 0, out _));

        Assert.IsFalse(context.EquipmentStatService.IsBonusApplied(Lena, Equipped(context), 1, 0));
        Assert.AreEqual(16.536f, BowPower(context), 1e-4f);
    }

    private static TestRunContext Setup()
    {
        var context = BagTestItems.CreateContext();
        BagTestItems.SetupBags(context, new[] { "b_sword" }, new[] { "b_bow" });
        return context;
    }

    private static System.Collections.Generic.IReadOnlyList<string> Equipped(TestRunContext context) =>
        context.PartyModel.MemberList[Lena].EquippedItemIdList;

    // 단궁(칸 0) 피해 = 13 × 등급 배수 × 레나 공격 1.2 × (1 + 공격 증가)
    private static float BowPower(TestRunContext context)
    {
        var lena = context.CharacterRepository.Get(context.PartyModel.MemberList[Lena].CharacterId);
        var stats = context.EquipmentStatService.Calculate(Lena, lena, Equipped(context), 0);
        var bow = context.ItemRepository.Get("b_bow");
        return context.EquipmentStatService.GetPower(bow, context.EquipmentStatService.GetGradeMultiplier(Lena, 0), lena, stats);
    }
}
