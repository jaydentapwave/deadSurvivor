using NUnit.Framework;
using UnityEngine;

// AC15 — 수치 변화 미리보기(기획서 C절)
public class BagPreviewTests
{
    private const int Lena = 1;
    private const int Mira = 2;

    private TestRunContext _context;
    private BagPreviewService _preview;

    [SetUp]
    public void SetUp()
    {
        _context = BagTestItems.CreateContext();
        BagTestItems.SetupBags(_context, new[] { "b_sword" }, new[] { "b_bow" }, new[] { "b_wand" });
        _preview = BagTestItems.CreatePreview(_context);
    }

    [Test]
    public void WhetstoneNextToShortbowImprovesDamageWithoutChangingState()
    {
        var uid = BagTestItems.AddToStash(_context, "b_whet");
        var version = _context.BagModel.Version.CurrentValue;

        var result = _preview.Preview(uid, 0, BagTarget.Cell(Lena, 0, 1));

        Assert.IsTrue(result.IsSuccess);
        Assert.AreEqual(Lena, result.Member);
        CollectionAssert.AreEqual(new[] { new Vector2Int(0, 1) }, result.CellList);
        CollectionAssert.Contains(result.LineList, "[개선] 레나 단궁 피해 15.6 → 18.72");
        CollectionAssert.Contains(result.LineList, "[개선] 레나 공격 증가 0% → 6%");
        Assert.AreEqual(version, _context.BagModel.Version.CurrentValue);
        Assert.AreEqual(1, _context.BagModel.StashList.Count);
        Assert.AreEqual(1, _context.BagModel.GetBag(Lena).Count);
        CollectionAssert.AreEqual(new[] { "b_bow" }, _context.PartyModel.MemberList[Lena].EquippedItemIdList);
    }

    [Test]
    public void OverlapPreviewFails()
    {
        var uid = BagTestItems.AddToStash(_context, "b_whet");

        var result = _preview.Preview(uid, 0, BagTarget.Cell(Lena, 1, 0));

        Assert.IsFalse(result.IsSuccess);
        Assert.AreEqual(BagFailReason.Overlap, result.Reason);
        CollectionAssert.AreEqual(new[] { new Vector2Int(1, 0) }, result.CellList);
        Assert.AreEqual(0, result.LineList.Count);
    }

    [Test]
    public void DownMemberBagPreviewIsLocked()
    {
        var uid = BagTestItems.AddToStash(_context, "b_whet");
        _context.PartyModel.MemberList[Mira].Hp = 0f;

        var result = _preview.Preview(uid, 0, BagTarget.Cell(Mira, 0, 3));

        Assert.IsFalse(result.IsSuccess);
        Assert.AreEqual(BagFailReason.Locked, result.Reason);
    }

    [Test]
    public void ShorterPeriodIsImprovement()
    {
        var uid = BagTestItems.AddToStash(_context, "b_ring");

        var result = _preview.Preview(uid, 0, BagTarget.Cell(Lena, 3, 0));

        Assert.IsTrue(result.IsSuccess);
        CollectionAssert.Contains(result.LineList, "[개선] 레나 단궁 주기 0.7 → 0.61");
    }

    [Test]
    public void UnequipPreviewShowsDecrease()
    {
        var whetUid = BagTestItems.AddToStash(_context, "b_whet");
        Assert.IsTrue(_context.BagService.Drop(whetUid, Lena, 0, 1, 0, out _));

        var result = _preview.Preview(whetUid, 0, BagTarget.StashEmpty());

        Assert.IsTrue(result.IsSuccess);
        CollectionAssert.Contains(result.LineList, "[감소] 레나 단궁 피해 18.72 → 15.6");
    }
}
