using NUnit.Framework;
using UnityEngine;

public class SafeAreaBleedTests
{
    [Test]
    public void SelectedEdgesExtendToScreenInCanvasUnits()
    {
        Assert.AreEqual(new Vector4(20, 10, 30, 0), SafeAreaLayout.Bleed(new Vector2(1920, 1080), new Rect(60, 30, 1770, 990), 3, true, true, true, false));
        Assert.AreEqual(Vector4.zero, SafeAreaLayout.Bleed(new Vector2(1920, 1080), new Rect(0, 0, 1920, 1080), 3, true, true, true, true));
    }
}
