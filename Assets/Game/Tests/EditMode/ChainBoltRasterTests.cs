using NUnit.Framework;

public class ChainBoltRasterTests
{
    [TestCase(0)]
    [TestCase(1)]
    [TestCase(2)]
    public void ChainMatchesReviewedPythonPixels(int frame)
    {
        GroundMarkRasterTests.AssertPixels(ChainBoltRaster.Draw(110, 28, 7, frame), $"chain_bolt_{frame + 1}");
    }

    [TestCase(110, 28)]
    [TestCase(-110, 28)]
    [TestCase(110, -28)]
    [TestCase(-110, -28)]
    [TestCase(0, 80)]
    [TestCase(80, 0)]
    public void BothAnchorsSurviveEveryFrame(int dx, int dy)
    {
        for (var frame = 0; frame < 3; frame++)
        {
            var image = ChainBoltRaster.Draw(dx, dy, 7, frame);
            var ox = dx >= 0 ? 10 : image.Width - 10;
            var oy = dy >= 0 ? 10 : image.Height - 10;
            Assert.AreEqual(255, image.Pixels[oy * image.Width + ox].a);
            Assert.AreEqual(255, image.Pixels[(oy + dy) * image.Width + ox + dx].a);
        }
    }
}
