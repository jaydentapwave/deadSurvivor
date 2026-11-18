using System;
using UnityEngine;

public static class FieldModelMath
{
    public static uint Seed(int seed, FieldLayoutPlacement placement)
    {
        unchecked
        {
            var value = ((uint)seed ^ (uint)Mathf.RoundToInt(placement.x * 1000)) * 16777619u;
            value = (value ^ (uint)Mathf.RoundToInt(placement.y * 1000)) * 16777619u;
            foreach (var c in placement.sprite)
            {
                value = (value ^ c) * 16777619u;
            }
            return value;
        }
    }

    public static string ModelId(string sprite, uint seed)
    {
        if (sprite.StartsWith("tree_"))
        {
            return "tree_v" + (1 + seed % 3);
        }
        if (sprite.StartsWith("bush_"))
        {
            return "bush_v" + (1 + seed % 2);
        }
        if (sprite.StartsWith("boulder_"))
        {
            return "boulder_v" + (1 + seed % 2);
        }
        if (sprite.StartsWith("rock_"))
        {
            return "stone_v" + (1 + seed % 2);
        }
        return sprite switch
        {
            "grass_tuft_01" => "grass_v1",
            "grass_tuft_02" => "grass_v2",
            "flower_01" => "flower_white",
            "flower_02" => "flower_yellow",
            "flower_03" => "flower_purple",
            _ => throw new ArgumentException("Unmapped scenery sprite: " + sprite)
        };
    }

    public static float Yaw(uint seed) => seed % 36000 / 100f;
    public static float StoneScale(uint seed) => .85f + (seed >> 16) / 65535f * .3f;

    public static Vector2 ProjectedSize(Vector3[] vertices, float yaw, float tilt)
    {
        var rotation = Quaternion.Euler(0, yaw, 0);
        var radians = tilt * Mathf.Deg2Rad;
        var min = Vector2.one * float.PositiveInfinity;
        var max = Vector2.one * float.NegativeInfinity;
        foreach (var vertex in vertices)
        {
            var p = rotation * vertex;
            var screen = new Vector2(p.x, p.y * Mathf.Cos(radians) + p.z * Mathf.Sin(radians));
            min = Vector2.Min(min, screen);
            max = Vector2.Max(max, screen);
        }
        return max - min;
    }

    // Returns 1m centres on the authored run's axis. Endpoints at centre +/-0.5m touch exactly.
    public static Vector2[] WallCenters(Vector2 first, Vector2 last)
    {
        var span = Vector2.Distance(first, last);
        var count = Mathf.Max(1, Mathf.RoundToInt(span + .35f));
        var direction = span > .001f ? (last - first) / span : Vector2.right;
        var middle = (first + last) * .5f;
        var result = new Vector2[count];
        for (var i = 0; i < count; i++)
        {
            result[i] = middle + direction * (i - (count - 1) * .5f);
        }
        return result;
    }
}
