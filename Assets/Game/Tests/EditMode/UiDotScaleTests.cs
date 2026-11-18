using NUnit.Framework;

public class UiDotScaleTests
{
    [TestCase(1f, 3f, 1f)]
    [TestCase(1f, 3.25f, 3f / 3.25f)]
    [TestCase(4f / 3, 3f, 4f / 3)]
    [TestCase(0.1f, 3f, 1f / 3)]
    public void ScreenPixelsAreIntegral(float desired, float scale, float expected)
    {
        Assert.AreEqual(expected, UiDotScale.CanvasPerTexel(desired, scale), 0.00001f);
    }
}
