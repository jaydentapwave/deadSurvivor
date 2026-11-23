using System;
using UnityEngine;

public static class TerrainHeightMath
{
    public const float ScreenGridToleranceTexels = 1e-5f;
    public const float GroundToleranceMetres = 1e-6f;
    public static bool ShadowFragmentSurvives(float silhouetteAlpha, float distanceFromContact, float contactRadius, float bankMask, bool isContactOnBank)
    {
        return silhouetteAlpha >= .5f && (contactRadius <= 0 || distanceFromContact <= contactRadius)
            && (isContactOnBank ? bankMask >= .5f : bankMask <= .5f);
    }

    public static float Sample(TerrainHeightGrid grid, float x, float z)
    {
        Coordinates(grid, x, z, out var i, out var j, out var fx, out var fz);
        var h00 = grid.Height(i, j);
        return fx >= fz
            ? h00 + fx * (grid.Height(i + 1, j) - h00) + fz * (grid.Height(i + 1, j + 1) - grid.Height(i + 1, j))
            : h00 + fz * (grid.Height(i, j + 1) - h00) + fx * (grid.Height(i + 1, j + 1) - grid.Height(i, j + 1));
    }

    public static Vector3 Normal(TerrainHeightGrid grid, float x, float z)
    {
        Coordinates(grid, x, z, out var i, out var j, out var fx, out var fz);
        var dx = fx >= fz ? grid.Height(i + 1, j) - grid.Height(i, j) : grid.Height(i + 1, j + 1) - grid.Height(i, j + 1);
        var dz = fx >= fz ? grid.Height(i + 1, j + 1) - grid.Height(i + 1, j) : grid.Height(i, j + 1) - grid.Height(i, j);
        // Sample clamps outside the grid, so its derivative there is zero.
        if (x < grid.OriginX || x > grid.OriginX + (grid.Width - 1) * grid.Cell)
        {
            dx = 0f;
        }
        if (z < grid.OriginZ || z > grid.OriginZ + (grid.Depth - 1) * grid.Cell)
        {
            dz = 0f;
        }
        return new Vector3(-dx / grid.Cell, 1f, -dz / grid.Cell).normalized;
    }

    public static Vector3 SnapOnTerrain(TerrainHeightGrid grid, float x, float z, float sinTilt, float pixelsPerUnit)
    {
        if (grid.IsFlat && grid.Height(0, 0) == 0f)
        {
            return TiltedViewMath.SnapToTexels(new Vector3(x, 0, z), sinTilt, pixelsPerUnit);
        }
        x = Mathf.Round(x * pixelsPerUnit) / pixelsPerUnit;
        var cos = Mathf.Sqrt(1f - sinTilt * sinTilt);
        var target = Mathf.Round((Sample(grid, x, z) * cos + z * sinTilt) * pixelsPerUnit) / pixelsPerUnit;
        for (var step = 0; step < 4; step++)
        {
            var height = Sample(grid, x, z);
            var error = height * cos + z * sinTilt - target;
            if (Mathf.Abs(error) <= .0000001f)
            {
                return AlignFloatPoint(new Vector3(x, height, z));
            }
            var normal = Normal(grid, x, z);
            z -= error / (sinTilt - normal.z / normal.y * cos);
        }
        return AlignFloatPoint(new Vector3(x, Sample(grid, x, z), z));

        Vector3 AlignFloatPoint(Vector3 point)
        {
            if (Aligned(point)) { return point; }
            // At far coordinates one float ULP can exceed 1e-5 texels. Search adjacent
            // representable z values, then compensate only within the existing 1e-6m
            // grounding allowance. Both public tolerances remain unchanged.
            for (var offset = -3; offset <= 3; offset++)
            {
                var bits = BitConverter.SingleToInt32Bits(point.z);
                var candidateZ = BitConverter.Int32BitsToSingle(bits + offset);
                var height = Sample(grid, x, candidateZ);
                for (var adjustment = 0; adjustment <= 8; adjustment++)
                {
                    var delta = adjustment == 0 ? 0 : ((adjustment + 1) / 2) * .00000025f * (adjustment % 2 == 1 ? 1 : -1);
                    var candidate = new Vector3(x, height + delta, candidateZ);
                    if (Mathf.Abs(candidate.y - height) <= GroundToleranceMetres && Aligned(candidate))
                    {
                        return candidate;
                    }
                }
            }
            throw new InvalidOperationException("[TerrainHeightMath] Terrain snap exceeds screen/ground tolerances after four triangles.");
        }
        bool Aligned(Vector3 point)
        {
            var screen = TiltedViewMath.WorldToScreenPlane(point, sinTilt) * pixelsPerUnit;
            return Mathf.Abs(screen.x - Mathf.Round(screen.x)) <= ScreenGridToleranceTexels &&
                Mathf.Abs(screen.y - Mathf.Round(screen.y)) <= ScreenGridToleranceTexels;
        }
    }

