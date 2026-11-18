using System;
using UnityEngine;

// 픽셀 배열은 Unity의 아래→위 순서. 계산은 기준 raster.py의 화면 좌표(위→아래)로 한다.
public sealed class EffectRaster
{
    public int Width { get; }
    public int Height { get; }
    public Color32[] Pixels { get; }
    public Vector2 Pivot { get; }
    public EffectRaster(int width, int height, Vector2 pivot)
    {
        Width = width;
        Height = height;
        Pivot = pivot;
        Pixels = new Color32[width * height];
    }
    public void Put(int x, int y, uint color)
    {
        if (x >= 0 && x < Width && y >= 0 && y < Height)
        {
            Pixels[(Height - 1 - y) * Width + x] = new Color32((byte)(color >> 16), (byte)(color >> 8), (byte)color, 255);
        }
    }
    public bool HasPixel(int x, int y) => Pixels[(Height - 1 - y) * Width + x].a != 0;
}

public static class GroundMarkRaster
{
    private static readonly double[] HealSteps = { .55, .8, 1, 1, 1 };
    private static readonly double[] PulseSteps = { .3, .55, .8, 1, 1 };
    private static readonly double[] BoomSteps = { .5, .8, 1, 1 };
    private static readonly double[] BlastSteps = { 1, 1 };
    public static double Mod(double value, double period)
    {
        var result = value % period;
        return result < 0 ? result + period : result;
    }

    private static bool Inside(double a, double b, double dx, double dy) =>
        (dx / a) * (dx / a) + (dy / b) * (dy / b) <= 1;

    private static bool Band(double a, double b, double thick, double dx, double dy) =>
        Inside(a, b, dx, dy) && !Inside(Math.Max(a - thick, .01), Math.Max(b - thick, .01), dx, dy);

    private static double Angle(double a, double b, double dx, double dy) =>
        Mod(Math.Atan2(dy / b, dx / a) * (180 / Math.PI) + 360, 360);

    public static EffectRaster Ring(string kind, double radiusMeters, int frame)
    {
        if (radiusMeters <= 0 || double.IsNaN(radiusMeters) || double.IsInfinity(radiusMeters))
        {
            throw new ArgumentOutOfRangeException(nameof(radiusMeters));
        }
        double[] steps;
        uint[] colors;
        switch (kind)
        {
            case "heal": steps = HealSteps; colors = new uint[] { 0x7fa040, 0xa8c828, 0xffe08a }; break;
            case "pulse": steps = PulseSteps; colors = new uint[] { 0x7a4a9e, 0xb07fd0, 0xffffff }; break;
            case "boom": steps = BoomSteps; colors = new uint[] { 0x77211d, 0xc44334, 0xe4b67e }; break;
            case "blast": steps = BlastSteps; colors = new uint[] { 0x77211d, 0xc44334, 0xe4b67e }; break;
            default: throw new ArgumentException(kind, nameof(kind));
        }
        var a = radiusMeters * 48;
        var b = a * .5;
        var image = new EffectRaster((int)Math.Ceiling(a) * 2 + 4, (int)Math.Ceiling(b) * 2 + 4, Vector2.one * .5f);
        var aa = a * steps[frame];
        var bb = b * steps[frame];
        for (var y = 0; y < image.Height; y++)
        {
            for (var x = 0; x < image.Width; x++)
            {
                var dx = x + .5 - image.Width / 2.0;
                var dy = y + .5 - image.Height / 2.0;
                var angle = Angle(aa, bb, dx, dy);
                var keep = kind == "heal" ? Mod(angle, 90) >= (frame < 3 ? 2 : frame == 3 ? 18 : 58)
                    : kind == "pulse" ? Mod(angle, 45) >= (frame < 4 ? 3 : 22)
                    : Mod(angle, 24) >= (kind == "blast" || frame < 2 ? 5 : 12);
                for (var band = 0; band < 3; band++)
                {
                    if (keep && Band(Math.Max(.1, aa - band), Math.Max(.1, bb - band), 1, dx, dy))
                    {
                        var color = colors[band];
                        if (kind == "pulse" && frame == 3 && band == 2)
                        {
                            color = 0x7fd0c8;
                        }
                        if (kind == "boom" && frame == 3)
                        {
                            color = band == 0 ? 0x77211du : band == 1 ? 0x774c3bu : 0xc44334u;
                        }
                        image.Put(x, y, color);
                    }
                }
                if (kind == "pulse")
                {
                    var ia = Math.Max(.1, aa - 5);
                    var ib = Math.Max(.1, bb - 5);
                    if (Band(ia, ib, 2, dx, dy) && Mod(Angle(ia, ib, dx, dy), 45) >= (frame < 4 ? 7 : 25))
                    {
                        image.Put(x, y, frame < 4 ? 0x7fd0c8u : 0x7a4a9eu);
                    }
                }
                else if (kind != "heal")
                {
                    if (Band(aa, bb, 5, dx, dy) && Mod(angle + frame * 2, 24) < 5)
                    {
                        image.Put(x, y, frame % 2 == 0 ? 0xc44334u : 0x77211du);
                    }
                    var ia = Math.Max(.1, aa - 6);
                    var ib = Math.Max(.1, bb - 6);
                    if (Band(ia, ib, 1, dx, dy) && Mod(Angle(ia, ib, dx, dy), 24) < 7)
                    {
                        image.Put(x, y, frame < 3 ? 0xe4b67eu : 0x774c3bu);
                    }
                    if (kind == "blast" && Inside(aa, bb, dx, dy) && !image.HasPixel(x, y)
                        && (x + 2 * y + frame * 2) % 8 == 0 && y % 2 == 0)
                    {
                        image.Put(x, y, 0x77211d);
                    }
                }
            }
        }
        if (kind == "heal")
        {
            for (var i = 0; i < 4; i++)
            {
                if (frame == 4 && i % 2 != 0)
                {
                    continue;
                }
                var radians = (25 + i * 90 + frame * 4) * Math.PI / 180;
                var px = (int)(image.Width / 2.0 + Math.Cos(radians) * Math.Max(1, aa - 4));
                var py = (int)(image.Height / 2.0 + Math.Sin(radians) * Math.Max(1, bb - 4));
                for (var dy = -2; dy <= 2; dy++)
                {
                    for (var dx = -2; dx <= 2; dx++)
                    {
                        if (Math.Abs(dx + dy) <= 1 && Math.Abs(dx - dy) <= 3)
                        {
                            image.Put(px + dx, py + dy, dx + dy != 0 ? 0xa8c828u : 0x7fa040u);
                        }
                    }
                }
                if (frame < 4)
                {
                    image.Put(px + 4, py - 2, 0xffffff);
                    image.Put(px + 5, py - 2, 0xffffff);
                    image.Put(px + 3, py - 2, 0xffffff);
                    image.Put(px + 4, py - 1, 0xffffff);
                    image.Put(px + 4, py - 3, 0xffffff);
                }
            }
        }
        return image;
    }

