using NUnit.Framework;
using UnityEngine;

public class TerrainHeightMathTests
{
    public static TerrainHeightGrid Plane(float dx, float dz)
    {
        var mm = new int[81];
        for (var j = 0; j < 9; j++)
        {
            for (var i = 0; i < 9; i++)
            {
                mm[j * 9 + i] = Mathf.RoundToInt((dx * (i - 2) + dz * (j - 2)) * 1000);
            }
        }
        return TerrainHeightGrid.FromMillimetres(1, -2, -2, 9, 9, mm);
    }

    [Test]
    public void TH1TH2TH3TH6_TrianglesEdgesAndClamping()
    {
        var grid = TerrainHeightGrid.FromMillimetres(1, 0, 0, 2, 2, new[] { 0, 1000, 2000, 4000 });
        Assert.AreEqual(1.5f, TerrainHeightMath.Sample(grid, .75f, .25f), 1e-6);
        Assert.AreEqual(2f, TerrainHeightMath.Sample(grid, .25f, .75f), 1e-6);
        for (var j = 0; j < 2; j++)
        {
            for (var i = 0; i < 2; i++)
            {
                Assert.AreEqual(grid.Height(i, j), TerrainHeightMath.Sample(grid, i, j), 1e-6);
            }
        }
        Assert.AreEqual(2f, TerrainHeightMath.Sample(grid, -4, 5), 1e-6);
        Assert.Less(Mathf.Abs(TerrainHeightMath.Sample(grid, .5f - 1e-6f, .5f) - TerrainHeightMath.Sample(grid, .5f + 1e-6f, .5f)), 1e-5);
    }

    [Test]
    public void TH5TH7TP1TP2_NormalMinimumAndPlaneProjection()
    {
        var grid = Plane(.2f, 0);
        Assert.Less(Vector3.Distance(new Vector3(-.2f, 1, 0).normalized, TerrainHeightMath.Normal(grid, 2, 3)), 1e-6);
        Assert.AreEqual(.9f, TerrainHeightMath.MinUnder(grid, 5, 5, .5f), 1e-6);
        var p = new Vector3(2, 4, 3);
        var flat = TerrainHeightMath.ProjectOntoPlane(p, new Vector3(0, -1, 1), Vector3.zero, Vector3.up);
        Assert.AreEqual(new Vector3(2, 0, 7), flat);
        var n = new Vector3(-.2f, 1, 0).normalized;
        Assert.AreEqual(0, Vector3.Dot(TerrainHeightMath.ProjectOntoPlane(p, Vector3.down, Vector3.zero, n), n), 1e-6);
    }

    [Test]
    public void TS1TS2TS3TS4TS5_SnapStaysOnTerrainAndScreenGrid()
    {
        Assert.Less(Vector3.Distance(TiltedViewMath.SnapToTexels(new Vector3(.013f, 0, .03f), .5f, 48),
            TerrainHeightMath.SnapOnTerrain(TerrainHeightGrid.Flat, .013f, .03f, .5f, 48)), 1e-6);
        var ridge = TerrainHeightGrid.FromMillimetres(1, -1, -1, 3, 3, new[] { 40, 40, 40, 0, 0, 0, 40, 40, 40 });
        foreach (var grid in new[] { Plane(.2f, .1f), ridge })
        {
            for (var i = 0; i < 200; i++)
            {
                var snapped = TerrainHeightMath.SnapOnTerrain(grid, Mathf.Sin(i * 1.3f), Mathf.Cos(i * 2.7f), .5f, 48);
                var screen = TiltedViewMath.WorldToScreenPlane(snapped, .5f) * 48;
                Assert.AreEqual(Mathf.Round(screen.x), screen.x, 1e-5);
                Assert.AreEqual(Mathf.Round(screen.y), screen.y, 1e-5);
                Assert.AreEqual(TerrainHeightMath.Sample(grid, snapped.x, snapped.z), snapped.y, 1e-6);
            }
        }
        var point = TerrainHeightMath.SnapOnTerrain(Plane(0, .2f), 0, .02f, .5f, 48);
        Assert.AreEqual(.03094649f, point.z, 1e-6);
        Assert.Less(Vector3.Distance(point, TiltedViewMath.SnapScreenPlane(point, .5f, 48)), 1e-6);
        Assert.AreEqual(.04166667f, TiltedViewMath.SnapToTexels(point, .5f, 48).z, 1e-6);
    }

