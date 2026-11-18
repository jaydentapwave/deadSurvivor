using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

// AC13·AC14 — 전리품 지급과 적응 가중치(기획서 B절)
public class LootServiceTests
{
    private const int Bruno = 0;
    private const int Lena = 1;

    [Test]
    public void GrantsThreeToFiveCommonAfterEachNonFinalWave()
    {
        var context = BagTestItems.CreateContext(new ItemRepository(new ResourceService()));
        context.RunService.StartRun(11, 0f);
        var grantCount = 0;
        while (context.RunModel.Phase.CurrentValue == RunPhase.Battle)
        {
            var before = context.BagModel.StashList.Count;
            context.StepUntilBattleEnds();
            var granted = context.BagModel.StashList.Count - before;
            if (context.RunModel.Phase.CurrentValue == RunPhase.Prep)
            {
                grantCount++;
                Assert.That(granted, Is.InRange(3, 5), $"wave {context.RunModel.CompletedWave.CurrentValue}");
                Assert.IsTrue(context.BagModel.StashList.All(item => item.Grade == 0 && item.Rotation == 0));
                context.RunService.NextWave();
                continue;
            }

            // 마지막 웨이브(승리)에는 지급 없음
            Assert.AreEqual(RunOutcome.Win, context.RunModel.Outcome.CurrentValue);
            Assert.AreEqual(0, granted);
        }

        Assert.AreEqual(context.WaveRepository.WaveDataList.Count - 1, grantCount);
    }

    [Test]
    public void NoLootOnDefeat()
    {
        var context = BagTestItems.CreateContext(new ItemRepository(new ResourceService()));
        context.RunService.StartRun(11, 0f);
        context.PlaceKillingContact(1f);

        context.StepUntilBattleEnds();

        Assert.AreEqual(RunOutcome.Lose, context.RunModel.Outcome.CurrentValue);
        Assert.AreEqual(0, context.BagModel.StashList.Count);
    }

    [Test]
    public void SameSeedSameLoot()
    {
        CollectionAssert.AreEqual(GrantTwice(42), GrantTwice(42));
    }

    [Test]
    public void CandidatesAreOnlyRepositoryItemsWithPositiveWeight()
    {
        var itemList = new List<ItemData>
        {
            BagTestItems.Create("x_a", "a", "passive", BagTestItems.One, baseEffect: "hp", baseValue: 1f),
            BagTestItems.Create("x_b", "b", "passive", BagTestItems.One, baseEffect: "hp", baseValue: 1f),
            BagTestItems.Create("x_c", "c", "passive", BagTestItems.One, baseEffect: "hp", baseValue: 1f),
            BagTestItems.Create("x_zero", "zero", "passive", BagTestItems.One, baseEffect: "hp", baseValue: 1f, lootWeight: 0f),
        };
        var context = new TestRunContext(new ItemRepository(itemList));
        context.BagService.Clear();
        context.LootService.ResetRun(3);

        for (var wave = 1; wave <= 30; wave++)
        {
            context.LootService.GrantWaveLoot(5);
        }

        var grantedIdSet = new HashSet<string>(context.BagModel.StashList.Select(item => item.ItemId));
        CollectionAssert.AreEquivalent(new[] { "x_a", "x_b", "x_c" }, grantedIdSet);
        CollectionAssert.AreEqual(new[] { "x_a", "x_b", "x_c" }, context.LootService.CandidateList.Select(item => item.Id));
    }

    [Test]
    public void AdaptiveWeights()
    {
        var context = BagTestItems.CreateContext();
        BagTestItems.SetupBags(context, new[] { "b_sword" }, new[] { "b_bow" });
        var repository = context.ItemRepository;

        // 끝난 웨이브 3: 단궁(투사체)을 든 산 대원이 있으면 화살통 2, 조건 없는 추가 효과(철제 방패) 2, 추가 효과 없음(장검) 1
        Assert.AreEqual(2f, context.LootService.GetWeight(repository.Get("b_quiver"), 3));
        Assert.AreEqual(2f, context.LootService.GetWeight(repository.Get("b_shield"), 3));
        Assert.AreEqual(1f, context.LootService.GetWeight(repository.Get("b_sword"), 3));

        // 번개 완드만 있으면 화살통 1
        var wandOnly = BagTestItems.CreateContext();
        BagTestItems.SetupBags(wandOnly, new[] { "b_wand" });
        Assert.AreEqual(1f, wandOnly.LootService.GetWeight(repository.Get("b_quiver"), 3));

        // 끝난 웨이브 4: 산 대원의 일반 장검 → 장검 1.5
        Assert.AreEqual(1.5f, context.LootService.GetWeight(repository.Get("b_sword"), 4));

        // 고급 장검만 있으면 1
        BagTestItems.BagItemAt(context, Bruno, 0).Grade = 1;
        Assert.AreEqual(1f, context.LootService.GetWeight(repository.Get("b_sword"), 4));

        // 일반 장검을 가진 대원이 쓰러졌으면 1
        BagTestItems.BagItemAt(context, Bruno, 0).Grade = 0;
        context.PartyModel.MemberList[Bruno].Hp = 0f;
        Assert.AreEqual(1f, context.LootService.GetWeight(repository.Get("b_sword"), 4));

        // 단궁을 든 대원이 쓰러졌으면 웨이브 3 화살통 1
        context.PartyModel.MemberList[Lena].Hp = 0f;
        Assert.AreEqual(1f, context.LootService.GetWeight(repository.Get("b_quiver"), 3));
    }

    [Test]
    public void WeightedDrawDistribution()
    {
        var context = new TestRunContext();
        var random = new System.Random(1234);
        var weightList = new List<float> { 1f, 2f, 1f };
        var middleCount = 0;
        const int drawCount = 10000;
        for (var i = 0; i < drawCount; i++)
        {
            if (context.LootService.PickIndex(weightList, random.NextDouble()) == 1)
            {
                middleCount++;
            }
        }

        Assert.AreEqual(0.5, middleCount / (double)drawCount, 0.02);
    }

    [Test]
    public void PickIndexBoundaries()
    {
        var context = new TestRunContext();
        var weightList = new List<float> { 1f, 2f, 1f };

        Assert.AreEqual(0, context.LootService.PickIndex(weightList, 0d));
        Assert.AreEqual(1, context.LootService.PickIndex(weightList, 0.25d));
        Assert.AreEqual(2, context.LootService.PickIndex(weightList, 0.75d));
        Assert.AreEqual(2, context.LootService.PickIndex(weightList, 0.9999999d));
    }

    private static List<string> GrantTwice(int seed)
    {
        var context = BagTestItems.CreateContext(new ItemRepository(new ResourceService()));
        context.PartyService.CreateParty();
        context.BagService.ResetRun();
        context.LootService.ResetRun(seed);
        context.LootService.GrantWaveLoot(1);
        context.LootService.GrantWaveLoot(2);
        return context.BagModel.StashList.Select(item => item.ItemId).ToList();
    }
}
