// Source Assets/Game/02.Scripts/Editor/HD2D/Round8/FieldLookdevScene8.cs SHA256 2340a8d0bc890d0995a19bda2999edae7d68ca8fa730b9e3c02d2d81560b16df
using UnityEngine;

// Adapted from FieldLookdevScene8.SnapSprite; native bottom-left phase, not pivot rounding.
public static class CardSnapMath
{
    public static Vector3 Snap(Vector3 position, Vector2 pivotPixels, float pixelsPerUnit, float tiltDegrees)
    {
        var rotation = Quaternion.Euler(tiltDegrees, 0, 0);
        var up = rotation * Vector3.up;
        var corner = position + rotation * new Vector3(-pivotPixels.x / pixelsPerUnit, -pivotPixels.y / pixelsPerUnit, 0);
        var y = Vector3.Dot(corner, up);
        position.x += Mathf.Round(corner.x * pixelsPerUnit) / pixelsPerUnit - corner.x;
        position.z += (Mathf.Round(y * pixelsPerUnit) / pixelsPerUnit - y) / up.z;
        return position;
    }
}
