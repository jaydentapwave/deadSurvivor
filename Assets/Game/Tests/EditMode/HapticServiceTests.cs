using NUnit.Framework;
using UnityEngine;

// F6 AC9(진동) — 간격·꺼짐·셈(기획서 D절). 에디터에서는 Handheld.Vibrate를 부르지 않고 셈만 늘린다
public class HapticServiceTests
{
    [Test]
    public void PlaysOncePerMinInterval()
    {
        var rules = new FeedbackRulesRepository(new ResourceService()).Rules.Haptic;
        var service = new HapticService(rules);

        Assert.IsTrue(service.TryPlay(0f));
        Assert.IsFalse(service.TryPlay(rules.MinInterval * 0.5f));
        Assert.IsFalse(service.TryPlay(rules.MinInterval - 0.001f));
        Assert.IsTrue(service.TryPlay(rules.MinInterval));
        Assert.AreEqual(2, service.PlayCount);

        service.ResetRun();
        Assert.AreEqual(0, service.PlayCount);
        Assert.IsTrue(service.TryPlay(0f));
    }

    [Test]
    public void DisabledHapticDoesNotCount()
    {
        var service = new HapticService(JsonUtility.FromJson<HapticRulesData>("{\"enabled\":false,\"minInterval\":0.3}"));

        Assert.IsFalse(service.TryPlay(0f));
        Assert.IsFalse(service.TryPlay(10f));
        Assert.AreEqual(0, service.PlayCount);
    }
}
