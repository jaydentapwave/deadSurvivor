using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public static class TerrainMeshMath
{
    public static Mesh Build(TerrainHeightGrid grid, float halfDiagonal = 36f)
    {
        // Preserve the original rasterisation when the data describes one plane.
        // Non-flat terrain retains the full, matching triangular height grid below.
        if (grid.IsFlat)
        {
            var height = grid.Height(0, 0);
            var flat = new Mesh
            {
                name = "Terrain flat ground",
                vertices = new[] { new Vector3(0, height, halfDiagonal), new Vector3(halfDiagonal, height, 0), new Vector3(0, height, -halfDiagonal), new Vector3(-halfDiagonal, height, 0) },
                triangles = new[] { 0, 1, 2, 0, 2, 3 }
            };
            flat.RecalculateNormals();
            flat.RecalculateBounds();
            return flat;
        }
        var vertices = new Vector3[grid.Width * grid.Depth];
        var uv = new Vector2[vertices.Length];
        var uv2 = new Vector2[vertices.Length];
        var indexList = new List<int>();
        for (var j = 0; j < grid.Depth; j++)
        {
            for (var i = 0; i < grid.Width; i++)
            {
                var index = j * grid.Width + i;
                vertices[index] = new Vector3(grid.OriginX + i * grid.Cell, grid.Height(i, j), grid.OriginZ + j * grid.Cell);
                uv[index] = new Vector2(vertices[index].x, vertices[index].z);
                uv2[index] = new Vector2(vertices[index].y, 0);
            }
        }
        for (var j = 0; j < grid.Depth - 1; j++)
        {
            for (var i = 0; i < grid.Width - 1; i++)
            {
                var a = j * grid.Width + i;
                Add(a, a + grid.Width + 1, a + 1);
                Add(a, a + grid.Width, a + grid.Width + 1);
            }
        }
        var mesh = new Mesh { name = "Terrain ground", indexFormat = IndexFormat.UInt32, vertices = vertices, uv = uv, uv2 = uv2 };
        mesh.SetTriangles(indexList, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
        void Add(int a, int b, int c)
        {
            var center = (vertices[a] + vertices[b] + vertices[c]) / 3f;
            if (Mathf.Abs(center.x) + Mathf.Abs(center.z) <= halfDiagonal)
            {
                indexList.Add(a); indexList.Add(b); indexList.Add(c);
            }
        }
    }
}
