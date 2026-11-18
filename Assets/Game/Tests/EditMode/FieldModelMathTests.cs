using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

public class FieldModelMathTests
{
    [Test]
    public void FixedSeedIsIndependentOfPlacementOrderAndUsesAllVariants()
    {
        var ids = new HashSet<string>();
        var yaws = new HashSet<float>();
        for (var i = 0; i < 40; i++)
        {
            var p = new FieldLayoutPlacement { x = i * .73f, y = -i * .37f, sprite = "tree_02" };
            var seed = FieldModelMath.Seed(8, p);
            Assert.AreEqual(seed, FieldModelMath.Seed(8, p));
            Assert.AreNotEqual(seed, FieldModelMath.Seed(9, p));
            ids.Add(FieldModelMath.ModelId(p.sprite, seed));
            yaws.Add(FieldModelMath.Yaw(seed));
            Assert.That(FieldModelMath.StoneScale(seed), Is.InRange(.85f, 1.15f));
        }
        Assert.AreEqual(3, ids.Count);
        Assert.Greater(yaws.Count, 30);
    }

    [TestCase(0, 1)]
    [TestCase(.8f, 1)]
    [TestCase(2.7f, 3)]
    [TestCase(5.2f, 6)]
    public void OneMetreWallsMeetAtBothHalfMetreEnds(float distance, int count)
    {
        var direction = new Vector2(.6f, .8f);
        var centers = FieldModelMath.WallCenters(Vector2.zero, direction * distance);
        Assert.AreEqual(count, centers.Length);
        for (var i = 1; i < centers.Length; i++)
        {
            Assert.That(Vector2.Distance(centers[i - 1] + direction * .5f, centers[i] - direction * .5f), Is.LessThan(.00001f));
        }
    }

    [Test]
    public void ProjectionIncludesDepthAndHeightAtActualTilt()
    {
        var vertices = new[] { Vector3.zero, new Vector3(2, 1, 0), new Vector3(0, 0, 1) };
        var size = FieldModelMath.ProjectedSize(vertices, 0, 30);
        Assert.That(size.x, Is.EqualTo(2).Within(.00001));
        Assert.That(size.y, Is.EqualTo(Mathf.Cos(30 * Mathf.Deg2Rad)).Within(.00001));
        size = FieldModelMath.ProjectedSize(vertices, 90, 30);
        Assert.That(size.x, Is.EqualTo(1).Within(.00001));
        Assert.Greater(size.y, 0);
    }
}