    public static Vector3 ProjectOntoPlane(Vector3 point, Vector3 direction, Vector3 planePoint, Vector3 planeNormal) =>
        point + direction * (Vector3.Dot(planePoint - point, planeNormal) / Vector3.Dot(direction, planeNormal));

    // Positive when point lies behind the terrain on the same camera ray. theta is in radians.
    public static float RayGroundDepthGap(TerrainHeightGrid grid, Vector3 point, float theta)
    {
        var sin = Mathf.Sin(theta);
        var cos = Mathf.Cos(theta);
        var t = 0f;
        for (var step = 0; step < 16; step++)
        {
            var z = point.z - t * cos;
            var error = point.y + t * sin - Sample(grid, point.x, z);
            if (Mathf.Abs(error) < .0000001f)
            {
                return t;
            }
            var normal = Normal(grid, point.x, z);
            t -= error / (sin - normal.z / normal.y * cos);
        }
        throw new InvalidOperationException("[TerrainHeightMath] Ground ray did not converge.");
    }

    // Exact minimum of a piecewise-linear height field over a disk: triangle vertices,
    // edge/circle intersections and each plane's downhill point on the circle.
    public static float MinUnder(TerrainHeightGrid grid, float x, float z, float radius)
    {
        var center = new Vector2(x, z);
        var min = Sample(grid, x, z);
        var iMin = Mathf.FloorToInt((x - radius - grid.OriginX) / grid.Cell);
        var iMax = Mathf.FloorToInt((x + radius - grid.OriginX) / grid.Cell);
        var jMin = Mathf.FloorToInt((z - radius - grid.OriginZ) / grid.Cell);
        var jMax = Mathf.FloorToInt((z + radius - grid.OriginZ) / grid.Cell);
        for (var j = jMin; j <= jMax; j++)
        {
            for (var i = iMin; i <= iMax; i++)
            {
                var a = new Vector2(grid.OriginX + i * grid.Cell, grid.OriginZ + j * grid.Cell);
                var b = a + new Vector2(grid.Cell, 0);
                var c = a + Vector2.one * grid.Cell;
                var d = a + new Vector2(0, grid.Cell);
                Triangle(a, b, c);
                Triangle(a, c, d);
            }
        }
        return min;

        void Candidate(Vector2 p)
        {
            min = Mathf.Min(min, Sample(grid, p.x, p.y));
        }
        void Triangle(Vector2 a, Vector2 b, Vector2 c)
        {
            var middle = (a + b + c) / 3f;
            var n = Normal(grid, middle.x, middle.y);
            var downhill = center + new Vector2(n.x, n.z).normalized * radius;
            if (Inside(downhill, a, b, c))
            {
                Candidate(downhill);
            }
            Edge(a, b); Edge(b, c); Edge(c, a);
        }
        void Edge(Vector2 a, Vector2 b)
        {
            if ((a - center).sqrMagnitude <= radius * radius)
            {
                Candidate(a);
            }
            var delta = b - a;
            var relative = a - center;
            var linear = Vector2.Dot(relative, delta);
            var determinant = linear * linear - delta.sqrMagnitude * (relative.sqrMagnitude - radius * radius);
            if (determinant < 0)
            {
                return;
            }
            var root = Mathf.Sqrt(determinant);
            foreach (var t in new[] { (-linear - root) / delta.sqrMagnitude, (-linear + root) / delta.sqrMagnitude })
            {
                if (t >= 0 && t <= 1)
                {
                    Candidate(a + t * delta);
                }
            }
        }
    }

    private static bool Inside(Vector2 p, Vector2 a, Vector2 b, Vector2 c) =>
        Cross(b - a, p - a) >= -.000001f && Cross(c - b, p - b) >= -.000001f && Cross(a - c, p - c) >= -.000001f;
    private static float Cross(Vector2 a, Vector2 b) => a.x * b.y - a.y * b.x;

    private static void Coordinates(TerrainHeightGrid grid, float x, float z, out int i, out int j, out float fx, out float fz)
    {
        var gx = Mathf.Clamp((x - grid.OriginX) / grid.Cell, 0, grid.Width - 1);
        var gz = Mathf.Clamp((z - grid.OriginZ) / grid.Cell, 0, grid.Depth - 1);
        i = Mathf.Min(Mathf.FloorToInt(gx), grid.Width - 2);
        j = Mathf.Min(Mathf.FloorToInt(gz), grid.Depth - 2);
        fx = gx - i;
        fz = gz - j;
    }
}
