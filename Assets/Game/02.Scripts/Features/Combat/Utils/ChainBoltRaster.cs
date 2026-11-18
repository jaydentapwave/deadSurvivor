using System;
using System.Collections.Generic;
using UnityEngine;

// r003 기준: xorshift32·은행가 반올림·2도트 심. 벡터 단위는 화면 텍셀이다.
public static class ChainBoltRaster
{
    public static EffectRaster Draw(int dx, int dy, uint seed, int frame)
    {
        if ((dx == 0 && dy == 0) || frame < 0 || frame > 2)
        {
            throw new ArgumentOutOfRangeException(nameof(frame));
        }
        var state = seed == 0 ? 1u : seed;
        var length = Math.Sqrt((double)dx * dx + (double)dy * dy);
        var segments = Math.Max(2, (int)(length / 10));
        var w = Math.Abs(dx) + 20;
        var h = Math.Abs(dy) + 20;
        var ox = dx >= 0 ? 10 : w - 10;
        var oy = dy >= 0 ? 10 : h - 10;
        var nx = -dy / length;
        var ny = dx / length;
        var image = new EffectRaster(w, h, new Vector2((float)ox / w, (float)oy / h));
        for (var k = 0; k <= frame; k++)
        {
            var points = new (double x, double y)[segments + 1];
            for (var i = 0; i <= segments; i++)
            {
                var t = (double)i / segments;
                var jitter = 0;
                if (i != 0 && i != segments)
                {
                    state ^= state << 13;
                    state ^= state >> 17;
                    state ^= state << 5;
                    jitter = (int)(state % 9) - 4;
                }
                points[i] = (ox + dx * t + nx * jitter, oy + dy * t + ny * jitter);
            }
            if (k != frame)
            {
                continue;
            }
            var core = new HashSet<Vector2Int>();
            for (var i = 0; i < segments; i++)
            {
                Line(core, points[i], points[i + 1], true, dx, dy);
            }
            foreach (var branch in new[] { (index: segments / 3, side: 1), (index: 2 * segments / 3, side: -1) })
            {
                var start = points[branch.index];
                var tip = (start.x + dx / length * 5 + nx * 6 * branch.side,
                    start.y + dy / length * 5 + ny * 6 * branch.side);
                Line(core, start, tip, false, dx, dy);
            }
            foreach (var endpoint in new[] { points[0], points[segments] })
            {
                var point = new Vector2Int((int)Math.Round(endpoint.x), (int)Math.Round(endpoint.y));
                core.Add(point);
                core.Add(point + Vector2Int.right);
                core.Add(point + Vector2Int.left);
                core.Add(point + Vector2Int.up);
                core.Add(point + Vector2Int.down);
            }
            foreach (var point in core)
            {
                foreach (var offset in new[] { Vector2Int.right, Vector2Int.left, Vector2Int.up, Vector2Int.down })
                {
                    if (!core.Contains(point + offset))
                    {
                        image.Put(point.x + offset.x, h - 1 - point.y - offset.y, k == 1 ? 0x7fd0c8u : 0xb07fd0u);
                    }
                }
            }
            foreach (var point in core)
            {
                image.Put(point.x, h - 1 - point.y, 0xffffff);
            }
            if (k == 2)
            {
                for (var y = 0; y < h; y++)
                {
                    for (var x = 0; x < w; x++)
                    {
                        var py = h - 1 - y;
                        var startDistance = (x - ox) * (x - ox) + (py - oy) * (py - oy);
                        var endDistance = (x - ox - dx) * (x - ox - dx) + (py - oy - dy) * (py - oy - dy);
                        if (startDistance > 9 && endDistance > 9 && (x / 3 + y / 3) % 3 == 0)
                        {
                            image.Pixels[py * w + x] = default;
                        }
                    }
                }
            }
        }
        return image;
    }

    private static void Line(HashSet<Vector2Int> core, (double x, double y) start, (double x, double y) end,
        bool isThick, int dx, int dy)
    {
        var steps = (int)Math.Max(Math.Abs(end.x - start.x), Math.Abs(end.y - start.y)) + 1;
        for (var i = 0; i <= steps; i++)
        {
            var point = new Vector2Int((int)Math.Round(start.x + (end.x - start.x) * i / steps),
                (int)Math.Round(start.y + (end.y - start.y) * i / steps));
            core.Add(point);
            if (isThick)
            {
                core.Add(point + (Math.Abs(dx) >= Math.Abs(dy) ? Vector2Int.up : Vector2Int.right));
            }
        }
    }
}
