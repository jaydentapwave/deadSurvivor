using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEditor;

public class TerrainDataContractTests
{
    private static TerrainHeightGrid LoadGrid()
    {
        var data = JsonUtility.FromJson<FieldHeightData>(File.ReadAllText("Assets/Game/03.Resources/Resources/Data/Field/field_height.json"));
        return TerrainHeightGrid.FromMillimetres(data.cell, data.originX, data.originZ, data.width, data.depth, data.heightsMm);
    }

    [Test]
    public void ActorDepthBiasIncludesPaintedPartyMaterial()
    {
        var actor = AssetDatabase.LoadAssetAtPath<Material>("Assets/Game/03.Resources/Materials/HD2D/HD2D_SpriteLit.mat");
        // BackgroundCard selects the preserved colour palette, not actor/background identity.
        Assert.That(actor.GetFloat("_BackgroundCard"), Is.EqualTo(1));
        var shader = File.ReadAllText(AssetDatabase.GetAssetPath(actor.shader));
        Assert.That(shader, Does.Contain("if (_HD2DActorDepthBias > 0)"));
        Assert.That(shader, Does.Not.Contain("_HD2DActorDepthBias > 0 && _BackgroundCard"));
    }

    [Test]
    public void TV1ActualHeightArrayFitsConfiguredDepthPad()
    {
        var grid = LoadGrid();
        var view = JsonUtility.FromJson<ViewRulesData>(File.ReadAllText("Assets/Game/03.Resources/Resources/Data/View/view_rules.json"));
        var required = grid.ReliefWithin(32) * Mathf.Cos(view.TiltDegrees * Mathf.Deg2Rad) / view.SinTilt + 1f / (48 * view.SinTilt);
        Assert.That(grid.IsFlat, Is.False);
        Assert.That(view.TerrainDepthPad, Is.GreaterThanOrEqualTo(required));
        Assert.That(grid.MaxSlope, Is.LessThanOrEqualTo(.2f));
        Assert.That(grid.MaxVertexCurvature, Is.LessThanOrEqualTo(.08f));
    }

    [Test]
    public void TV2CentersAreClampedToEachPadsNewLimitBeforeSpawnAndRecycle()
    {
        var center = new Vector2(-7.99656f, -2.75344f);
        var angle = 228.24999f * Mathf.Deg2Rad;
        var direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
        var oldPoint = PartyMoveMath.ClampToDiamond(center + direction * (new Vector2(10, 11.25f).magnitude + .791f), 32);
        Assert.That(Mathf.Abs(oldPoint.y - center.y) - 11.25f, Is.EqualTo(.01011f).Within(.001f));
        const float pad = .907692f;
        var extent = new Vector2(10, 11.25f + pad);
        var limit = 32 - extent.x - extent.y;
        center = PartyMoveMath.ClampToDiamond(center, limit);
        Assert.That(center.x, Is.EqualTo(-7.542714f).Within(.00001f));
        Assert.That(center.y, Is.EqualTo(-2.299594f).Within(.00001f));
        var point = PartyMoveMath.ClampToDiamond(center + direction * (extent.magnitude + .791f), 32);
        Assert.That(point.x, Is.EqualTo(-17.95879056f).Within(.00001f));
        Assert.That(point.y, Is.EqualTo(-14.04120944f).Within(.00001f));
        Assert.That(Mathf.Abs(point.x - center.x), Is.GreaterThan(10));
        for (var i = 0; i <= 40; i++)
        {
            for (var j = 0; j <= 40; j++)
            {
                var c = PartyMoveMath.ClampToDiamond(new Vector2(-limit + 2 * limit * i / 40, -limit + 2 * limit * j / 40), limit);
                for (var degrees = 0; degrees < 360; degrees++)
                {
                    var d = new Vector2(Mathf.Cos(degrees * Mathf.Deg2Rad), Mathf.Sin(degrees * Mathf.Deg2Rad));
                    var spawned = PartyMoveMath.ClampToDiamond(c + d * (extent.magnitude + .791f), 32);
                    var distance = Mathf.Min((extent.x + 1.695f) / Mathf.Max(.0001f, Mathf.Abs(d.x)), (extent.y + 1.695f) / Mathf.Max(.0001f, Mathf.Abs(d.y)));
                    var recycled = PartyMoveMath.ClampToDiamond(c + d * distance, 32);
                    Check(spawned - c); Check(recycled - c);
                }
            }
        }
        void Check(Vector2 offset)
        {
            if (Mathf.Abs(offset.x) <= 10)
            {
                Assert.That(Mathf.Abs(offset.y) - 11.25f, Is.GreaterThanOrEqualTo(pad - .0001f));
            }
        }
    }

    [Test]
    public void TW1WaterAndBridgeSurroundingsRemainFlat()
    {
        var grid = LoadGrid();
        var layout = JsonUtility.FromJson<FieldLayoutData>(File.ReadAllText("Assets/Game/03.Resources/Resources/Data/Field/field_layout.json"));
        foreach (var water in layout.water)
        {
            for (var segment = 0; segment < water.points.Length - 1; segment++)
            {
                for (var i = 0; i <= 20; i++)
                {
                    Check(Vector2.Lerp(water.points[segment], water.points[segment + 1], i / 20f), water.width * .5f + 1.5f);
                }
            }
        }
        foreach (var placement in layout.placements.Where(p => p.prop == "bridge")) { Check(placement.Floor, 2.5f); }
        void Check(Vector2 center, float radius)
        {
            for (var angle = 0; angle < 360; angle += 15)
            {
                for (var r = 0; r <= 4; r++)
                {
                    var point = center + new Vector2(Mathf.Cos(angle * Mathf.Deg2Rad), Mathf.Sin(angle * Mathf.Deg2Rad)) * (r * radius / 4);
                    Assert.That(TerrainHeightMath.Sample(grid, point.x, point.y), Is.EqualTo(0).Within(.001f), point.ToString());
                }
            }
        }
    }
}
