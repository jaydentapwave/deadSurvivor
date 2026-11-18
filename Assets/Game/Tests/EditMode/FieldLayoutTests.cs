using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

public class FieldLayoutTests
{
    private static FieldLayoutData Load() => JsonUtility.FromJson<FieldLayoutData>(File.ReadAllText("Assets/Game/03.Resources/Resources/Data/Field/field_layout.json"));

    [Test]
    public void BakeCopiesSelectedSeedBytesAndRepeatsExactly()
    {
        System.Type.GetType("FieldLayoutBaker, Game.Editor").GetMethod("Bake").Invoke(null, null);
        foreach (var file in new[] { "field_layout.json", "ground_path.json", "FieldPathMask.png", "FieldWaterMask.png", "FieldBankMask.png", "FieldZoneMask.png" })
        {
            var destination = "Assets/Game/03.Resources/" + (file.EndsWith("json") ? "Resources/Data/Field/" : "Textures/HD2D/") + file;
            var first = File.ReadAllBytes(destination);
            CollectionAssert.AreEqual(File.ReadAllBytes("output/map-pcg/seed-8/export/" + file), first, file);
            System.Type.GetType("FieldLayoutBaker, Game.Editor").GetMethod("Bake").Invoke(null, null);
            CollectionAssert.AreEqual(first, File.ReadAllBytes(destination), file);
        }
    }

    [System.Serializable] private class Config
    {
        public Placement placement;
        public Gameplay gameplay;
        public Catalog catalog;
        public Banks banks;
    }
    [System.Serializable] private class Placement
    {
        public Caps densityCap;
        public Camp camp;
        public int fenceTotal;
        public int signs;
        public float pathClearanceTall;
    }
    [System.Serializable] private class Caps { public int tree, boulder, stone, bush, flowers; }
    [System.Serializable] private class Camp { public int tents, flags; }
    [System.Serializable] private class Gameplay { public float clearTallL1, moveBandL1; public int maxTallInMoveBand; }
    [System.Serializable] private class Banks { public float height, cell; }
    [System.Serializable] private class Entry { public bool tall, roadside; }
    [System.Serializable] private class Catalog
    {
        public Entry tree_02, bush_01, bush_02, boulder_01, rock_01, rock_02;
        public Entry flower_01, flower_02, flower_03, grass_tuft_01, grass_tuft_02;
        public Entry fence, sign, flag, tent, bridge;
    }
    private static Config LoadConfig() => JsonUtility.FromJson<Config>(File.ReadAllText("Tools/map_pcg/config/deadsurvivor.json"));
    private static Entry CatalogEntry(Config config, FieldLayoutPlacement p) =>
        (Entry)typeof(Catalog).GetField(p.kind == "prop" ? p.prop : p.sprite).GetValue(config.catalog);

    private static float CentrelineDistance(Vector2 point, Vector2[] path)
    {
        var distance = float.PositiveInfinity;
        for (var i = 1; i < path.Length; i++)
        {
            var delta = path[i] - path[i - 1];
            var t = Mathf.Clamp01(Vector2.Dot(point - path[i - 1], delta) / delta.sqrMagnitude);
            distance = Mathf.Min(distance, Vector2.Distance(point, path[i - 1] + delta * t));
        }
        return distance;
    }

    [Test]
    public void LayoutKeepsPathStartYellowAndCornersClear()
    {
        var data = Load();
        var config = LoadConfig();
        foreach (var p in data.placements)
        {
            Assert.IsTrue(FieldLayoutMath.IsOutsideCorners(p.Floor), p.group);
            Assert.Greater(FieldLayoutMath.L1(p.Floor), data.startClearance, p.group);
            var catalog = CatalogEntry(config, p);
            if (catalog.tall)
            {
                Assert.Greater(FieldLayoutMath.L1(p.Floor), config.gameplay.clearTallL1, p.group);
            }
            if (p.sprite == "flower_02")
            {
                Assert.Greater(FieldLayoutMath.L1(p.Floor), data.yellowClearance);
            }
            if (p.group == "rim")
            {
                Assert.Greater(p.y, 0);
                Assert.That(FieldLayoutMath.L1(p.Floor), Is.InRange(32.5f, 34.5f));
            }
            else if (catalog.tall && !catalog.roadside)
            {
                var clearance = CentrelineDistance(p.Floor, data.path.points) - data.path.halfWidth;
                // Camp props sit beside the branch terminus; the main road remains clear.
                if (p.prop != "tent" && p.prop != "flag")
                {
                    clearance = Mathf.Min(clearance, CentrelineDistance(p.Floor, data.path.branch) - .45f);
                }
                Assert.GreaterOrEqual(clearance, config.placement.pathClearanceTall - .05f, p.group + " " + p.Floor);
            }
            Assert.That(p.height, Is.InRange(0, config.banks.height));
        }
        Assert.LessOrEqual(data.placements.Count(p => CatalogEntry(config, p).tall && FieldLayoutMath.L1(p.Floor) <= config.gameplay.moveBandL1), config.gameplay.maxTallInMoveBand);
    }

    [Test]
    public void PcgCompositionAndLandmarksRemainVisible()
    {
        var data = Load();
        var config = LoadConfig();
        Assert.AreEqual(8, data.seed);
        Assert.Greater(data.path.points.Length, 2);
        Assert.Greater(data.path.branch.Length, 2);
        Assert.AreEqual(1, data.banks.Length);
        Assert.IsNotEmpty(data.banks[0].runs);
        Assert.IsEmpty(data.banks[0].rectangles);
        Assert.AreEqual(config.banks.height, data.banks[0].height);
        Assert.AreEqual(config.banks.cell, data.banks[0].cell);
        Assert.That(data.water.Length, Is.InRange(2, 4));
        Assert.AreEqual(config.placement.camp.tents, data.placements.Count(p => p.prop == "tent"));
        Assert.AreEqual(config.placement.camp.flags, data.placements.Count(p => p.prop == "flag"));
        Assert.AreEqual(config.placement.signs, data.placements.Count(p => p.prop == "sign"));
        Assert.AreEqual(config.placement.fenceTotal, data.placements.Count(p => p.prop == "fence"));
        Assert.AreEqual(1, data.placements.Count(p => p.prop == "bridge"));
        Assert.IsNotEmpty(data.placements.Where(p => p.group.StartsWith("wall_")));
        foreach (var p in data.placements.Where(p => p.kind == "prop" || p.group.StartsWith("wall_")))
        {
            Assert.IsTrue(FieldLayoutMath.IsVisible(p.Floor), p.group + " " + p.Floor);
        }
    }

    [TestCase("tree")]
    [TestCase("boulder")]
    [TestCase("stone")]
    [TestCase("bush")]
    [TestCase("flowers")]
    public void EveryWindowRespectsConfiguredPcgDensity(string group)
    {
        var config = LoadConfig();
        var cap = (int)typeof(Caps).GetField(group).GetValue(config.placement.densityCap);
        var points = Load().placements.Where(p => p.group == group).Select(p => p.Floor).ToArray();
        Assert.IsNotEmpty(points, group);
        Assert.LessOrEqual(FieldLayoutMath.MaxWindowCount(points), cap, group);
    }
}
