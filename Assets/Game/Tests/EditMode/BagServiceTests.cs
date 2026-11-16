using System.Linq;
using NUnit.Framework;

// AC8·AC9·AC10·AC12 — 가방 조작·합성·잠금(기획서 A5), 판의 시작과 끝(A6)
public class BagServiceTests
{
    private const int Bruno = 0;
    private const int Lena = 1;

    private TestRunContext _context;

    [SetUp]
    public void SetUp()
    {
        _context = BagTestItems.CreateContext();
        BagTestItems.SetupBags(_context, new[] { "b_sword" }, new[] { "b_bow" });
    }

    [TearDown]
    public void TearDown() => _context.RunModel.Dispose();

    [Test]
    public void DropFromStashAppendsToBagAndSyncsEquipped()
    {
        var uid = BagTestItems.AddToStash(_context, "b_whet");

        Assert.IsTrue(_context.BagService.Drop(uid, Lena, 0, 1, 0, out var reason), reason.ToString());

        Assert.AreEqual(0, _context.BagModel.StashList.Count);
        Assert.AreEqual(uid, BagTestItems.BagItemAt(_context, Lena, 1).Uid);
        AssertEquippedMatchesBag(Lena);
    }

    [Test]
    public void MoveToOtherMemberSyncsBothMembers()
    {
        var whetUid = BagTestItems.AddToStash(_context, "b_whet");
        _context.BagService.Drop(whetUid, Lena, 0, 1, 0, out _);

        Assert.IsTrue(_context.BagService.Drop(whetUid, Bruno, 3, 3, 0, out _));

        CollectionAssert.AreEqual(new[] { "b_bow" }, _context.PartyModel.MemberList[Lena].EquippedItemIdList);
        CollectionAssert.AreEqual(new[] { "b_sword", "b_whet" }, _context.PartyModel.MemberList[Bruno].EquippedItemIdList);
        AssertEquippedMatchesBag(Lena);
        AssertEquippedMatchesBag(Bruno);
    }

    [Test]
    public void MoveInsideSameBagGoesToEnd()
    {
        var whetUid = BagTestItems.AddToStash(_context, "b_whet");
        _context.BagService.Drop(whetUid, Lena, 0, 1, 0, out _);
        var bowUid = BagTestItems.BagItemAt(_context, Lena, 0).Uid;

        Assert.IsTrue(_context.BagService.Drop(bowUid, Lena, 1, 1, 1, out _));

        CollectionAssert.AreEqual(new[] { "b_whet", "b_bow" }, _context.PartyModel.MemberList[Lena].EquippedItemIdList);
        Assert.AreEqual(1, BagTestItems.BagItemAt(_context, Lena, 1).Rotation);
    }

    [Test]
    public void UnequipGoesToStashEnd()
    {
        BagTestItems.AddToStash(_context, "b_whet");
        var bowUid = BagTestItems.BagItemAt(_context, Lena, 0).Uid;

        Assert.IsTrue(_context.BagService.Unequip(bowUid, out _));

        Assert.AreEqual(bowUid, _context.BagModel.StashList.Last().Uid);
        Assert.AreEqual(0, _context.BagModel.GetBag(Lena).Count);
        Assert.AreEqual(0, _context.PartyModel.MemberList[Lena].EquippedItemIdList.Count);
    }

    [Test]
    public void OutOfBagOverlapAndNotPrepAreRejectedWithoutChange()
    {
        var uid = BagTestItems.AddToStash(_context, "b_wand");
        var version = _context.BagModel.Version.CurrentValue;

        Assert.IsFalse(_context.BagService.Drop(uid, Lena, 2, 1, 0, out var outReason));
        Assert.AreEqual(BagFailReason.OutOfBag, outReason);
        Assert.IsFalse(_context.BagService.Drop(uid, Lena, 1, 0, 1, out var overlapReason));
        Assert.AreEqual(BagFailReason.Overlap, overlapReason);
        _context.RunModel.SetPhase(RunPhase.Battle);
        Assert.IsFalse(_context.BagService.Drop(uid, Lena, 0, 1, 0, out var phaseReason));
        Assert.AreEqual(BagFailReason.NotPrep, phaseReason);

        Assert.AreEqual(version, _context.BagModel.Version.CurrentValue);
        Assert.AreEqual(1, _context.BagModel.StashList.Count);
        Assert.AreEqual(1, _context.BagModel.GetBag(Lena).Count);
        CollectionAssert.AreEqual(new[] { "b_bow" }, _context.PartyModel.MemberList[Lena].EquippedItemIdList);
    }

