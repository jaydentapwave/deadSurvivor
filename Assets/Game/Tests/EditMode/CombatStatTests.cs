using NUnit.Framework;

// AC7 — 능력치 계산(기획서 B3·B4)
public class CombatStatTests
{
    private const int Lena = 1;

    [Test]
    public void ShortbowOnlyLenaDamagePeriodRange()
    {
        var context = CreateContext(new FakePlacementInfo());
        var stats = Calculate(context, Lena, 0, "t_bow");
        var bow = context.ItemRepository.Get("t_bow");

        Assert.AreEqual(15.6f, context.EquipmentStatService.GetPower(bow, 1f, Character(context, Lena), stats), 1e-4f);
        Assert.AreEqual(0.7f, context.EquipmentStatService.GetPeriod(bow, stats), 1e-6f);
        Assert.AreEqual(336f, context.EquipmentStatService.GetRange(bow, stats), 1e-4f);
    }

    [Test]
    public void RingNotAdjacentAddsBaseHasteOnly()
    {
        var context = CreateContext(new FakePlacementInfo());
        var stats = Calculate(context, Lena, 0, "t_bow", "t_ring");

        Assert.AreEqual(0.7f / 1.04f, context.EquipmentStatService.GetPeriod(context.ItemRepository.Get("t_bow"), stats), 1e-6f);
    }

    [Test]
    public void WhetstoneAdjacentAndNotAdjacent()
    {
        var adjacent = CreateContext(new FakePlacementInfo { IsAllAdjacent = true });
        var notAdjacent = CreateContext(new FakePlacementInfo());
        var bow = adjacent.ItemRepository.Get("t_bow");

        var adjacentStats = Calculate(adjacent, Lena, 0, "t_bow", "t_whetstone");
        var notAdjacentStats = Calculate(notAdjacent, Lena, 0, "t_bow", "t_whetstone");

        Assert.AreEqual(18.72f, adjacent.EquipmentStatService.GetPower(bow, 1f, Character(adjacent, Lena), adjacentStats), 1e-4f);
        Assert.AreEqual(16.536f,
            notAdjacent.EquipmentStatService.GetPower(bow, 1f, Character(notAdjacent, Lena), notAdjacentStats), 1e-4f);
    }

    [Test]
    public void GradeMultipliesPowerButNotCountEffects()
    {
        var placement = new FakePlacementInfo { IsAllAdjacent = true };
        for (var slot = 0; slot < 3; slot++)
        {
            placement.SetGrade(Lena, slot, 1.4f);
        }

        var context = CreateContext(placement);
        var bow = context.ItemRepository.Get("t_bow");
        var stats = Calculate(context, Lena, 0, "t_bow", "t_pierce", "t_split");

        var noBonus = Calculate(CreateContext(placement), Lena, 0, "t_bow");
        Assert.AreEqual(21.84f, context.EquipmentStatService.GetPower(bow, 1.4f, Character(context, Lena), noBonus), 1e-4f);
        Assert.AreEqual(1, stats.Pierce);
        Assert.AreEqual(1, stats.ExtraShots);

        var chainStats = Calculate(context, Lena, 0, "t_wand", "t_hat");
        Assert.AreEqual(2, chainStats.Chain);
    }

    [Test]
    public void DefenseAndHasteAreCapped()
    {
        var context = CreateContext(new FakePlacementInfo(),
            CombatTestItems.Passive("t_def50", "def", 0.5f), CombatTestItems.Passive("t_def45", "def", 0.45f),
            CombatTestItems.Passive("t_haste60", "haste", 0.6f));
        // 브루노 방어 0.15 + 0.95
        var stats = Calculate(context, 0, -1, "t_def50", "t_def45", "t_haste60", "t_haste60");

        Assert.AreEqual(0.15f, Character(context, 0).Def, 1e-6f);
        Assert.AreEqual(0.8f, stats.Defense, 1e-6f);
        Assert.AreEqual(1f, stats.Haste, 1e-6f);
    }

    [Test]
    public void BrunoWithChainmailHas475MaxHp()
    {
        var context = CreateContext(new FakePlacementInfo());
        var stats = Calculate(context, 0, -1, "t_sword", "t_chainmail");

        Assert.AreEqual(475f, stats.MaxHp, 1e-4f);
    }

    [Test]
    public void ConditionalBonusIsNotInWholeMemberStats()
    {
        var context = CreateContext(new FakePlacementInfo { IsAllAdjacent = true });
        var whole = Calculate(context, Lena, -1, "t_bow", "t_whetstone");
        var forBow = Calculate(context, Lena, 0, "t_bow", "t_whetstone");

        Assert.AreEqual(0.06f, whole.Attack, 1e-6f);
        Assert.AreEqual(0.2f, forBow.Attack, 1e-6f);
    }

    private static TestRunContext CreateContext(FakePlacementInfo placement, params ItemData[] extraItems)
    {
        return new TestRunContext(CombatTestItems.CreateRepository(extraItems), placement);
    }

    private static CharacterData Character(TestRunContext context, int memberIndex) =>
        context.CharacterRepository.CharacterDataList[memberIndex];

    private static CombatStats Calculate(TestRunContext context, int memberIndex, int actionSlot, params string[] itemIds)
    {
        return context.EquipmentStatService.Calculate(memberIndex, Character(context, memberIndex), itemIds, actionSlot);
    }
}
