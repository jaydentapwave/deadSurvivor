using NUnit.Framework;
using UnityEngine;

// 그림 넣기 AC6 — 방향 규칙(기획서 E절): 축마다 따로, 기준 아래인 축은 지난 값
public class SpriteFacingTests
{
    private const float MinMove = 0.25f / Const.PixelsPerUnit;
    private const float AxisRatio = 0.25f;
    private const float Step = 2f / Const.PixelsPerUnit;

    private static SpriteFacing Member(SpriteFacing last, float x, float y) =>
        SpriteFacing.Next(last, new Vector2(x, y) * Step, MinMove, AxisRatio, true);

    [TestCase(1f, -1f, false, false, "front-right")]
    [TestCase(-1f, -1f, false, true, "front-left")]
    [TestCase(-1f, 1f, true, true, "back-left")]
    [TestCase(1f, 1f, true, false, "back-right")]
    public void DiagonalsGiveFourDirections(float x, float y, bool isBack, bool isFlipped, string label)
    {
        foreach (var last in new[] { SpriteFacing.Front, new SpriteFacing(true, true) })
        {
            var facing = Member(last, x, y);

            Assert.AreEqual(isBack, facing.IsBack);
            Assert.AreEqual(isFlipped, facing.IsFlipped);
            Assert.AreEqual(label, facing.Label);
        }
    }

    [Test]
    public void HorizontalKeepsFrontBack()
    {
        var facing = Member(new SpriteFacing(true, true), 1f, 0f);

        Assert.IsTrue(facing.IsBack);
        Assert.IsFalse(facing.IsFlipped);
    }

    [Test]
    public void VerticalKeepsFlip()
    {
        var facing = Member(new SpriteFacing(false, true), 0f, 1f);

        Assert.IsTrue(facing.IsBack);
        Assert.IsTrue(facing.IsFlipped);
    }

    [Test]
    public void StopAndTinyMoveKeepLastFacing()
    {
        var last = new SpriteFacing(true, true);

        Assert.AreEqual("back-left", SpriteFacing.Next(last, Vector2.zero, MinMove, AxisRatio, true).Label);
        Assert.AreEqual("back-left", SpriteFacing.Next(last, new Vector2(MinMove * 0.9f, -MinMove * 0.9f), MinMove, AxisRatio, true).Label);
    }

    [Test]
    public void VerticalBelowAxisRatioDoesNotChangeFrontBack()
    {
        var facing = Member(SpriteFacing.Front, 1f, 0.2f);

        Assert.IsFalse(facing.IsBack);
        Assert.IsFalse(facing.IsFlipped);
        Assert.IsTrue(Member(new SpriteFacing(true, false), 1f, -0.2f).IsBack);
    }

    [Test]
    public void MonsterModeIsAlwaysFrontAndOnlyFlips()
    {
        var up = SpriteFacing.Next(SpriteFacing.Front, new Vector2(-1f, 1f) * Step, MinMove, AxisRatio, false);
        var right = SpriteFacing.Next(up, new Vector2(1f, 1f) * Step, MinMove, AxisRatio, false);

        Assert.IsFalse(up.IsBack);
        Assert.IsTrue(up.IsFlipped);
        Assert.IsFalse(right.IsBack);
        Assert.IsFalse(right.IsFlipped);
    }

    [Test]
    public void NewRunStartsFrontRight()
    {
        Assert.AreEqual("front-right", SpriteFacing.Front.Label);
        Assert.IsFalse(SpriteFacing.Front.IsBack);
        Assert.IsFalse(SpriteFacing.Front.IsFlipped);
    }
}
