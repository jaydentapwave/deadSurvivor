using NUnit.Framework;
using UnityEngine;

public class TiltedViewMathTests
{
    [TestCase(10.5f, 9.592308f)]
    [TestCase(10.74f, 9.832308f)]
    [TestCase(12f, 9.842308f)]
    public void DT11CaptureKeepsBoundaryInset(float oldDistance, float expected)
    {
        var target = TiltedViewMath.RemapBoundaryPoint(new Vector2(0, oldDistance), 10.75f, 9.842308f);
        Assert.That(target.y, Is.EqualTo(expected).Within(.00001f));
        Assert.That(target.x, Is.Zero);
    }

    [Test]
    public void DT11DiagonalCaptureKeepsDirectionAndSameL1Inset()
    {
        var oldPoint = new Vector2(-5.1f, 5.65f);
        var target = TiltedViewMath.RemapBoundaryPoint(oldPoint, 10.75f, 9.842308f);
        Assert.That(Mathf.Abs(target.x) + Mathf.Abs(target.y), Is.EqualTo(9.842308f).Within(.00001f));
        Assert.That(target.x / target.y, Is.EqualTo(oldPoint.x / oldPoint.y).Within(.00001f));
        Assert.That(TiltedViewMath.RemapBoundaryPoint(Vector2.zero, 10.75f, 9.842308f), Is.EqualTo(Vector2.zero));
    }

    [TestCase(16f/9f, 10f)]
    [TestCase(21f/9f, 13.125f)]
    [TestCase(4f/3f, 7.5f)]
    public void F2F3Footprint(float aspect, float width)
    {
        var actual = TiltedViewMath.Footprint(5.625f, aspect, .5f);
        Assert.AreEqual(width, actual.x, 1e-4f);
        Assert.AreEqual(11.25f, actual.y, 1e-4f);
    }
    [Test] public void F1OriginalFootprint()
    {
        var actual = TiltedViewMath.Footprint(3.75f, 16f/9f, .5f) / .0113f;
        Assert.That(Vector2.Distance(actual, new Vector2(3.75f*16f/9f/.0113f,7.5f/.0113f)), Is.LessThan(.001f));
    }
    [Test] public void T1T2TexelsStayOnScreenGrid()
    {
        var actual = TiltedViewMath.SnapToTexels(new Vector3(.013f,0,.03f), .5f,48);
        Assert.AreEqual(1f/48, actual.x, 1e-6);
        Assert.AreEqual(1f/24, actual.z, 1e-6);
        Assert.AreEqual(0, actual.y);
        var screen = TiltedViewMath.FloorToScreenPlane(new Vector2(actual.x,actual.z),.5f)*48;
        Assert.AreEqual(Vector2.one,screen);
    }
    [Test] public void P1P2D6ScreenDirections()
    {
        Assert.That(Vector2.Distance(new Vector2(1,2).normalized,TiltedViewMath.ScreenToFloorDirection(Vector2.one,.5f)),Is.LessThan(1e-6));
        Assert.AreEqual(Vector2.zero,TiltedViewMath.ScreenToFloorDirection(Vector2.zero,.5f));
        Assert.AreEqual(new Vector2(0,.5f),TiltedViewMath.FloorToScreenPlane(Vector2.up,.5f));
    }
    [Test] public void UprightHeightUsesCameraUpAxis()
    {
        var actual = TiltedViewMath.UprightOffset(1,30);
        Assert.AreEqual(Mathf.Sqrt(3)/2,actual.y,1e-6);
        Assert.AreEqual(.5f,actual.z,1e-6);
    }
}
