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
        AssertPixelsPath(actual, $"Assets/Game/03.Resources/Art/HD2D/Effects/{name}.png");
    }

    private static void AssertPixelsPath(EffectRaster actual, string name)
    {
        var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        try
        {
            texture.LoadImage(File.ReadAllBytes(name));
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
    [System.Serializable]
    private class CanonicalCase
    {
        public string kind;
        public double radius;
        public double angle;
        public int dx;
        public int dy;
        public uint seed;
        public int frame;
        public string file;
        public string sha256;
    }

    [System.Serializable]
    private class CanonicalManifest
    {
        public string source;
        public string source_sha256;
        public CanonicalCase[] cases;
    }

    private static string HashFile(string path)
    {
        using var sha = System.Security.Cryptography.SHA256.Create();
        return System.BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(path))).Replace("-", "").ToLowerInvariant();
    }

    [Test]
    public void All51PinnedCanonicalFramesMatchPixelForPixel()
    {
        var manifest = JsonUtility.FromJson<CanonicalManifest>(File.ReadAllText("output/hd2d-fx/verification/r002/reference/manifest.json"));
        Assert.AreEqual(manifest.source_sha256, HashFile(manifest.source));
        Assert.AreEqual(51, manifest.cases.Length);
        foreach (var entry in manifest.cases)
        {
            Assert.AreEqual(entry.sha256, HashFile(entry.file));
            var actual = entry.kind == "chain" ? ChainBoltRaster.Draw(entry.dx, entry.dy, entry.seed, entry.frame)
                : entry.kind == "dash" ? GroundMarkRaster.DashPath(entry.radius, entry.angle, entry.frame)
                : GroundMarkRaster.Ring(entry.kind, entry.radius, entry.frame);
            AssertPixelsPath(actual, entry.file);
        }
    }
}
