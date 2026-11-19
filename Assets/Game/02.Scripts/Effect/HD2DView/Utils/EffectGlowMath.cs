using UnityEngine;

public static class EffectGlowMath
{
    // Mirrors HD2D_EffectGlow.Frag: rgba is the sampled linear texture * vertex colour.
    public static Color Shade(Color rgba, float coreIntensity, float bodyIntensity, out bool isVisible)
    {
        // clip(c.a - 0.5)
        isVisible = rgba.a >= .5f;
        if (!isVisible)
        {
            return Color.clear;
        }
        // LinearToSRGB(c.rgb), then max/min/saturation as in the shader.
        var srgb = rgba.gamma;
        var hi = Mathf.Max(srgb.r, srgb.g, srgb.b);
        var lo = Mathf.Min(srgb.r, srgb.g, srgb.b);
        var saturation = (hi - lo) / Mathf.Max(hi, .0001f);
        var intensity = hi >= .96f && saturation < .12f ? coreIntensity : bodyIntensity;
        return new Color(rgba.r * intensity, rgba.g * intensity, rgba.b * intensity, 1);
    }
}
