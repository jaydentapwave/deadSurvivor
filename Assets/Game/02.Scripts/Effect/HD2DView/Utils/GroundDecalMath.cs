using UnityEngine;
using UnityEngine.Rendering;

public static class GroundDecalMath
{
    public static Mesh Build(TerrainHeightGrid grid, Vector3 center, Vector2 size, float yawDegrees, float spacing = .25f, float lift = .002f)
    {
        var width = Mathf.Max(1, Mathf.CeilToInt(size.x / spacing));
        var depth = Mathf.Max(1, Mathf.CeilToInt(size.y / spacing));
        var vertices = new Vector3[(width + 1) * (depth + 1)];
        var uv = new Vector2[vertices.Length];
        var indices = new int[width * depth * 6];
        var rotation = Quaternion.Euler(0, yawDegrees, 0);
        for (var j = 0; j <= depth; j++)
        {
            for (var i = 0; i <= width; i++)
            {
                var index = j * (width + 1) + i;
                uv[index] = new Vector2((float)i / width, (float)j / depth);
                var p = center + rotation * new Vector3((uv[index].x - .5f) * size.x, 0, (uv[index].y - .5f) * size.y);
                vertices[index] = new Vector3(p.x, TerrainHeightMath.Sample(grid, p.x, p.z) + lift, p.z);
                if (i < width && j < depth)
                {
                    var t = (j * width + i) * 6;
                    indices[t] = index; indices[t + 1] = index + width + 2; indices[t + 2] = index + 1;
                    indices[t + 3] = index; indices[t + 4] = index + width + 1; indices[t + 5] = index + width + 2;
                }
            }
        }
        var mesh = new Mesh { name = "Terrain decal", indexFormat = IndexFormat.UInt32, vertices = vertices, uv = uv, triangles = indices };
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }
}
