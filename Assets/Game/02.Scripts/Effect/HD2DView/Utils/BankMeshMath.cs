using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;

// Adapted from Editor/HD2D/Round8/FieldLookdevAssets8.BankStrip; provenance in field mesh source.txt.
// Only exposed outer cells are inset. Random runs replace the old modulo-5/7 teeth.
public static class BankMeshMath
{
    public static HashSet<Vector2Int> Cells(Rect[] rectangles, float step, int seed)
    {
        var random = new System.Random(seed);
        var cellSet = new HashSet<Vector2Int>();
        foreach (var rect in rectangles)
        {
            var left = Mathf.RoundToInt(rect.xMin / step);
            var right = Mathf.RoundToInt(rect.xMax / step);
            var bottom = Mathf.RoundToInt(rect.yMin / step);
            var top = Mathf.RoundToInt(rect.yMax / step);
            var leftEdge = Walk(top - bottom, random);
            var rightEdge = Walk(top - bottom, random);
            var bottomEdge = Walk(right - left, random);
            var topEdge = Walk(right - left, random);
            for (var y = bottom; y < top; y++)
            {
                for (var x = left; x < right; x++)
                {
                    if (x >= left + leftEdge[y - bottom] && x < right - rightEdge[y - bottom] &&
                        y >= bottom + bottomEdge[x - left] && y < top - topEdge[x - left])
                    {
                        cellSet.Add(new Vector2Int(x, y));
                    }
                }
            }
        }
        return cellSet;
    }

    private static int[] Walk(int count, System.Random random)
    {
        var result = new int[count];
        var depth = random.Next(3);
        var end = 0;
        while (end < count)
        {
            var length = random.Next(2, 8);
            for (var i = 0; i < length && end < count; i++)
            {
                result[end++] = depth;
            }
            depth = Math.Max(0, Math.Min(2, depth + random.Next(-2, 3)));
        }
        return result;
    }

    public static Mesh Build(Rect[] rectangles, float step, float height, int seed)
    {
        var cellSet = Cells(rectangles, step, seed);
        var vertexList = new List<Vector3>();
        var topList = new List<int>();
        var sideList = new List<int>();
        void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, List<int> indexList)
        {
            var index = vertexList.Count;
            vertexList.AddRange(new[] { a, b, c, d });
            indexList.AddRange(new[] { index, index + 1, index + 2, index, index + 2, index + 3 });
        }
        foreach (var cell in cellSet.OrderBy(p => p.y).ThenBy(p => p.x))
        {
            var x = cell.x * step;
            var z = cell.y * step;
            var corner = new[] { new Vector3(x, 0, z), new Vector3(x, 0, z + step), new Vector3(x + step, 0, z + step), new Vector3(x + step, 0, z) };
            Quad(corner[0] + Vector3.up * height, corner[1] + Vector3.up * height, corner[2] + Vector3.up * height, corner[3] + Vector3.up * height, topList);
            var adjacent = new[] { Vector2Int.left, Vector2Int.up, Vector2Int.right, Vector2Int.down };
            for (var side = 0; side < 4; side++)
            {
                if (cellSet.Contains(cell + adjacent[side]))
                {
                    continue;
                }
                var a = corner[side];
                var b = corner[(side + 1) % 4];
                Quad(a, b, b + Vector3.up * height, a + Vector3.up * height, sideList);
            }
        }
        var mesh = new Mesh { name = "Irregular grass bank", indexFormat = IndexFormat.UInt32, subMeshCount = 2 };
        mesh.SetVertices(vertexList);
        mesh.SetTriangles(topList, 0);
        mesh.SetTriangles(sideList, 1);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }
}
