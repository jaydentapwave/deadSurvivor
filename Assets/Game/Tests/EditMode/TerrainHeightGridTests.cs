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
}
