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

    [Test]
    public void LayoutKeepsPathStartYellowAndCornersClear()
    {
        var data = Load();
        var path = JsonUtility.FromJson<GroundPathData>(File.ReadAllText("Assets/Game/03.Resources/Resources/Data/Field/ground_path.json"));
        var points = path.Cells.Select(c => IsoProjection.IsoCellToFloor(new Vector3Int(c.X,c.Y,0))).ToArray();
        foreach (var p in data.placements)
        {
            Assert.IsTrue(FieldLayoutMath.IsOutsideCorners(p.Floor),p.group);
            Assert.Greater(FieldLayoutMath.L1(p.Floor), data.startClearance,p.group);
            if (p.sprite == "flower_02")
            {
                Assert.Greater(FieldLayoutMath.L1(p.Floor),data.yellowClearance);
            }
            // Bridge crosses the preserved road. Shore, rim and masonry follow their own boundaries.
            if (p.group == "rim")
            {
                Assert.Greater(p.y,0);
                Assert.That(FieldLayoutMath.L1(p.Floor),Is.InRange(32.5f,34.5f));
            }
            else if (p.group != "shore" && !p.group.StartsWith("wall_") && p.prop != "bridge")
            {
                Assert.GreaterOrEqual(FieldLayoutMath.PathDistance(p.Floor,points),p.isRoadside ? data.roadsideClearance : data.pathClearance);
            }
        }
    }

    [Test]
    public void FCompositionAndLandmarksRemainVisible()
    {
        var data=Load();
        Assert.AreEqual(2,data.banks.Length);
        Assert.AreEqual(3,data.water.Length);
        Assert.AreEqual(2,data.placements.Count(p => p.prop=="tent"));
        Assert.AreEqual(1,data.placements.Count(p => p.prop=="bridge"));
        Assert.AreEqual(12,data.placements.Count(p => p.prop=="fence"));
        Assert.AreEqual(3,data.placements.Count(p => p.prop=="flag"));
        Assert.AreEqual(10,data.placements.Where(p => p.group.StartsWith("wall_")).Select(p=>p.group).Distinct().Count());
        foreach (var p in data.placements.Where(p=>p.kind=="prop" || p.group.StartsWith("wall_")))
        {
            Assert.IsTrue(FieldLayoutMath.IsVisible(p.Floor),p.group+" "+p.Floor);
        }
    }

    [TestCase("tree",18,24,6)]
    [TestCase("boulder",6,9,4)]
    [TestCase("stone",16,24,4)]
    [TestCase("bush",30,40,10)]
    [TestCase("flowers",450,600,150)]
    public void PopulationAndEveryWindowRespectFDensity(string group,int minimum,int maximum,int cap)
    {
        var points=Load().placements.Where(p=>p.group==group).Select(p=>p.Floor).ToArray();
        Assert.That(points.Length,Is.InRange(minimum,maximum),group);
        Assert.LessOrEqual(FieldLayoutMath.MaxWindowCount(points),cap,group);
    }
}
