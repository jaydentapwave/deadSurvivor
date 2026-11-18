// Source Assets/Game/02.Scripts/Editor/HD2D/Round8/FieldLookdevAssets8.cs SHA256 462ed194540785f9e6d3202e3c71f109f23e3e7476f76a0162a83befcecc7a9a
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
        // Form the union first: joins between rectangles are not exposed banks.
        foreach (var rect in rectangles)
        {
            for (var y = Mathf.RoundToInt(rect.yMin / step); y < Mathf.RoundToInt(rect.yMax / step); y++)
            for (var x = Mathf.RoundToInt(rect.xMin / step); x < Mathf.RoundToInt(rect.xMax / step); x++)
                cellSet.Add(new Vector2Int(x, y));
        }
        var removed = new HashSet<Vector2Int>();
        foreach (var normal in new[] { Vector2Int.left, Vector2Int.up, Vector2Int.right, Vector2Int.down })
        {
            var vertical = normal.x != 0;
            var lines = cellSet.Where(p => !cellSet.Contains(p + normal))
                .GroupBy(p => vertical ? p.x : p.y).OrderBy(g => g.Key);
            foreach (var line in lines)
            {
                var edge = line.OrderBy(p => vertical ? p.y : p.x).ToArray();
                var start = 0;
                while (start < edge.Length)
                {
                    var end = start + 1;
                    while (end < edge.Length && edge[end] - edge[end - 1] == (vertical ? Vector2Int.up : Vector2Int.right)) end++;
                    var depths = Walk(end - start, random);
                    for (var i = start; i < end; i++)
                    for (var inset = 0; inset < depths[i - start]; inset++)
                    {
                        var cell = edge[i] - normal * inset;
                        if (cellSet.Contains(cell)) removed.Add(cell);
                    }
                    start = end;
                }
            }
        }
        cellSet.ExceptWith(removed);
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
