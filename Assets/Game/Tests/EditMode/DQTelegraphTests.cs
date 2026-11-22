using System;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

public sealed class DQTelegraphTests
{
    [Serializable] private sealed class ReferenceSet { public ReferenceSource[] sources; public ReferenceSample[] samples; }
    [Serializable] private sealed class ReferenceSource { public string path; public string sha256; }
    [Serializable] private sealed class ReferenceSample
    {
        public double length, radius, angle;
        public int stage, width, height;
        public string sha256;
    }

    [Test]
    public void RasterMatchesCanonicalPythonReferenceWithZeroPixelTolerance()
    {
        var reference = JsonUtility.FromJson<ReferenceSet>(System.IO.File.ReadAllText(
            "Assets/Game/Tests/EditMode/DQTelegraphReference.json"));
        using var sha = System.Security.Cryptography.SHA256.Create();
        string Hash(byte[] bytes) => BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant();
        foreach (var source in reference.sources)
        {
            Assert.That(Hash(System.IO.File.ReadAllBytes(source.path)), Is.EqualTo(source.sha256), source.path);
        }
        Assert.That(reference.samples.Length, Is.EqualTo(280));
        var tiles = Resources.Load<DQTelegraphTiles>(DQTelegraphTiles.ResourcePath);
        foreach (var sample in reference.samples)
        {
            var raster = tiles.Raster(sample.length, sample.radius, sample.angle, sample.stage);
            Assert.That(raster.Width, Is.EqualTo(sample.width));
            Assert.That(raster.Height, Is.EqualTo(sample.height));
            var bytes = raster.Pixels.SelectMany(c => new[] { c.r, c.g, c.b, c.a }).ToArray();
            Assert.That(Hash(bytes), Is.EqualTo(sample.sha256), $"{sample.length}/{sample.radius}/{sample.angle}/{sample.stage}");
        }
    }

    [Test]
    public void EveryAdoptedActorDirectionHasEventAlignedCueAtNativePivot()
    {
        var library = Resources.Load<DQAttackFxLibrary>(DQAttackFxLibrary.ResourcePath);
        Assert.That(library, Is.Not.Null);
        Assert.That(library.entries.Length, Is.EqualTo(20));
        Assert.That(library.entries.Select(e => (e.actor, e.direction)).Distinct().Count(), Is.EqualTo(20));
        foreach (var entry in library.entries)
        {
            var sprite = library.GetFrame(entry.actor, entry.direction, entry.eventFrame);
            Assert.That(sprite, Is.Not.Null, entry.actor + ":" + entry.direction);
            Assert.That(sprite.pixelsPerUnit, Is.EqualTo(48));
            Assert.That(sprite.pivot, Is.EqualTo(new Vector2(36, 8)));
            Assert.That(sprite.rect.size, Is.EqualTo(new Vector2(72, 72)));
            Assert.That(sprite.texture.filterMode, Is.EqualTo(FilterMode.Point));
            Assert.That(library.GetFrame(entry.actor, entry.direction, entry.eventFrame - 1), Is.Null);
        }
    }
    [TestCase(0f, 0)]
    [TestCase(.259f, 0)]
    [TestCase(.26f, 1)]
    [TestCase(.499f, 1)]
    [TestCase(.5f, 2)]
    [TestCase(.699f, 2)]
    [TestCase(.7f, 3)]
    [TestCase(.799f, 3)]
    public void AdoptedTimingDoesNotLoop(float seconds, int expected)
    {
        Assert.That(DQTelegraphTiles.StageAt(seconds, .8f), Is.EqualTo(expected));
        Assert.That(DQTelegraphTiles.StageAt(seconds * 2, 1.6f), Is.EqualTo(expected));
    }

