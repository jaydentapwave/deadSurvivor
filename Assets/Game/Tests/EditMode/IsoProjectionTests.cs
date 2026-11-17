using NUnit.Framework;
using UnityEngine;

// AC18 — 바닥 평면 ↔ 월드
public class IsoProjectionTests
{
    private const float UnitScale = 0.0113f;

    [TestCase(0f, 0f)]
    [TestCase(100f, 0f)]
    [TestCase(0f, 100f)]
    [TestCase(-958.3f, 412.7f)]
    [TestCase(24f, -24f)]
    public void RoundTripKeepsValue(float x, float y)
    {
        var floor = new Vector2(x, y);
        var back = IsoProjection.WorldToFloor(IsoProjection.FloorToWorld(floor, UnitScale), UnitScale);

        Assert.AreEqual(floor.x, back.x, 1e-4f);
        Assert.AreEqual(floor.y, back.y, 1e-4f);
    }

    [Test]
    public void HorizontalToVerticalScreenLengthIsTwoToOne()
    {
        var horizontal = TiltedViewMath.FloorToScreenPlane(new Vector2(100f, 0f) * UnitScale, .5f).magnitude;
        var vertical = TiltedViewMath.FloorToScreenPlane(new Vector2(0f, 100f) * UnitScale, .5f).magnitude;

        Assert.AreEqual(2f, horizontal / vertical, 1e-4f);
    }
}
