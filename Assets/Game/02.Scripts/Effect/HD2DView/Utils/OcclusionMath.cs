using UnityEngine;

public static class OcclusionMath
{
    public static bool Covers(Rect prop, float propDepth, Rect member, float memberDepth) =>
        propDepth > 0 && memberDepth > propDepth + .001f && prop.Overlaps(member);

    public static Rect ProjectBounds(Camera camera, Bounds bounds)
    {
        var min = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
        var max = new Vector2(float.NegativeInfinity, float.NegativeInfinity);
        var crossesNear = false;
        for (var i = 0; i < 8; i++)
        {
            var corner = Corner(bounds, i);
            var p = camera.WorldToViewportPoint(corner);
            if (p.z >= camera.nearClipPlane)
            {
                min = Vector2.Min(min, p);
                max = Vector2.Max(max, p);
            }
            else
            {
                crossesNear = true;
            }
        }
        if (float.IsPositiveInfinity(min.x))
        {
            return new Rect(0, 0, 0, 0);
        }
        // Clipped edge endpoints can project beyond every surviving original corner.
        if (crossesNear)
        {
            for (var i = 0; i < 8; i++)
            {
                for (var bit = 1; bit <= 4; bit <<= 1)
                {
                    if ((i & bit) != 0)
                    {
                        continue;
                    }
                    var a = Corner(bounds, i);
                    var b = Corner(bounds, i | bit);
                    var az = camera.transform.InverseTransformPoint(a).z;
                    var bz = camera.transform.InverseTransformPoint(b).z;
                    var near = camera.nearClipPlane;
                    if ((az < near) == (bz < near))
                    {
                        continue;
                    }
                    var point = camera.WorldToViewportPoint(Vector3.LerpUnclamped(a, b, (near - az) / (bz - az)));
                    min = Vector2.Min(min, point);
                    max = Vector2.Max(max, point);
                }
            }
        }
        return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
    }

    private static Vector3 Corner(Bounds bounds, int i) => bounds.center + Vector3.Scale(bounds.extents,
        new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
}