    [Test]
    public void InvalidInputsFailAtBoundary()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => DQTelegraphTiles.StageAt(0, 0));
        var tiles = Resources.Load<DQTelegraphTiles>(DQTelegraphTiles.ResourcePath);
        Assert.That(tiles, Is.Not.Null, "Run the approved finish migration first.");
        Assert.Throws<ArgumentException>(() => tiles.Raster(0, .3, 0, 0));
        Assert.Throws<ArgumentException>(() => tiles.Raster(3, double.NaN, 0, 0));
    }

    [Test]
    public void AllDirectionsStayInsideActualChargeCapsuleAndReachBothCaps()
    {
        var tiles = Resources.Load<DQTelegraphTiles>(DQTelegraphTiles.ResourcePath);
        Assert.That(tiles, Is.Not.Null);
        foreach (var dimensions in new[] { new Vector2(322 * .0113f, 28 * .0113f), new Vector2(2.25f, .4f) })
        {
            foreach (var angle in Enumerable.Range(0, 32).Select(d => d * 11.25f).Concat(new[] { 5.625f, 39.7f, 181.3f }))
            {
                var image = tiles.Raster(dimensions.x, dimensions.y, angle, 3);
                var axis = new Vector2(Mathf.Cos(angle * Mathf.Deg2Rad), Mathf.Sin(angle * Mathf.Deg2Rad));
                var count = 0;
                var maxAlong = float.NegativeInfinity;
                var minAlong = float.PositiveInfinity;
                var maxAcross = float.NegativeInfinity;
                var minAcross = float.PositiveInfinity;
                for (var y = 0; y < image.Height; y++)
                {
                    for (var x = 0; x < image.Width; x++)
                    {
                        var color = image.Pixels[y * image.Width + x];
                        Assert.That(color.a == 0 || color.a == 255, Is.True);
                        if (color.a == 0)
                        {
                            continue;
                        }
                        var floor = new Vector2((x + .5f - image.Pivot.x * image.Width) / 48,
                            (y + .5f - image.Pivot.y * image.Height) * 2 / 48);
                        var along = Vector2.Dot(floor, axis);
                        var across = axis.x * floor.y - axis.y * floor.x;
                        var closest = axis * Mathf.Clamp(along, 0, dimensions.x);
                        Assert.That(Vector2.Distance(floor, closest), Is.LessThanOrEqualTo(dimensions.y + .00001f), "Artwork extends beyond damage capsule.");
                        maxAlong = Mathf.Max(maxAlong, along);
                        minAlong = Mathf.Min(minAlong, along);
                        maxAcross = Mathf.Max(maxAcross, across);
                        minAcross = Mathf.Min(minAcross, across);
                        count++;
                    }
                }
                Assert.That(count, Is.GreaterThan(0));
                Assert.That(maxAlong, Is.GreaterThan(dimensions.x + dimensions.y * .6f));
                Assert.That(minAlong, Is.LessThan(-dimensions.y * .6f));
                Assert.That(maxAcross - minAcross, Is.GreaterThan(dimensions.y * 1.6f), "Thin old rails do not cover the collision width.");
            }
        }
    }

    [Test]
    public void CachePreservesOriginalRadiusAndReleasesOwnedSprites()
    {
        var cache = new EffectSpriteCache();
        Assert.That(cache.HasDQTelegraph, Is.True);
        var sprite = cache.DQDash(3.6386, .3164, 0, 3);
        var texture = sprite.texture;
        Assert.That(cache.DQDash(3.6386, .3164, 360, 3), Is.SameAs(sprite));
        Assert.That(cache.DQDash(3.6386, .3165, 0, 3), Is.Not.SameAs(sprite));
        cache.Dispose();
        Assert.That(sprite == null && texture == null, Is.True);
    }

    [Test]
    public void EndingOneWarningDoesNotDestroyAnotherWarning()
    {
        var cache = new EffectSpriteCache();
        var first = cache.DQDash(3.6386, .3164, 5.625f, 2, 1);
        var second = cache.DQDash(3.6386, .3164, 5.625f, 2, 2);
        cache.ReleaseDQDash(1);
        Assert.That(first == null, Is.True);
        Assert.That(second != null, Is.True);
        cache.ReleaseDQDash(1);
        cache.Dispose();
        Assert.That(second == null, Is.True);
    }
}
