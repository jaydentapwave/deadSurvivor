using UnityEngine;

public static class PixelScaleMath
{
    public static int Scale(int pixelHeight, float targetFraction, int pixelsPerUnit, int minScale) =>
        Mathf.Max(minScale, Mathf.RoundToInt(pixelHeight * targetFraction / pixelsPerUnit));
    public static float OrthographicSize(int pixelHeight, int pixelsPerUnit, int scale) =>
        pixelHeight / (2f * pixelsPerUnit * scale);
}