    [Test]
    public void TB1TB2_ActorBiasUsesRayDepthAndFlatIsZero()
    {
        Assert.AreEqual(0, TerrainHeightMath.ActorDepthBias(TerrainHeightGrid.Flat, .15f, .5f));
        var grid = Plane(.2f, 0);
        Assert.AreEqual(.1098f, TerrainHeightMath.ActorDepthBias(grid, .15f, .5f), .0001);
        Assert.AreEqual(.2016f, TerrainHeightMath.ActorDepthBias(grid, .3f, .5f), .0001);
        for (var i = 0; i < 200; i++)
        {
            var angle = i * Mathf.PI * 2 / 200;
            var slope = Plane(.2f * Mathf.Cos(angle), .2f * Mathf.Sin(angle));
            var p = new Vector3(.15f, 0, 0);
            Assert.LessOrEqual(TerrainHeightMath.RayGroundDepthGap(slope, p, Mathf.PI / 6),
                TerrainHeightMath.ActorDepthBias(slope, .15f, .5f) - .018f + 1e-5);
        }
    }

    [Test]
    public void TF1_ProjectionUsesGridAndFlatIsPreviousContract()
    {
        var p = IsoProjection.FloorToWorld(new Vector2(2, 3), 1, Plane(.2f, .1f));
        Assert.AreEqual(.7f, p.y, 1e-6);
        Assert.AreEqual(IsoProjection.FloorToWorld(new Vector2(2, 3), 1), IsoProjection.FloorToWorld(new Vector2(2, 3), 1, TerrainHeightGrid.Flat));
    }
    [Test]
    public void TV2_ClampedSpawnAndRecycleRespectPadAfterClampingCenter()
    {
        var old = new Vector2(-7.99656f, -2.75344f);
        var angle = 3.9837138f;
        var d = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
        var before = PartyMoveMath.ClampToDiamond(old + d * (new Vector2(10, 11.25f).magnitude + 70f * .0113f), 32);
        Assert.AreEqual(.01011f, Mathf.Abs(before.y - old.y) - 11.25f, .00002);
        const float pad = .907692f;
        var center = PartyMoveMath.ClampToDiamond(old, 32 - 10 - 11.25f - pad);
        Assert.AreEqual(-7.542714f, center.x, 1e-5);
        Assert.AreEqual(-2.299594f, center.y, 1e-5);
        var after = PartyMoveMath.ClampToDiamond(center + d * (new Vector2(10, 11.25f + pad).magnitude + 70f * .0113f), 32);
        Assert.AreEqual(-17.95879056f, after.x, 1e-5);
        Assert.Greater(Mathf.Abs(after.x - center.x), 10);
        var limit = 32 - 10 - 11.25f - pad;
        for (var i = 0; i <= 40; i++)
        {
            for (var j = 0; j <= 40; j++)
            {
                var c = PartyMoveMath.ClampToDiamond(new Vector2((i / 20f - 1) * limit, (j / 20f - 1) * limit), limit);
                for (var degrees = 0; degrees < 360; degrees++)
                {
                    var v = new Vector2(Mathf.Cos(degrees * Mathf.Deg2Rad), Mathf.Sin(degrees * Mathf.Deg2Rad));
                    Check(c, v, new Vector2(10, 11.25f + pad).magnitude + 70f * .0113f);
                    Check(c, v, Mathf.Min((10 + 150f * .0113f) / Mathf.Max(.0001f, Mathf.Abs(v.x)), (11.25f + pad + 150f * .0113f) / Mathf.Max(.0001f, Mathf.Abs(v.y))));
                }
            }
        }
        void Check(Vector2 c, Vector2 v, float distance)
        {
            var offset = PartyMoveMath.ClampToDiamond(c + v * distance, 32) - c;
            if (Mathf.Abs(offset.x) <= 10)
            {
                Assert.GreaterOrEqual(Mathf.Abs(offset.y) - 11.25f, pad - 1e-4);
            }
        }
    }
}
