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
    [TestCase(1, 1)]
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
        // Whole 1m segments need not end exactly at the input centres. Keep both
        // outside ends within half a metre of their authored endpoint centres.
        Assert.That(Vector2.Distance(centers[0] - direction * .5f, Vector2.zero), Is.LessThanOrEqualTo(.50001f));
        Assert.That(Vector2.Distance(centers[centers.Length - 1] + direction * .5f, direction * distance), Is.LessThanOrEqualTo(.50001f));
    }

    [TestCase(null, false)]
    [TestCase("", false)]
    [TestCase("wall_", false)]
    [TestCase("wall_0", true)]
    [TestCase("wall_8", true)]
    [TestCase("wall_edge_3", false)]
    [TestCase("wall_-1", false)]
    [TestCase("wall_1_extra", false)]
    public void OnlyNumberedWallRunsUseWallSegments(string group, bool expected)
    {
        Assert.AreEqual(expected, FieldModelMath.IsWallRun(group));
    }

    [Test]
    public void GeometricEndpointsAndSeedAreStableAcrossInputPermutations()
    {
        var a = new FieldLayoutPlacement { x = 0, y = 0, sprite = "rock_01" };
        var middle = new FieldLayoutPlacement { x = 1.8f, y = 2.4f, sprite = "rock_02" };
        var b = new FieldLayoutPlacement { x = 3, y = 4, sprite = "rock_02" };
        foreach (var points in new[] { new[] { a, middle, b }, new[] { middle, a, b }, new[] { b, middle, a } })
        {
            var (first, last) = FieldModelMath.WallEndpoints(points);
            Assert.AreSame(a, first);
            Assert.AreSame(b, last);
            Assert.AreEqual(FieldModelMath.Seed(8, a), FieldModelMath.Seed(8, first));
            var centers = FieldModelMath.WallCenters(first.Floor, last.Floor);
            Assert.AreEqual(5, centers.Length);
            Assert.That(Vector2.Distance(centers[0], new Vector2(.3f, .4f)), Is.LessThan(.00001f));
            Assert.That(Vector2.Distance(centers[4], new Vector2(2.7f, 3.6f)), Is.LessThan(.00001f));
        }
    }

    [Test]
    public void EqualDiameterTiesAndCoincidentEndpointsHaveCanonicalAnchors()
    {
        var a = new FieldLayoutPlacement { x = 0, y = 0, sprite = "rock_01" };
        var b = new FieldLayoutPlacement { x = 1, y = 1, sprite = "rock_01" };
        var c = new FieldLayoutPlacement { x = 0, y = 1, sprite = "rock_01" };
        var d = new FieldLayoutPlacement { x = 1, y = 0, sprite = "rock_01" };
        foreach (var points in new[] { new[] { d, c, b, a }, new[] { a, b, c, d } })
        {
            var (first, last) = FieldModelMath.WallEndpoints(points);
            Assert.AreSame(a, first);
            Assert.AreSame(b, last);
        }
        var coincident = new FieldLayoutPlacement { x = 0, y = 0, sprite = "rock_02" };
        var endpoints = FieldModelMath.WallEndpoints(new[] { coincident, a });
        Assert.AreSame(a, endpoints.First);
        Assert.AreEqual(endpoints.First.Floor, endpoints.Last.Floor);
        Assert.AreEqual(1, FieldModelMath.WallCenters(endpoints.First.Floor, endpoints.Last.Floor).Length);
        Assert.Throws<System.ArgumentException>(() => FieldModelMath.WallEndpoints(new FieldLayoutPlacement[0]));
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
