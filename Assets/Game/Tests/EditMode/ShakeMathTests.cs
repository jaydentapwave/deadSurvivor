using NUnit.Framework;
using UnityEngine;

// F6 AC9(흔들림) — 간격, 정수 픽셀 오프셋, 끝나면 기준 위치(기획서 D절). 값은 실제 feedback_rules.json
public class ShakeMathTests
{
    private const float Frame = 1f / 60f;

    private static ShakeRulesData Rules => new FeedbackRulesRepository(new ResourceService()).Rules.Shake;

    [Test]
    public void HitsWithinMinIntervalStartOnlyOnce()
    {
        var rules = Rules;
        var lastStart = float.NegativeInfinity;
        var starts = 0;
        foreach (var hitTime in new[] { 0f, 0.05f, 0.1f, rules.MinInterval - 0.01f })
        {
            if (ShakeMath.CanStart(hitTime, lastStart, rules.MinInterval))
            {
                lastStart = hitTime;
                starts++;
            }
        }

        Assert.AreEqual(1, starts);
        Assert.IsTrue(ShakeMath.CanStart(rules.MinInterval, 0f, rules.MinInterval));
    }

    [Test]
    public void OffsetIsWholePixelWithinAmplitudeAndZeroAfterSeconds()
    {
        var rules = Rules;
        var step = 0;
        for (var remaining = rules.Seconds; remaining > 0f; remaining -= Frame)
        {
            var pixels = ShakeMath.GetOffset(remaining, rules.Seconds, rules.AmplitudePx, step++) * Const.PixelsPerUnit;
            Assert.AreEqual(Mathf.Round(pixels.x), pixels.x, 1e-4f);
            Assert.AreEqual(Mathf.Round(pixels.y), pixels.y, 1e-4f);
            Assert.LessOrEqual(Mathf.Abs(pixels.x), rules.AmplitudePx);
            Assert.LessOrEqual(Mathf.Abs(pixels.y), rules.AmplitudePx);
        }

        var start = ShakeMath.GetOffset(rules.Seconds, rules.Seconds, rules.AmplitudePx, 0) * Const.PixelsPerUnit;
        Assert.AreEqual(rules.AmplitudePx, Mathf.Abs(start.x), 1e-4f);
        Assert.AreEqual(Vector2.zero, ShakeMath.GetOffset(0f, rules.Seconds, rules.AmplitudePx, 3));
        Assert.AreNotEqual(ShakeMath.GetOffset(rules.Seconds, rules.Seconds, 2, 0), ShakeMath.GetOffset(rules.Seconds, rules.Seconds, 2, 1));
    }

}
