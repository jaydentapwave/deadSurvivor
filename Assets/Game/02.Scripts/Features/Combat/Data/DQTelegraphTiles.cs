using System;
using UnityEngine;

// Four assembled r2 tiles, baked by the finish migration. Pixel order is Unity bottom-up.
[PreferBinarySerialization]
public sealed class DQTelegraphTiles : ScriptableObject
{
    [Serializable]
    public sealed class Frame
    {
        public Color32[] pixels;
    }

    public Frame[] frames;
    public const int Width = 34;
    public const int Height = 209;
    public const string ResourcePath = "Art/DQ/BoarTelegraph";

    public static int StageAt(float elapsed, float windupSeconds)
    {
        if (!(windupSeconds > 0) || float.IsInfinity(windupSeconds) || float.IsNaN(elapsed) || float.IsInfinity(elapsed))
        {
            throw new ArgumentOutOfRangeException(nameof(windupSeconds));
        }
        // Scale the authored float boundaries, preserving adjacent elapsed values.
        double scale = (double)windupSeconds / .8f;
        return elapsed < (float)(.26f * scale) ? 0
            : elapsed < (float)(.50f * scale) ? 1
            : elapsed < (float)(.70f * scale) ? 2 : 3;
    }

    public EffectRaster Raster(double lengthMetres, double radiusMetres, double floorAngle, int stage)
    {
        if (!(lengthMetres > 0 && radiusMetres > 0) || double.IsInfinity(lengthMetres) || double.IsInfinity(radiusMetres)
            || double.IsNaN(floorAngle) || double.IsInfinity(floorAngle)
            || stage < 0 || stage > 3 || frames == null || frames.Length != 4 || frames[stage].pixels.Length != Width * Height)
        {
            throw new ArgumentException("DQ telegraph requires finite positive length/radius and four complete frames.");
        }
        var angle = floorAngle * Math.PI / 180;
        var cosine = Math.Cos(angle);
        var sine = Math.Sin(angle);
        var length = lengthMetres * 48;
        var radius = radiusMetres * 48;
        var endX = cosine * length;
        var endY = sine * length * .5;
        var marginX = (int)Math.Ceiling(radius) + 2;
        var marginY = (int)Math.Ceiling(radius * .5) + 2;
        var width = (int)Math.Ceiling(Math.Abs(endX)) + marginX * 2;
        var height = (int)Math.Ceiling(Math.Abs(endY)) + marginY * 2;
        var originX = marginX - Math.Min(0, endX);
        var originY = marginY - Math.Min(0, endY);
        var image = new EffectRaster(width, height, new Vector2((float)(originX / width), (float)(originY / height)));
        var pixels = frames[stage].pixels;
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var dx = x + .5 - originX;
                var dy = (height - y - .5 - originY) * 2;
                var along = dx * cosine + dy * sine;
                var perpendicular = -dx * sine + dy * cosine;
                var nearest = Math.Max(0, Math.Min(length, along));
                if ((along - nearest) * (along - nearest) + perpendicular * perpendicular > radius * radius)
                {
                    continue;
                }
                var sourceX = (int)Math.Floor(17 + perpendicular / radius * 15);
                var sourceY = (int)Math.Floor(along < 0 ? 192 - along / radius * 15
                    : along > length ? 17 - (along - length) / radius * 15 : 192 - along / length * 175);
                if (sourceX < 0 || sourceX >= Width || sourceY < 0 || sourceY >= Height)
                {
                    continue;
                }
                var color = pixels[(Height - 1 - sourceY) * Width + sourceX];
                if (color.a >= 128)
                {
                    color.a = 255;
                    image.Pixels[(height - 1 - y) * width + x] = color;
                }
            }
        }
        return image;
    }
}
