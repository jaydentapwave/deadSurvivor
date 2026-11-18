using NUnit.Framework;
using UnityEngine;

public class GroundDecalMathTests
{
    [Test]
    public void TC1TC3_DecalPreservesXZAndUVAndFollowsTerrain()
    {
        foreach (var grid in new[] { TerrainHeightGrid.Flat, TerrainHeightMathTests.Plane(.2f, 0) })
        {
            var mesh = GroundDecalMath.Build(grid, new Vector3(2, 0, 3), new Vector2(2, 1), 30, .25f, .001f);
            try
            {
                var vertices = mesh.vertices;
                for (var i = 0; i < vertices.Length; i++)
                {
                    var p = vertices[i];
                    Assert.AreEqual(TerrainHeightMath.Sample(grid, p.x, p.z) + .001f, p.y, 1e-6);
                    var expected = new Vector3(2, 0, 3) + Quaternion.Euler(0, 30, 0) * new Vector3((mesh.uv[i].x - .5f) * 2, 0, mesh.uv[i].y - .5f);
                    Assert.AreEqual(expected.x, p.x, 1e-6);
                    Assert.AreEqual(expected.z, p.z, 1e-6);
                }
                Assert.AreEqual(Vector2.zero, mesh.uv[0]);
                Assert.AreEqual(Vector2.one, mesh.uv[mesh.uv.Length - 1]);
            }
            finally
            {
                Object.DestroyImmediate(mesh);
            }
        }
    }
}
