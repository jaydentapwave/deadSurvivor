using System;
using System.Collections.Generic;
using UnityEngine;

public static class FieldLayoutMath
{
    public static float L1(Vector2 p) => Mathf.Abs(p.x) + Mathf.Abs(p.y);
    public static bool IsVisible(Vector2 p) => Mathf.Max(0, Mathf.Abs(p.x) - 10f) + Mathf.Max(0, Mathf.Abs(p.y) - 11.25f) <= 10.75f;
    public static bool IsOutsideCorners(Vector2 p) => Mathf.Abs(p.x) <= 24.5f && Mathf.Abs(p.y) <= 25.33f;

    public static float PathDistance(Vector2 p, IReadOnlyList<Vector2> pathList)
    {
        var distance = float.PositiveInfinity;
        foreach (var path in pathList)
        {
            distance = Mathf.Min(distance, L1(p - path));
        }
        return distance;
    }

    public static Vector2 PushOutside(Vector2 p, IReadOnlyList<Vector2> pathList, float clearance)
    {
        var sign = p.x < 0 ? -1f : 1f;
        for (var i = 0; i < 1000 && PathDistance(p, pathList) < clearance; i++)
        {
            p.x += sign * .125f;
        }
        if (PathDistance(p, pathList) < clearance)
        {
            throw new InvalidOperationException("[FieldLayoutMath] Cannot clear path.");
        }
        return p;
    }

    // Maximum count in ANY 20 x 22.5m window, not just windows sampled at the origin.
    public static int MaxWindowCount(IReadOnlyList<Vector2> pointList) => MaxWindowCount(pointList, int.MaxValue);

    public static int MaxWindowCount(IReadOnlyList<Vector2> pointList, int stopAbove)
    {
        var maximum = 0;
        foreach (var left in pointList)
        {
            var yList = new List<float>();
            foreach (var p in pointList)
            {
                if (p.x >= left.x && p.x <= left.x + 20f)
                {
                    yList.Add(p.y);
                }
            }
            yList.Sort();
            var start = 0;
            for (var end = 0; end < yList.Count; end++)
            {
                while (yList[end] - yList[start] > 22.5f)
                {
                    start++;
                }
                maximum = Math.Max(maximum, end - start + 1);
                if (maximum > stopAbove)
                {
                    return maximum;
                }
            }
        }
        return maximum;
    }
}
