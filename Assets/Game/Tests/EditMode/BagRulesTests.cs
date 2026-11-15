using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

// AC7 — 가방 규칙(기획서 A4)
public class BagRulesTests
{
    private ItemRepository _repository;

    [SetUp]
    public void SetUp() => _repository = new ItemRepository(BagTestItems.CreateCatalog());

    [Test]
    public void RotateRowOnceBecomesColumnAndFourTimesIsSame()
    {
        var shape = _repository.Get("b_bow").Shape;
        var cellList = new List<Vector2Int>();

        BagRules.GetCells(shape, 1, cellList);
        CollectionAssert.AreEqual(new[] { new Vector2Int(0, 0), new Vector2Int(0, 1), new Vector2Int(0, 2) }, cellList);

        BagRules.GetCells(shape, 4, cellList);
        CollectionAssert.AreEqual(new[] { new Vector2Int(0, 0), new Vector2Int(1, 0), new Vector2Int(2, 0) }, cellList);
    }

    [Test]
    public void RotationNormalizesToTopLeft()
    {
        // 칸 (0,0)(1,0)(0,1)(1,1)은 몇 번 돌려도 같은 칸 집합
        var cellList = new List<Vector2Int>();
        BagRules.GetCells(_repository.Get("b_shield").Shape, 3, cellList);
        CollectionAssert.AreEquivalent(new[] { new Vector2Int(0, 0), new Vector2Int(1, 0), new Vector2Int(0, 1), new Vector2Int(1, 1) },
            cellList);
    }

    [Test]
    public void RowFitsAtOneButNotAtTwo()
    {
        var bag = new List<BagItem>();
        var shape = _repository.Get("b_bow").Shape;

        Assert.IsFalse(BagRules.Fits(bag, 1, shape, 0, 2, 0, _repository, 4, 4, out var outReason));
        Assert.AreEqual(BagFailReason.OutOfBag, outReason);
        Assert.IsTrue(BagRules.Fits(bag, 1, shape, 0, 1, 0, _repository, 4, 4, out var reason));
        Assert.AreEqual(BagFailReason.None, reason);
    }

    [Test]
    public void OverlapRejectedButOwnCellsIgnored()
    {
        var bow = new BagItem(1, "b_bow", 0, 0, 0, 1);
        var bag = new List<BagItem> { bow };
        var shape = _repository.Get("b_bow").Shape;

        // 다른 아이템: (2, 0)에 세로로 놓으면 (2, 1)이 겹침
        Assert.IsFalse(BagRules.Fits(bag, 2, shape, 1, 2, 0, _repository, 4, 4, out var reason));
        Assert.AreEqual(BagFailReason.Overlap, reason);

        // 같은 아이템을 한 칸 오른쪽으로: 자기 칸 (1,1)(2,1)과 겹치지만 겹침이 아니다
        Assert.IsTrue(BagRules.Fits(bag, 1, shape, 0, 1, 1, _repository, 4, 4, out _));
    }

    [Test]
    public void SideTouchIsAdjacentDiagonalIsNot()
    {
        var bow = new BagItem(1, "b_bow", 0, 0, 0, 0);
        var below = new BagItem(2, "b_whet", 0, 0, 0, 1);
        var diagonal = new BagItem(3, "b_whet", 0, 0, 3, 1);
        var gap = new BagItem(4, "b_whet", 0, 0, 0, 2);

        Assert.IsTrue(BagRules.IsAdjacent(bow, below, _repository));
        Assert.IsFalse(BagRules.IsAdjacent(bow, diagonal, _repository));
        Assert.IsFalse(BagRules.IsAdjacent(bow, gap, _repository));
        Assert.IsFalse(BagRules.IsAdjacent(bow, bow, _repository));
    }

    [Test]
    public void CanMergeNeedsSameIdSameGradeBelowLast()
    {
        var a = new BagItem(1, "b_bow", 0, 0, 0, 0);
        Assert.IsTrue(BagRules.CanMerge(a, new BagItem(2, "b_bow", 0, 0, 0, 0), 4));
        Assert.IsFalse(BagRules.CanMerge(a, new BagItem(3, "b_sword", 0, 0, 0, 0), 4));
        Assert.IsFalse(BagRules.CanMerge(a, new BagItem(4, "b_bow", 1, 0, 0, 0), 4));
        Assert.IsFalse(BagRules.CanMerge(a, a, 4));
        Assert.IsFalse(BagRules.CanMerge(new BagItem(5, "b_bow", 4, 0, 0, 0), new BagItem(6, "b_bow", 4, 0, 0, 0), 4));
    }
}
