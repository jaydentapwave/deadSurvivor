using System;
using NUnit.Framework;

public class TerrainHeightGridTests
{
    [Test]
    public void TD1TD2_InvalidDimensionsCellAndLengthThrow()
    {
        Assert.Throws<ArgumentException>(() => TerrainHeightGrid.FromMillimetres(1, 0, 0, 3, 3, new int[8]));
        Assert.Throws<ArgumentException>(() => TerrainHeightGrid.FromMillimetres(1, 0, 0, 1, 3, new int[3]));
        Assert.Throws<ArgumentException>(() => TerrainHeightGrid.FromMillimetres(0, 0, 0, 2, 2, new int[4]));
        Assert.Throws<ArgumentException>(() => TerrainHeightGrid.FromMillimetres(float.NaN, 0, 0, 2, 2, new int[4]));
    }

    [Test]
    public void TD3TD4_ArrayIsCopiedAndPropertiesAreComputed()
    {
        var mm = new[] { 0, 200, 0, 200 };
        var grid = TerrainHeightGrid.FromMillimetres(1, 0, 0, 2, 2, mm);
        mm[1] = 900;
        Assert.AreEqual(.2f, grid.Height(1, 0), 1e-6);
        Assert.AreEqual(.2f, grid.MaxSlope, 1e-6);
        Assert.AreEqual(0, grid.MaxVertexCurvature, 1e-6);
        Assert.AreEqual(.2f, grid.ReliefWithin(2), 1e-6);
        Assert.IsFalse(grid.IsFlat);
        Assert.IsTrue(TerrainHeightGrid.Flat.IsFlat);
        var ridge = TerrainHeightGrid.FromMillimetres(1, -1, 0, 3, 2, new[] { 50, 0, 50, 50, 0, 50 });
        Assert.AreEqual(.1f, ridge.MaxVertexCurvature, 1e-6);
    }
    [Test]
    public void TD4_ReliefIncludesClippedTriangleBoundaryExtrema()
    {
        var mm = new int[53 * 53];
        for (var j = 0; j < 53; j++)
        {
            for (var i = 0; i < 53; i++) { mm[j * 53 + i] = (i - 26) * 10; }
        }
        var grid = TerrainHeightGrid.FromMillimetres(1, -26, -26, 53, 53, mm);
        Assert.LessOrEqual(grid.MaxSlope, .2f);
        Assert.LessOrEqual(grid.MaxVertexCurvature, .08f);
        Assert.AreEqual(.51f, grid.ReliefWithin(25.5f), 1e-6);
        var enclosing = TerrainHeightGrid.FromMillimetres(20, -10, -10, 2, 2, new[] { -100, 100, -100, 100 });
        Assert.AreEqual(.01f, enclosing.ReliefWithin(.5f), 1e-6);
        Assert.AreEqual(0, enclosing.ReliefWithin(0), 1e-6);
    }

}