    [Test]
    public void MergeOntoBagItemKeepsPlaceAndOrder()
    {
        var whetUid = BagTestItems.AddToStash(_context, "b_whet");
        _context.BagService.Drop(whetUid, Lena, 0, 1, 0, out _);
        var bowUid = BagTestItems.BagItemAt(_context, Lena, 0).Uid;
        var sourceUid = BagTestItems.AddToStash(_context, "b_bow");

        Assert.IsTrue(_context.BagService.Merge(sourceUid, bowUid, out _));

        var bow = BagTestItems.BagItemAt(_context, Lena, 0);
        Assert.AreEqual(bowUid, bow.Uid);
        Assert.AreEqual(1, bow.Grade);
        Assert.AreEqual((0, 0, 0), (bow.X, bow.Y, bow.Rotation));
        Assert.IsNull(_context.BagModel.FindItem(sourceUid, out _));
        CollectionAssert.AreEqual(new[] { "b_bow", "b_whet" }, _context.PartyModel.MemberList[Lena].EquippedItemIdList);
    }

    [Test]
    public void MergeOntoStashItemKeepsStashPlace()
    {
        var first = BagTestItems.AddToStash(_context, "b_whet");
        var target = BagTestItems.AddToStash(_context, "b_whet");
        var last = BagTestItems.AddToStash(_context, "b_ring");

        Assert.IsTrue(_context.BagService.Merge(first, target, out _));

        CollectionAssert.AreEqual(new[] { target, last }, _context.BagModel.StashList.Select(item => item.Uid));
        Assert.AreEqual(1, _context.BagModel.StashList[0].Grade);
    }

    [Test]
    public void MergeRejectsDifferentIdGradeLegendaryAndSelf()
    {
        var bowUid = BagTestItems.BagItemAt(_context, Lena, 0).Uid;
        var sword = BagTestItems.AddToStash(_context, "b_sword");
        var gradedBow = BagTestItems.AddToStash(_context, "b_bow");
        _context.BagModel.FindItem(gradedBow, out _).Grade = 1;
        var legendA = BagTestItems.AddToStash(_context, "b_whet");
        var legendB = BagTestItems.AddToStash(_context, "b_whet");
        _context.BagModel.FindItem(legendA, out _).Grade = _context.GradeRepository.LastGrade;
        _context.BagModel.FindItem(legendB, out _).Grade = _context.GradeRepository.LastGrade;

        Assert.IsFalse(_context.BagService.Merge(sword, bowUid, out var idReason));
        Assert.AreEqual(BagFailReason.CannotMerge, idReason);
        Assert.IsFalse(_context.BagService.Merge(gradedBow, bowUid, out _));
        Assert.IsFalse(_context.BagService.Merge(legendA, legendB, out _));
        Assert.IsFalse(_context.BagService.Merge(bowUid, bowUid, out _));
        Assert.AreEqual(0, BagTestItems.BagItemAt(_context, Lena, 0).Grade);
    }

    [Test]
    public void DropOnMergeableCellMergesInsteadOfPlacing()
    {
        var bowUid = BagTestItems.BagItemAt(_context, Lena, 0).Uid;
        var sourceUid = BagTestItems.AddToStash(_context, "b_bow");

        // (2, 0)은 단궁의 셋째 칸
        Assert.IsTrue(_context.BagService.Drop(sourceUid, Lena, 2, 0, 1, out _));

        Assert.AreEqual(1, _context.BagModel.GetBag(Lena).Count);
        Assert.AreEqual(1, _context.BagModel.FindItem(bowUid, out _).Grade);
        Assert.AreEqual(0, _context.BagModel.StashList.Count);
    }

