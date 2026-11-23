using System.Collections.Generic;
using UnityEngine;

// View-only rolling hills. The judgment grid (field_height.json) stays as it is; this builds a second
// grid = judgment + hills on the same cells, used by everything that is drawn so all of it follows one surface.
public static class MeadowHillMath
{
    public const float Amplitude = .5f;
    public const float BroadWavelength = 16f;
    public const float FineWavelength = 6f;
    public const float FineWeight = .2f;
    // Below tan(18 deg) = 0.325 so every visible slope still faces the camera (a ridge's back side is squeezed,
    // not inverted) and the foot snap stays monotonic. Sprites bias their own depth (TerrainHeightTexture).
    public const float MaxSlope = .30f;
    // Step 14 r2: a slope that falls away from the camera (+z downhill) may be steeper than tan(18 deg); it turns its
    // back to the camera and the crest in front of it hides the meadow behind (real occlusion). Sprites behind the
    // crest keep their depth (TerrainHeightTexture: occlusion wins).
    public const float MaxBackSlope = .8f;
    public const float RidgeBackWidth = 1.8f;
    // Protected zones keep the judgment height: water edge + FlatRadius, bridge and tent footprints (centre + radius).
    public const float FlatRadius = 3f;
    public const float BridgeRadius = 6f;
    public const float TentRadius = 4f;
    // Step 14: one ridge across the meadow beyond the stream (z ~ RidgeZ), so the near view has a crest that hides
    // part of the meadow behind it. Gaussian cross-section; its steepest side is about 0.86 * height / width.
    public const float RidgeHeight = 1.3f;
    public const float RidgeWidth = 5.5f;
    public const float RidgeZ = 22f;
    // Width of the smooth rise from a protected zone to full hills.
    public const float BlendWidth = 4f;

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
                hillList[j * width + i] = (Hill(x, z, layout.seed) + Ridge(x, z, layout.seed)) * Mask(x, z, layout, judgment.Cell * 1.4143f);
            }
        }
        // Keep the steepest visible slope under MaxSlope (screen height collapses at tan(tilt)). Step 14: lower the
        // hills only around the steep spots first, so one steep place does not flatten the whole meadow.
        for (var pass = 0; pass < 32 && SlopeRatio(Compose(judgment, hillList, 1f)) > 1f; pass++)
        {
            LowerSteepSpots(judgment, hillList);
        }
        var scale = 1f;
        var grid = Compose(judgment, hillList, scale);
        // The scaled estimate can still overshoot where judgment and hill slopes add; shrink until it fits.
        // Millimetre rounding can also push a tiny scale over the limit; end at scale 0 (the judgment grid) if needed.
        while (SlopeRatio(grid) > 1f && scale > 0f)
        {
            scale = scale > .01f ? scale * .9f : 0f;
            grid = Compose(judgment, hillList, scale);
        }
        return grid;
    }

    // Scales the hill down by (limit / local slope) over a 5x5 neighbourhood of every too-steep vertex.
    // Protected vertices have hill 0 and stay 0 under any factor.
    private static void LowerSteepSpots(TerrainHeightGrid judgment, float[] hillList)
    {
        var width = judgment.Width;
        var depth = judgment.Depth;
        var cell = judgment.Cell;
        var factorList = new float[hillList.Length];
        for (var k = 0; k < factorList.Length; k++)
        {
            factorList[k] = 1f;
        }
        float Height(int i, int j) => judgment.Height(i, j) + hillList[j * width + i];
        for (var j = 0; j < depth - 1; j++)
        {
            for (var i = 0; i < width - 1; i++)
            {
                var lower = new Vector2(Height(i + 1, j) - Height(i, j), Height(i + 1, j + 1) - Height(i + 1, j)) / cell;
                var upper = new Vector2(Height(i + 1, j + 1) - Height(i, j + 1), Height(i, j + 1) - Height(i, j)) / cell;
                var ratio = Mathf.Max(Ratio(lower), Ratio(upper));
                if (ratio <= .98f)
                {
                    continue;
                }
                var factor = .9f / ratio;
                for (var dj = -2; dj <= 3; dj++)
                {
                    for (var di = -2; di <= 3; di++)
                    {
                        var ni = i + di;
                        var nj = j + dj;
                        if (ni >= 0 && nj >= 0 && ni < width && nj < depth)
                        {
                            factorList[nj * width + ni] = Mathf.Min(factorList[nj * width + ni], factor);
                        }
                    }
                }
            }
        }
        for (var k = 0; k < hillList.Length; k++)
        {
            hillList[k] *= factorList[k];
        }
    }

    // Smooth value noise in [-Amplitude, Amplitude]: a broad swell plus a finer roll.
    public static float Hill(float x, float z, int seed)
    {
        var broad = Noise(x / BroadWavelength, z / BroadWavelength, seed) * 2 - 1;
        var fine = Noise(x / FineWavelength, z / FineWavelength, seed + 7919) * 2 - 1;
        return Amplitude * ((1 - FineWeight) * broad + FineWeight * fine);
    }

    // Largest triangle slope as a share of its limit: side and camera-facing slopes against MaxSlope, slopes that fall
    // away from the camera against MaxBackSlope. <= 1 means the grid is within limits.
    public static float SlopeRatio(TerrainHeightGrid grid)
    {
        var ratio = 0f;
        for (var j = 0; j < grid.Depth - 1; j++)
        {
            for (var i = 0; i < grid.Width - 1; i++)
            {
                var lower = new Vector2(grid.Height(i + 1, j) - grid.Height(i, j), grid.Height(i + 1, j + 1) - grid.Height(i + 1, j)) / grid.Cell;
                var upper = new Vector2(grid.Height(i + 1, j + 1) - grid.Height(i, j + 1), grid.Height(i, j + 1) - grid.Height(i, j)) / grid.Cell;
                ratio = Mathf.Max(ratio, Mathf.Max(Ratio(lower), Ratio(upper)));
            }
        }
        return ratio;
    }

    // gradient = (dh/dx, dh/dz); negative dz is ground falling away from the camera.
    private static float Ratio(Vector2 gradient) =>
        Mathf.Max(new Vector2(gradient.x, Mathf.Max(gradient.y, 0f)).magnitude / MaxSlope, Mathf.Max(-gradient.y, 0f) / MaxBackSlope);

    // The ridge line wanders in z along x and its height varies, so it reads as land, not a wall.
    public static float Ridge(float x, float z, int seed)
    {
        var center = RidgeZ + .6f * Mathf.Sin(x / 9f + seed * .37f) + .3f * (Noise(x / 7f, 3.1f, seed + 101) * 2 - 1);
        var height = RidgeHeight * (.9f + .2f * Noise(x / 11f, 7.3f, seed + 211));
        // Camera-facing (south) side is broad; the far side falls steeply so the crest hides the meadow behind it.
        var d = (z - center) / (z < center ? RidgeWidth : RidgeBackWidth);
        return height * Mathf.Exp(-d * d);
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
            // A pond contour is closed (IsInside joins last to first), so its closing edge counts too.
            var segmentCount = water.name == "pond" && points.Length > 2 ? points.Length : points.Length - 1;
            for (var k = 0; k < segmentCount; k++)
            {
                distance = Mathf.Min(distance, SegmentDistance(point, points[k], points[(k + 1) % points.Length]) - halfWidth - FlatRadius);
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
