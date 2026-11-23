using System.Collections.Generic;
using UnityEngine;

// View-only rolling hills. The judgment grid (field_height.json) stays as it is; this builds a second
// grid = judgment + hills on the same cells, used by everything that is drawn so all of it follows one surface.
public static class MeadowHillMath
{
    public const float Amplitude = .6f;
    public const float BroadWavelength = 14f;
    public const float FineWavelength = 6f;
    public const float FineWeight = .2f;
    // Kept low so the global ActorDepthBias (it grows with the steepest slope) stays small.
    public const float MaxSlope = .12f;
    // Protected zones keep the judgment height: water edge + FlatRadius, bridge and tent footprints (centre + radius).
    public const float FlatRadius = 3f;
    public const float BridgeRadius = 6f;
    public const float TentRadius = 4f;
    // Width of the smooth rise from a protected zone to full hills.
    public const float BlendWidth = 6f;

    public static TerrainHeightGrid Build(TerrainHeightGrid judgment, FieldLayoutData layout)
    {
        var width = judgment.Width;
        var depth = judgment.Depth;
        var hillList = new float[width * depth];
        for (var j = 0; j < depth; j++)
        {
            for (var i = 0; i < width; i++)
            {
                var x = judgment.OriginX + i * judgment.Cell;
                var z = judgment.OriginZ + j * judgment.Cell;
                // A vertex within one cell diagonal of a protected zone stays flat, so every triangle that
                // touches the zone interpolates the judgment height exactly (not just its sampled corners).
                hillList[j * width + i] = Hill(x, z, layout.seed) * Mask(x, z, layout, judgment.Cell * 1.4143f);
            }
        }
        // Keep the steepest visible slope under MaxSlope (ActorDepthBias diverges near tan(tilt)).
        var scale = 1f;
        var slope = Compose(judgment, hillList, 1f).MaxSlope;
        if (slope > MaxSlope)
        {
            var judgmentSlope = judgment.MaxSlope;
            scale = Mathf.Clamp01((MaxSlope - judgmentSlope) / Mathf.Max(slope - judgmentSlope, 1e-4f));
        }
        var grid = Compose(judgment, hillList, scale);
        // The scaled estimate can still overshoot where judgment and hill slopes add; shrink until it fits.
        while (grid.MaxSlope > MaxSlope && scale > .01f)
        {
            scale *= .9f;
            grid = Compose(judgment, hillList, scale);
        }
        return grid;
    }

    // Smooth value noise in [-Amplitude, Amplitude]: a broad swell plus a finer roll.
    public static float Hill(float x, float z, int seed)
    {
        var broad = Noise(x / BroadWavelength, z / BroadWavelength, seed) * 2 - 1;
        var fine = Noise(x / FineWavelength, z / FineWavelength, seed + 7919) * 2 - 1;
        return Amplitude * ((1 - FineWeight) * broad + FineWeight * fine);
    }

    // 0 inside a protected zone grown by margin, 1 at BlendWidth beyond it, smoothstep between.
    public static float Mask(float x, float z, FieldLayoutData layout, float margin = 0f)
    {
        var t = Mathf.Clamp01((Clearance(x, z, layout) - margin) / BlendWidth);
        return t * t * (3 - 2 * t);
    }

    // Signed distance (m) from the nearest protected zone; <= 0 inside one.
    public static float Clearance(float x, float z, FieldLayoutData layout)
    {
        var point = new Vector2(x, z);
        var distance = float.PositiveInfinity;
        foreach (var water in layout.water)
        {
            var points = water.points;
            if (points == null || points.Length == 0)
            {
                continue;
            }
            if (water.name == "pond" && IsInside(point, points))
            {
                return -FlatRadius;
            }
            // Streams store a centre line and a width; distance is measured from the water edge.
            var halfWidth = Mathf.Max(0f, water.width) * .5f;
            if (points.Length == 1)
            {
                distance = Mathf.Min(distance, Vector2.Distance(point, points[0]) - halfWidth - FlatRadius);
            }
            for (var k = 0; k + 1 < points.Length; k++)
            {
                distance = Mathf.Min(distance, SegmentDistance(point, points[k], points[k + 1]) - halfWidth - FlatRadius);
            }
        }
        foreach (var placement in layout.placements)
        {
            var radius = placement.group == "bridge" ? BridgeRadius : placement.group == "tent" ? TentRadius : 0f;
            if (radius > 0f)
            {
                distance = Mathf.Min(distance, Vector2.Distance(point, placement.Floor) - radius * Mathf.Max(1f, placement.scale));
            }
        }
        return distance;
    }

    private static TerrainHeightGrid Compose(TerrainHeightGrid judgment, IReadOnlyList<float> hillList, float scale)
    {
        var heightsMm = new int[judgment.Width * judgment.Depth];
        for (var j = 0; j < judgment.Depth; j++)
        {
            for (var i = 0; i < judgment.Width; i++)
            {
                var index = j * judgment.Width + i;
                heightsMm[index] = Mathf.RoundToInt((judgment.Height(i, j) + hillList[index] * scale) * 1000f);
            }
        }
        return TerrainHeightGrid.FromMillimetres(judgment.Cell, judgment.OriginX, judgment.OriginZ, judgment.Width, judgment.Depth, heightsMm);
    }

    private static float Noise(float x, float z, int seed)
    {
        var ix = Mathf.FloorToInt(x);
        var iz = Mathf.FloorToInt(z);
        var fx = x - ix;
        var fz = z - iz;
        fx = fx * fx * (3 - 2 * fx);
        fz = fz * fz * (3 - 2 * fz);
        var a = Mathf.Lerp(Lattice(ix, iz, seed), Lattice(ix + 1, iz, seed), fx);
        var b = Mathf.Lerp(Lattice(ix, iz + 1, seed), Lattice(ix + 1, iz + 1, seed), fx);
        return Mathf.Lerp(a, b, fz);
    }

    // Integer hash to [0,1]; platform-independent (no float sin).
    private static float Lattice(int x, int z, int seed)
    {
        unchecked
        {
            var h = (uint)(x * 374761393 + z * 668265263 + seed * 1442695041);
            h = (h ^ (h >> 13)) * 1274126177u;
            h ^= h >> 16;
            return (h & 0xFFFFFF) / (float)0xFFFFFF;
        }
    }

    private static float SegmentDistance(Vector2 p, Vector2 a, Vector2 b)
    {
        var ab = b - a;
        var t = ab.sqrMagnitude > 0 ? Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude) : 0f;
        return Vector2.Distance(p, a + ab * t);
    }

    private static bool IsInside(Vector2 p, Vector2[] polygon)
    {
        var isInside = false;
        for (int i = 0, j = polygon.Length - 1; i < polygon.Length; j = i++)
        {
            if ((polygon[i].y > p.y) != (polygon[j].y > p.y)
                && p.x < (polygon[j].x - polygon[i].x) * (p.y - polygon[i].y) / (polygon[j].y - polygon[i].y) + polygon[i].x)
            {
                isInside = !isInside;
            }
        }
        return isInside;
    }
}
