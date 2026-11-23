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
    [Test]
    public void FinishedChainResourcesAreReleasedWhileAnotherChainRemainsLive()
    {
        using var cache = new EffectSpriteCache(new ResourceService());
        var live = cache.Chain(110, 28, 7, 0);
        for (uint seed = 8; seed < 208; seed++)
        {
            var sprites = new UnityEngine.Sprite[3];
            var textures = new UnityEngine.Texture2D[3];
            for (var frame = 0; frame < 3; frame++)
            {
                sprites[frame] = cache.Chain(110, 28, seed, frame);
                textures[frame] = sprites[frame].texture;
            }
            cache.ReleaseChain(seed);
            foreach (var sprite in sprites)
            {
                Assert.IsTrue(sprite == null, "finished Sprite must be destroyed");
            }
            foreach (var texture in textures)
            {
                Assert.IsTrue(texture == null, "finished Texture must be destroyed");
            }
            Assert.AreSame(live, cache.Chain(110, 28, 7, 0));
        }
        cache.ReleaseChain(7);
        Assert.IsTrue(live == null);
    }
}
