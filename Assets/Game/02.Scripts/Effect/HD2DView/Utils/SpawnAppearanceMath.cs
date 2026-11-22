using UnityEngine;

public static class SpawnAppearanceMath
{
    public const float Seconds = .30f;
    public static float Fraction(float elapsed) => Mathf.Clamp01(elapsed / Seconds);
    // Camera yaw is fixed at zero: +Z north, +X east. Ties go north/south.
    public static int Direction(Vector2 relative)
    {
        if (Mathf.Abs(relative.y) >= Mathf.Abs(relative.x))
        {
            return relative.y >= 0 ? 0 : 3;
        }
        return relative.x >= 0 ? 1 : 2;
    }
}
