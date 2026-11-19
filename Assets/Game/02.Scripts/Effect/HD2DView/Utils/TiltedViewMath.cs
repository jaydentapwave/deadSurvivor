using UnityEngine;

// Coordinates are metres; gameplay floor units are converted by the caller.
public static class TiltedViewMath
{
    public static Vector2 WorldToScreenPlane(Vector3 delta, float sinTilt) =>
        new(delta.x, delta.y * Mathf.Sqrt(1f - sinTilt * sinTilt) + delta.z * sinTilt);
    public static Vector3 SnapScreenPlane(Vector3 world, float sinTilt, float pixelsPerUnit)
    {
        if (world.y == 0f)
        {
            return SnapToTexels(world, sinTilt, pixelsPerUnit);
        }
        var screen = WorldToScreenPlane(world, sinTilt);
        return new Vector3(Mathf.Round(screen.x * pixelsPerUnit) / pixelsPerUnit, world.y,
            (Mathf.Round(screen.y * pixelsPerUnit) / pixelsPerUnit - world.y * Mathf.Sqrt(1f - sinTilt * sinTilt)) / sinTilt);
    }
    public static Vector2 FloorToScreenPlane(Vector2 floor, float sinTilt) => new(floor.x, floor.y * sinTilt);
    public static Vector2 ScreenToFloorDirection(Vector2 screen, float sinTilt) => new Vector2(screen.x, screen.y / sinTilt).normalized;
    public static Vector2 Footprint(float size, float aspect, float sinTilt) => new(size * aspect, size / sinTilt);
    public static Quaternion UprightRotation(float tiltDegrees) => Quaternion.Euler(tiltDegrees, 0f, 0f);
    public static Vector3 UprightOffset(float height, float tiltDegrees) => UprightRotation(tiltDegrees) * Vector3.up * height;
    public static Vector3 SnapToTexels(Vector3 world, float sinTilt, float pixelsPerUnit)
    {
        return new Vector3(Mathf.Round(world.x * pixelsPerUnit) / pixelsPerUnit, world.y,
            Mathf.Round(world.z * sinTilt * pixelsPerUnit) / (sinTilt * pixelsPerUnit));
    }
}
