using NUnit.Framework;
using UnityEngine;

// AC18 — 안전 영역 판넬 앵커
public class SafeAreaLayoutTests
{
    [Test]
    public void NotchLandscapeLeftInset()
    {
        // 19.5:9 가로, 왼쪽 노치 132px, 아래 홈 표시줄 63px
        var (anchorMin, anchorMax) = SafeAreaLayout.Calculate(new Vector2(2340f, 1080f), new Rect(132f, 63f, 2208f, 1017f));

        Assert.AreEqual(132f / 2340f, anchorMin.x, 1e-5f);
        Assert.AreEqual(63f / 1080f, anchorMin.y, 1e-5f);
        Assert.AreEqual(1f, anchorMax.x, 1e-5f);
        Assert.AreEqual(1f, anchorMax.y, 1e-5f);
    }

    [Test]
    public void NotchLandscapeBothSides()
    {
        var (anchorMin, anchorMax) = SafeAreaLayout.Calculate(new Vector2(2340f, 1080f), new Rect(132f, 0f, 2076f, 1080f));

        Assert.AreEqual(132f / 2340f, anchorMin.x, 1e-5f);
        Assert.AreEqual((2340f - 132f) / 2340f, anchorMax.x, 1e-5f);
        Assert.AreEqual(0f, anchorMin.y, 1e-5f);
        Assert.AreEqual(1f, anchorMax.y, 1e-5f);
    }

    [Test]
    public void NoNotchFillsScreen()
    {
        var (anchorMin, anchorMax) = SafeAreaLayout.Calculate(new Vector2(1920f, 1080f), new Rect(0f, 0f, 1920f, 1080f));

        Assert.AreEqual(Vector2.zero, anchorMin);
        Assert.AreEqual(Vector2.one, anchorMax);
    }
}
