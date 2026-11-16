using System.Linq;
using NUnit.Framework;

// AC3 — 등급·가방 규칙·전리품 규칙 파일과 아이템 lootWeight 읽기(기획서 데이터 스키마)
public class BagDataLoadTests
{
    [Test]
    public void GradesAreFiveWithMultipliers()
    {
        var repository = new GradeRepository(new ResourceService());

        CollectionAssert.AreEqual(new[] { "common", "uncommon", "rare", "epic", "legendary" },
            repository.GradeDataList.Select(grade => grade.Id));
        CollectionAssert.AreEqual(new[] { "일반", "고급", "희귀", "영웅", "전설" }, repository.GradeDataList.Select(grade => grade.Name));
        CollectionAssert.AreEqual(new[] { 1f, 1.4f, 2f, 3f, 4.5f }, repository.GradeDataList.Select(grade => grade.Mult));
        CollectionAssert.AreEqual(new[] { "#aeb9bd", "#93d57c", "#76b9ef", "#c394f3", "#ffb75b" },
            repository.GradeDataList.Select(grade => grade.Color));
        Assert.AreEqual(4, repository.LastGrade);
    }

    [Test]
    public void BagRulesMatchSchema()
    {
        var rules = new BagRulesRepository(new ResourceService()).Rules;

        Assert.AreEqual(4, rules.BagWidth);
        Assert.AreEqual(4, rules.BagHeight);
        Assert.AreEqual((0, 0), (rules.StartCell.X, rules.StartCell.Y));
        Assert.AreEqual(0.05f, rules.AutoEquip.HpWeight, 1e-6f);
        Assert.AreEqual(0.1f, rules.AutoEquip.ShieldWeight, 1e-6f);
    }

    [Test]
    public void LootRulesMatchSchema()
    {
        var rules = new LootRulesRepository(new ResourceService()).Rules;

        Assert.AreEqual((3, 5), (rules.MinCount, rules.MaxCount));
        Assert.AreEqual(3, rules.EarlyWaveMax);
        Assert.AreEqual(2f, rules.EarlyBonusFactor);
        Assert.AreEqual(4, rules.MergeWaveMin);
        Assert.AreEqual(1.5f, rules.MergeFactor);
        Assert.AreEqual(7919, rules.LootSeedOffset);
    }

    [Test]
    public void MissingLootWeightReadsAsOne()
    {
        // 아이템 파일에는 lootWeight 칸이 없다
        var repository = new ItemRepository(new ResourceService());
        Assert.IsTrue(repository.ItemDataList.All(item => item.LootWeight == 1f));

        var zero = BagTestItems.Create("x_zero", "zero", "passive", BagTestItems.One, baseEffect: "hp", baseValue: 1f, lootWeight: 0f);
        Assert.AreEqual(0f, zero.LootWeight);
        var json = "{\"id\":\"x\",\"name\":\"x\",\"mode\":\"melee\",\"shape\":[{\"x\":0,\"y\":0}],\"power\":1,\"interval\":1,\"range\":1}";
        Assert.AreEqual(1f, UnityEngine.JsonUtility.FromJson<ItemData>(json).LootWeight);
    }
}
