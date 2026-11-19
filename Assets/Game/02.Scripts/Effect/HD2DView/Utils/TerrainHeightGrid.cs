using System;
using System.Collections.Generic;
using UnityEngine;

// Immutable view-only terrain in metres. JSON stores millimetres in row-major order.
public sealed class TerrainHeightGrid
{
    public float Cell { get; }
    public float OriginX { get; }
    public float OriginZ { get; }
    public int Width { get; }
    public int Depth { get; }
    public float MaxSlope { get; }
    public float MaxVertexCurvature { get; }
    public bool IsFlat { get; }
    public static TerrainHeightGrid Flat { get; } = FromMillimetres(1f, -36f, -36f, 73, 73, new int[73 * 73]);
    private readonly int[] _heightsMm;

    private TerrainHeightGrid(float cell, float originX, float originZ, int width, int depth, int[] heightsMm)
    {
        Cell = cell;
        OriginX = originX;
        OriginZ = originZ;
        Width = width;
        Depth = depth;
        _heightsMm = (int[])heightsMm.Clone();
        IsFlat = true;
        foreach (var height in _heightsMm)
        {
            IsFlat &= height == _heightsMm[0];
        }
        var gradients = new List<Vector2>[width * depth];
        for (var j = 0; j < depth - 1; j++)
        {
            for (var i = 0; i < width - 1; i++)
            {
                var lower = new Vector2(Height(i + 1, j) - Height(i, j), Height(i + 1, j + 1) - Height(i + 1, j)) / cell;
                var upper = new Vector2(Height(i + 1, j + 1) - Height(i, j + 1), Height(i, j + 1) - Height(i, j)) / cell;
                MaxSlope = Mathf.Max(MaxSlope, lower.magnitude, upper.magnitude);
                Add(i, j, lower); Add(i + 1, j, lower); Add(i + 1, j + 1, lower);
                Add(i, j, upper); Add(i, j + 1, upper); Add(i + 1, j + 1, upper);
            }
        }
        foreach (var gradientList in gradients)
        {
            if (gradientList == null)
            {
                continue;
            }
            foreach (var a in gradientList)
            {
                foreach (var b in gradientList)
                {
                    MaxVertexCurvature = Mathf.Max(MaxVertexCurvature, (a - b).magnitude);
                }
            }
        }
        void Add(int i, int j, Vector2 gradient)
        {
            var index = j * width + i;
            gradients[index] ??= new List<Vector2>();
            gradients[index].Add(gradient);
        }
    }

    public static TerrainHeightGrid FromMillimetres(float cell, float originX, float originZ, int width, int depth, int[] heightsMm)
    {
        // D-T8: the Game loading boundary logs and rethrows; this assembly has no Game dependency.
        if (width < 2 || depth < 2 || !(cell > 0f) || float.IsInfinity(cell) ||
            float.IsNaN(originX) || float.IsInfinity(originX) || float.IsNaN(originZ) || float.IsInfinity(originZ) ||
            heightsMm == null || heightsMm.LongLength != (long)width * depth)
        {
            throw new ArgumentException("[TerrainHeightGrid] Expected finite origin/cell, positive cell, dimensions >= 2 and width*depth millimetres.");
        }
        return new TerrainHeightGrid(cell, originX, originZ, width, depth, heightsMm);
    }

    public float Height(int i, int j) => _heightsMm[j * Width + i] * .001f;

    public float ReliefWithin(float halfDiagonal)
    {
        var min = float.PositiveInfinity;
        var max = float.NegativeInfinity;
        for (var j = 0; j < Depth - 1; j++)
        {
            for (var i = 0; i < Width - 1; i++)
            {
                var a = new Vector3(OriginX + i * Cell, Height(i, j), OriginZ + j * Cell);
                var b = new Vector3(a.x + Cell, Height(i + 1, j), a.z);
                var c = new Vector3(a.x + Cell, Height(i + 1, j + 1), a.z + Cell);
                var d = new Vector3(a.x, Height(i, j + 1), a.z + Cell);
                Include(a, b, c);
                Include(a, c, d);
            }
        }
        return max >= min ? max - min : 0f;

        void Include(Vector3 a, Vector3 b, Vector3 c)
        {
            var polygon = new List<Vector3> { a, b, c };
            foreach (var normal in new[] { new Vector2(1, 1), new Vector2(-1, 1), new Vector2(-1, -1), new Vector2(1, -1) })
            {
                var clipped = new List<Vector3>();
                for (var k = 0; k < polygon.Count; k++)
                {
                    var from = polygon[k];
                    var to = polygon[(k + 1) % polygon.Count];
                    var fromDistance = normal.x * from.x + normal.y * from.z - halfDiagonal;
                    var toDistance = normal.x * to.x + normal.y * to.z - halfDiagonal;
                    if (fromDistance <= 0f)
                    {
                        clipped.Add(from);
                    }
                    if ((fromDistance <= 0f) != (toDistance <= 0f))
                    {
                        clipped.Add(Vector3.LerpUnclamped(from, to, fromDistance / (fromDistance - toDistance)));
                    }
                }
                polygon = clipped;
            }
            foreach (var point in polygon)
            {
                min = Mathf.Min(min, point.y);
                max = Mathf.Max(max, point.y);
            }
        }
    }
}