    public static EffectRaster DashPath(double lengthMeters, double floorAngle, int frame)
    {
        if (lengthMeters <= 0 || double.IsNaN(lengthMeters) || double.IsInfinity(lengthMeters))
        {
            throw new ArgumentOutOfRangeException(nameof(lengthMeters));
        }
        var radians = floorAngle * Math.PI / 180;
        var sx = Math.Cos(radians) * lengthMeters * 48;
        var sy = Math.Sin(radians) * lengthMeters * 24;
        var length = Math.Sqrt(sx * sx + sy * sy);
        var ux = sx / length;
        var uy = sy / length;
        var w = (int)Math.Ceiling(Math.Abs(sx)) + 26;
        var h = (int)Math.Ceiling(Math.Abs(sy)) + 26;
        var ox = sx >= 0 ? 13 : w - 13;
        var oy = sy >= 0 ? 13 : h - 13;
        var image = new EffectRaster(w, h, new Vector2((float)ox / w, (float)oy / h));
        for (var y = 0; y < h; y++)
        {
            for (var x = 0; x < w; x++)
            {
                var px = x + .5 - ox;
                var py = h - 1 - y + .5 - oy;
                var along = px * ux + py * uy;
                var perp = -px * uy + py * ux;
                var ap = Math.Abs(perp);
                if (along >= 0 && along <= length - 9)
                {
                    if (ap >= 4 && ap <= 5 && Mod(along - frame * 3, 8) < 6)
                    {
                        image.Put(x, y, 0x77211d);
                    }
                    else if (ap < 3 && Mod(along + ap * 1.4 - frame * 3, 9) < 2.2)
                    {
                        image.Put(x, y, 0xc44334);
                    }
                }
                var limit = (length + 2 - along) * .8;
                if (along >= length - 11 && along <= length + 2 && ap <= limit)
                {
                    var edge = ap >= limit - 1.2 || along < length - 9.8;
                    image.Put(x, y, edge ? 0x77211du : frame == 1 ? 0xe4b67eu : 0xc44334u);
                }
            }
        }
        return image;
    }
}
