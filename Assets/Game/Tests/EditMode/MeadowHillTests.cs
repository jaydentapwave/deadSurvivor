using NUnit.Framework;
using UnityEngine;

public class MeadowHillTests
{
    private static TerrainHeightGrid Judgment() => TerrainHeightGrid.FromMillimetres(1f, -36f, -36f, 73, 73,
        new ResourceService().LoadJson<FieldHeightData>("Data/Field/field_height").heightsMm);

    private static FieldLayoutData Layout() => new ResourceService().LoadJson<FieldLayoutData>("Data/Field/field_layout");

    [Test]
    public void ShippedFieldGetsVisibleHillsUnderTheSlopeLimit()
    {
        var judgment = Judgment();
        var visual = MeadowHillMath.Build(judgment, Layout());
        Assert.LessOrEqual(visual.MaxSlope, MeadowHillMath.MaxSlope + 1e-4f);
        // Hills must actually read: at least 40 cm of relief inside the playable diamond beyond the judgment relief.
        Assert.Greater(visual.ReliefWithin(32) - judgment.ReliefWithin(32), .4f);
        Assert.AreEqual(judgment.Width, visual.Width);
        Assert.AreEqual(judgment.OriginX, visual.OriginX);
    }

    [Test]
    public void EveryPointInsideWaterBridgeAndTentZonesKeepsTheJudgmentHeight()
    {
        var judgment = Judgment();
        var layout = Layout();
        var visual = MeadowHillMath.Build(judgment, layout);
        // Final interpolated height (not only grid corners) on a 0.25 m lattice, incl. water edges, bridge ends, tent corners.
        var checkedCount = 0;
        for (var z = -35.9f; z < 35.9f; z += .25f)
        {
            for (var x = -35.9f; x < 35.9f; x += .25f)
            {
                if (MeadowHillMath.Clearance(x, z, layout) <= 0f)
                {
                    Assert.AreEqual(TerrainHeightMath.Sample(judgment, x, z), TerrainHeightMath.Sample(visual, x, z), 1e-5f, $"{x},{z}");
                    checkedCount++;
                }
            }
        }
        Assert.Greater(checkedCount, 1000);
        foreach (var placement in layout.placements)
        {
            if (placement.group == "bridge" || placement.group == "tent")
            {
                Assert.LessOrEqual(MeadowHillMath.Clearance(placement.x, placement.y, layout), 0f);
            }
        }
    }

    [Test]
    public void MaskBlendsSmoothlyAndHillIsDeterministic()
    {
        var layout = new FieldLayoutData
        {
            seed = 3,
            water = new[] { new FieldWaterLayout { name = "stream", width = 2f, points = new[] { new Vector2(0, 0), new Vector2(10, 0) } } },
            placements = new FieldLayoutPlacement[0],
        };
        var edge = 1f + MeadowHillMath.FlatRadius;
        Assert.AreEqual(0f, MeadowHillMath.Mask(5, edge, layout), 1e-6f);
        Assert.AreEqual(1f, MeadowHillMath.Mask(5, edge + MeadowHillMath.BlendWidth, layout), 1e-6f);
        Assert.AreEqual(.5f, MeadowHillMath.Mask(5, edge + MeadowHillMath.BlendWidth / 2, layout), 1e-5f);
        Assert.AreEqual(0f, MeadowHillMath.Mask(5, edge + 1f, layout, 1f), 1e-6f);
        // Beyond the segment end the distance is to the end point, not the infinite line.
        Assert.AreEqual(1f, MeadowHillMath.Mask(10 + edge + MeadowHillMath.BlendWidth, 0, layout), 1e-6f);
        Assert.AreEqual(MeadowHillMath.Hill(3.3f, -7.1f, 3), MeadowHillMath.Hill(3.3f, -7.1f, 3));
        Assert.LessOrEqual(Mathf.Abs(MeadowHillMath.Hill(12.5f, 4f, 3)), MeadowHillMath.Amplitude);
    }

    [Test]
    public void PondClosingEdgeIsProtectedInTheFinalHeight()
    {
        // Square pond (0,0)->(20,0)->(20,20)->(0,20); (-2,10) is 2 m from the closing (left) edge.
        var layout = new FieldLayoutData
        {
            seed = 5,
            water = new[] { new FieldWaterLayout { name = "pond", points = new[] { new Vector2(0, 0), new Vector2(20, 0), new Vector2(20, 20), new Vector2(0, 20) } } },
            placements = new FieldLayoutPlacement[0],
        };
        Assert.AreEqual(-1f, MeadowHillMath.Clearance(-2f, 10f, layout), 1e-5f);
        var visual = MeadowHillMath.Build(TerrainHeightGrid.Flat, layout);
        for (var x = -2.9f; x <= 0f; x += .25f)
        {
            Assert.AreEqual(0f, TerrainHeightMath.Sample(visual, x, 10f), 1e-5f, $"{x}");
        }
    }

    [Test]
    public void RoundingNeverLeavesTheResultAboveTheSlopeLimit()
    {
        // algorithm-tester counterexample: 2x2 judgment at slope .1198; scale .0052 rounded to mm gave .1208.
        var judgment = TerrainHeightGrid.FromMillimetres(1f, 3f, 4f, 2, 2, new[] { 0, 119, 14, 133 });
        var layout = new FieldLayoutData { seed = 0, water = new FieldWaterLayout[0], placements = new FieldLayoutPlacement[0] };
        Assert.LessOrEqual(judgment.MaxSlope, MeadowHillMath.MaxSlope);
        Assert.LessOrEqual(MeadowHillMath.Build(judgment, layout).MaxSlope, MeadowHillMath.MaxSlope);
    }

    [Test]
    public void EmptyLayoutStillBuildsAndFlatJudgmentStaysUnderLimit()
    {
        var layout = new FieldLayoutData { seed = 1, water = new FieldWaterLayout[0], placements = new FieldLayoutPlacement[0] };
        var visual = MeadowHillMath.Build(TerrainHeightGrid.Flat, layout);
        Assert.IsFalse(visual.IsFlat);
        Assert.LessOrEqual(visual.MaxSlope, MeadowHillMath.MaxSlope + 1e-4f);
    }
}
