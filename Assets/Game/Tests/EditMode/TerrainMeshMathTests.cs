using NUnit.Framework;
using UnityEngine;

public class TerrainMeshMathTests
{
    [Test]
    public void TH4_MeshAndSamplerUseSameTriangles()
    {
        var grid = TerrainHeightGrid.FromMillimetres(1, -1, -1, 3, 3, new[] { 0, 10, 30, 20, 70, 5, 80, 15, 40 });
        var mesh = TerrainMeshMath.Build(grid);
        try
        {
            var random = new System.Random(8);
            var vertices = mesh.vertices;
            var indices = mesh.triangles;
            for (var i = 0; i < 200; i++)
            {
                var t = random.Next(indices.Length / 3) * 3;
                var point = (vertices[indices[t]] + vertices[indices[t + 1]] + vertices[indices[t + 2]]) / 3;
                Assert.AreEqual(point.y, TerrainHeightMath.Sample(grid, point.x, point.z), 1e-5);
                Assert.AreEqual(vertices[indices[t]].y, mesh.uv2[indices[t]].x, 1e-6);
                Assert.Greater(Vector3.Cross(vertices[indices[t + 1]] - vertices[indices[t]], vertices[indices[t + 2]] - vertices[indices[t]]).y, 0);
            }
        }
        finally
        {
            Object.DestroyImmediate(mesh);
        }
    }
}