    [Test]
    public void DownMemberEquipmentIsLockedUntilRevive()
    {
        var lena = _context.PartyModel.MemberList[Lena];
        var bowUid = BagTestItems.BagItemAt(_context, Lena, 0).Uid;
        var whetUid = BagTestItems.AddToStash(_context, "b_whet");
        var bowCopy = BagTestItems.AddToStash(_context, "b_bow");
        lena.Hp = 0f;

        Assert.IsFalse(_context.BagService.Drop(whetUid, Lena, 0, 1, 0, out var intoReason));
        Assert.AreEqual(BagFailReason.Locked, intoReason);
        Assert.IsFalse(_context.BagService.Drop(bowUid, Bruno, 0, 1, 0, out var outReason));
        Assert.AreEqual(BagFailReason.Locked, outReason);
        Assert.IsFalse(_context.BagService.Unequip(bowUid, out var unequipReason));
        Assert.AreEqual(BagFailReason.Locked, unequipReason);
        Assert.IsFalse(_context.BagService.Merge(bowUid, bowCopy, out var sourceReason));
        Assert.AreEqual(BagFailReason.Locked, sourceReason);
        Assert.IsFalse(_context.BagService.Merge(bowCopy, bowUid, out var targetReason));
        Assert.AreEqual(BagFailReason.Locked, targetReason);
        Assert.AreEqual(2, _context.BagModel.StashList.Count);

        lena.Hp = lena.MaxHp;
        Assert.IsTrue(_context.BagService.Drop(whetUid, Lena, 0, 1, 0, out _));
        Assert.IsTrue(_context.BagService.Merge(bowCopy, bowUid, out _));
    }

    [Test]
    public void ResetRun()
    {
        var context = BagTestItems.CreateContext();
        context.RunService.StartRun(7, 0f);

        Assert.AreEqual(0, context.BagModel.StashList.Count);
        var memberList = context.PartyModel.MemberList;
        for (var member = 0; member < memberList.Count; member++)
        {
            var bag = context.BagModel.GetBag(member);
            Assert.AreEqual(1, bag.Count);
            Assert.AreEqual(context.CharacterRepository.Get(memberList[member].CharacterId).StartItem, bag[0].ItemId);
            Assert.AreEqual((0, 0, 0, 0), (bag[0].X, bag[0].Y, bag[0].Rotation, bag[0].Grade));
            CollectionAssert.AreEqual(new[] { bag[0].ItemId }, memberList[member].EquippedItemIdList);
        }
    }

    [Test]
    public void ClearOnLobby()
    {
        var context = BagTestItems.CreateContext();
        context.RunService.StartRun(7, 0f);
        context.RunModel.SetPhase(RunPhase.Prep);
        BagTestItems.AddToStash(context, "b_whet");
        var swordUid = BagTestItems.BagItemAt(context, Bruno, 0).Uid;
        Assert.IsTrue(context.BagService.Unequip(swordUid, out _));

        context.RunModel.SetPhase(RunPhase.Result);
        context.RunService.ReturnToLobby();
        Assert.AreEqual(0, context.BagModel.MemberCount);
        Assert.AreEqual(0, context.BagModel.StashList.Count);

        context.RunService.StartRun(7, 0f);
        Assert.AreEqual(0, context.BagModel.StashList.Count);
        Assert.AreEqual(1, context.BagModel.GetBag(Bruno).Count);
        // Uid도 1부터 다시
        Assert.AreEqual(1, context.BagModel.GetBag(Bruno)[0].Uid);
        Assert.AreEqual(0, context.BagModel.GetBag(Bruno)[0].Grade);
    }

    private void AssertEquippedMatchesBag(int member)
    {
        CollectionAssert.AreEqual(_context.BagModel.GetBag(member).Select(item => item.ItemId),
            _context.PartyModel.MemberList[member].EquippedItemIdList);
    }
}
