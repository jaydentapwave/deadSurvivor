using System.IO;
using NUnit.Framework;
using UnityEngine;

public class GroundMarkRasterTests
{
    [TestCase("heal", 168, 2)]
    [TestCase("blast", 90, 0)]
    [TestCase("pulse", 100, 2)]
    [TestCase("boom", 75, 1)]
    public void RingMatchesReviewedPythonPixels(string kind, int radius, int frame)
    {
        AssertPixels(GroundMarkRaster.Ring(kind, radius * .0113, frame), $"ground_{kind}_{frame + 1}");
    }

    [Test]
    public void DashMatchesReviewedPythonPixels()
    {
        AssertPixels(GroundMarkRaster.DashPath(460 * .7 * .0113, 35, 0), "dash_line_1");
    }

    [Test]
    public void CacheReusesFramesButDoesNotAliasDifferentRadii()
    {
        using var cache = new EffectSpriteCache();
        var first = cache.Ring("heal", 1.9, 2);
        Assert.AreSame(first, cache.Ring("heal", 1.9, 2));
        Assert.AreNotSame(first, cache.Ring("heal", 1.9001, 2));
        Assert.AreNotSame(first, cache.Ring("heal", 1.9, 3));
        Assert.AreEqual(FilterMode.Point, first.texture.filterMode);
        Assert.AreEqual(1, first.texture.mipmapCount);
        Assert.AreEqual(48, first.pixelsPerUnit);
    }

    public static void AssertPixels(EffectRaster actual, string name)
    {
        var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        try
        {
            texture.LoadImage(File.ReadAllBytes($"Assets/Game/03.Resources/Art/HD2D/Effects/{name}.png"));
            Assert.AreEqual(texture.width, actual.Width, name);
            Assert.AreEqual(texture.height, actual.Height, name);
            var pixels = texture.GetPixels32();
            var different = 0;
            for (var i = 0; i < pixels.Length; i++)
            {
                if (!pixels[i].Equals(actual.Pixels[i]))
                {
                    different++;
                }
            }
            Assert.AreEqual(0, different, name + " pixel differences");
        }
        finally
        {
            Object.DestroyImmediate(texture);
        }
    }
}
