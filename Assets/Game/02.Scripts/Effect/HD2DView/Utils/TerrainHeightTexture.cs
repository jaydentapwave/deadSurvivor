using UnityEngine;

// Uploads the visible height grid for the sprite shader's per-sprite depth bias (HD2D_SpriteLit "SpriteDepthBias").
// SpriteBias is the same walk in C#, so the shader rule is tested without a GPU.
public static class TerrainHeightTexture
{
    // Bottom-edge samples every SampleSpacing (at most MaxSamples): a crease between two samples buries the edge
    // at most 0.30 * 0.05 = 0.015 m, which Margin covers along a ray rising >= 0.2 per metre.
    public const float SampleSpacing = .1f;
    public const int MaxSamples = 32;
    // Walk points t = WalkLimit * k / WalkSteps, k = 0..WalkSteps (about 0.08 m apart, the end point included).
    public const int WalkSteps = 19;
    public const float WalkLimit = 1.5f;
    public const float Margin = .08f;

    public static Texture2D Create(TerrainHeightGrid grid)
    {
        var texture = new Texture2D(grid.Width, grid.Depth, TextureFormat.RFloat, false, true)
        {
            name = "HD2D terrain height",
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp,
        };
        var pixelList = new Color[grid.Width * grid.Depth];
        for (var j = 0; j < grid.Depth; j++)
        {
            for (var i = 0; i < grid.Width; i++)
            {
                pixelList[j * grid.Width + i] = new Color(grid.Height(i, j), 0, 0, 0);
            }
        }
        texture.SetPixels(pixelList);
        texture.Apply(false, true);
        return texture;
    }

    // x, z origin, 1 / cell, 1 = on (0 turns the bias off for a flat grid).
    public static Vector4 Parameters(TerrainHeightGrid grid) =>
        new Vector4(grid.OriginX, grid.OriginZ, 1f / grid.Cell, grid.IsFlat ? 0f : 1f);

    // View-depth pull for one sprite: the largest distance the camera ray from a point on the bounds' bottom edge
    // needs to come out of the ground, plus Margin. Sample kinds: exposed (0), exits within WalkLimit (t), still
    // buried after WalkLimit = behind a ridge. If any sample is behind a ridge, occlusion wins: the sprite keeps its
    // depth (0). A flat grid is 0.
    public static float SpriteBias(TerrainHeightGrid grid, Vector3 boundsMin, Vector3 boundsMax, Vector3 cameraPosition)
    {
        if (grid.IsFlat)
        {
            return 0f;
        }
        var count = SampleCount(boundsMax.x - boundsMin.x);
        var bias = 0f;
        for (var k = 0; k < count; k++)
        {
            var point = new Vector3(Mathf.Lerp(boundsMin.x, boundsMax.x, k / (count - 1f)), boundsMin.y, boundsMin.z);
            var ray = (cameraPosition - point).normalized;
            var isOut = false;
            for (var step = 0; step <= WalkSteps; step++)
            {
                var t = WalkLimit * step / WalkSteps;
                var p = point + ray * t;
                if (TerrainHeightMath.Sample(grid, p.x, p.z) <= p.y)
                {
                    bias = Mathf.Max(bias, t);
                    isOut = true;
                    break;
                }
            }
            if (!isOut)
            {
                return 0f;
            }
        }
        // Margin only when some sample was buried; a sprite fully above the ground keeps its depth.
        return bias > 0f ? bias + Margin : 0f;
    }

    public static int SampleCount(float width) => Mathf.Clamp(Mathf.CeilToInt(width / SampleSpacing) + 1, 2, MaxSamples);
}
