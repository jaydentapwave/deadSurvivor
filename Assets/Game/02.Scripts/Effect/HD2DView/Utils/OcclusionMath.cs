using UnityEngine;

public static class OcclusionMath
{
    public static bool Covers(Rect prop, float propDepth, Rect member, float memberDepth) =>
        propDepth > 0 && memberDepth > propDepth + .001f && prop.Overlaps(member);

    public static Rect ProjectBounds(Camera camera, Bounds bounds)
    {
        var min = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
        var max = new Vector2(float.NegativeInfinity, float.NegativeInfinity);
        for (var i = 0; i < 8; i++)
        {
            var corner = bounds.center + Vector3.Scale(bounds.extents,
                new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
            var p = camera.WorldToViewportPoint(corner);
            min = Vector2.Min(min, p);
            max = Vector2.Max(max, p);
        }
        return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
    }
}
