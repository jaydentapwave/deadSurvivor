using System.Collections.Generic;
using UnityEngine;
namespace DeadSurvivor
{
    // Local vector sketches rasterized at runtime. Prototype silhouettes, not final art.
    public static class DSIconArt
    {
        public static readonly Dictionary<string, string> Paths = new()
        {
            {"sword", "M 16 3 L 12 18 L 20 18 Z M 16 18 L 16 29 M 10 21 L 22 21"},
            {"axe", "M 15 4 L 15 29 M 15 6 L 25 4 L 27 15 L 15 13 M 15 7 L 8 10 L 15 13"},
            {"shortbow", "M 10 3 L 22 8 L 25 16 L 22 24 L 10 29 L 10 3 M 4 16 L 29 16 L 25 12 M 29 16 L 25 20"},
            {"crossbow", "M 16 3 L 16 29 M 4 12 L 16 7 L 28 12 L 4 12 M 11 24 L 21 24 M 12 7 L 16 3 L 20 7"},
            {"firestaff", "M 16 16 L 16 29 M 16 2 L 22 10 L 21 16 L 11 16 L 10 10 L 16 2 M 16 8 L 13 13 L 19 13 Z"},
            {"wand", "M 8 28 L 20 13 M 22 2 L 15 10 L 21 10 L 17 18 L 28 7 L 22 7 Z"},
            {"relic", "M 13 3 L 19 3 L 19 10 L 27 10 L 27 16 L 19 16 L 19 28 L 13 28 L 13 16 L 5 16 L 5 10 L 13 10 Z"},
            {"censer", "M 16 2 L 16 5 M 8 7 L 24 7 L 22 12 L 10 12 Z M 8 8 L 5 23 L 27 23 L 24 8 M 7 23 L 12 29 L 20 29 L 25 23 M 12 18 L 20 18"},
            {"ironshield", "M 5 5 L 27 5 L 25 21 L 16 29 L 7 21 Z M 16 8 L 16 24 M 10 14 L 22 14"},
            {"chainmail", "M 10 4 L 22 4 L 29 10 L 25 15 L 23 12 L 23 28 L 9 28 L 9 12 L 7 15 L 3 10 Z M 12 10 L 20 18 M 20 10 L 12 18 M 12 18 L 20 26 M 20 18 L 12 26"},
            {"helmet", "M 5 20 L 5 12 L 10 5 L 22 5 L 27 12 L 27 20 L 23 27 L 18 27 L 18 17 L 14 17 L 14 27 L 9 27 Z M 7 13 L 25 13"},
            {"whetstone", "M 4 21 L 11 10 L 25 7 L 29 14 L 22 25 L 8 28 Z M 4 21 L 20 17 L 29 14 M 20 17 L 22 25"},
            {"gauntlet", "M 7 28 L 6 15 L 10 12 L 10 5 L 14 5 L 14 13 L 15 3 L 19 3 L 19 13 L 20 6 L 24 6 L 24 15 L 28 12 L 28 22 L 23 28 Z M 8 23 L 24 23"},
            {"banner", "M 8 2 L 8 29 M 8 4 L 27 4 L 22 11 L 27 18 L 8 18 M 13 8 L 19 8 L 16 14 Z"},
            {"quiver", "M 9 10 L 23 10 L 22 28 L 10 28 Z M 12 10 L 9 3 M 16 10 L 16 2 M 20 10 L 24 3 M 8 4 L 11 3 M 14 3 L 18 3 M 22 3 L 26 4 M 10 20 L 22 15"},
            {"pierce", "M 16 29 L 16 3 L 10 10 M 16 3 L 22 10 M 5 16 L 12 16 M 20 16 L 27 16 M 5 22 L 12 22 M 20 22 L 27 22"},
            {"split", "M 16 29 L 16 12 L 6 3 M 16 12 L 26 3 M 2 7 L 6 3 L 10 3 M 22 3 L 26 3 L 30 7"},
            {"feather", "M 5 29 L 25 4 L 29 4 L 27 16 L 18 23 L 10 23 M 12 20 L 12 13 M 17 15 L 18 8 M 17 15 L 24 15"},
            {"scope", "M 3 13 L 23 5 L 29 19 L 9 27 Z M 8 11 L 14 25 M 20 7 L 26 21 M 15 13 L 18 19 M 13 17 L 20 14"},
            {"leather", "M 10 4 L 14 9 L 18 9 L 22 4 L 27 10 L 23 14 L 24 28 L 8 28 L 9 14 L 5 10 Z M 16 10 L 16 28 M 12 15 L 20 15 M 12 21 L 20 21"},
            {"crystal", "M 16 2 L 27 12 L 23 24 L 16 30 L 7 24 L 5 12 Z M 16 2 L 12 12 L 16 30 L 21 12 Z M 5 12 L 27 12"},
            {"spellbook", "M 3 6 L 13 4 L 16 8 L 19 4 L 29 6 L 29 28 L 19 26 L 16 29 L 13 26 L 3 28 Z M 16 8 L 16 29 M 7 11 L 12 10 M 21 12 L 25 19 L 19 19 Z"},
            {"ring", "M 10 14 L 5 20 L 7 27 L 16 30 L 25 27 L 27 20 L 22 14 M 16 2 L 23 8 L 16 16 L 9 8 Z M 10 19 L 9 24 L 16 27 L 23 24 L 22 19"},
            {"hat", "M 4 26 L 14 4 L 20 12 L 22 23 L 29 26 L 16 30 Z M 8 21 L 22 21 M 14 12 L 17 16 L 12 16 Z"},
            {"orb", "M 16 3 L 25 7 L 29 16 L 25 25 L 16 29 L 7 25 L 3 16 L 7 7 Z M 5 13 L 27 19 M 13 5 L 19 27 M 10 10 L 22 22"},
            {"robe", "M 12 3 L 20 3 L 26 10 L 22 15 L 26 29 L 6 29 L 10 15 L 6 10 Z M 12 3 L 16 11 L 20 3 M 16 11 L 16 27 M 10 21 L 22 21"},
            {"scripture", "M 6 3 L 27 3 L 27 28 L 6 28 L 4 25 L 4 6 Z M 8 4 L 8 27 M 17 9 L 17 22 M 12 14 L 22 14"},
            {"bell", "M 5 24 L 9 19 L 9 11 L 12 7 L 20 7 L 23 11 L 23 19 L 27 24 Z M 13 25 L 13 29 L 19 29 L 19 25 M 16 3 L 16 7"},
            {"rosary", "M 10 3 L 22 3 L 27 12 L 23 22 L 16 25 L 9 22 L 5 12 Z M 16 25 L 16 31 M 12 28 L 20 28 M 8 8 L 11 11 M 21 8 L 24 11 M 10 17 L 13 20 M 20 17 L 23 20"},
            {"amulet", "M 6 3 L 16 12 L 26 3 M 16 11 L 25 20 L 16 29 L 7 20 Z M 16 15 L 16 25 M 12 20 L 20 20"},
            {"vestment", "M 10 3 L 16 7 L 22 3 L 28 10 L 23 14 L 23 29 L 9 29 L 9 14 L 4 10 Z M 13 6 L 13 27 M 19 6 L 19 27 M 10 17 L 22 17"},
            {"lantern", "M 12 5 L 12 2 L 20 2 L 20 5 M 5 10 L 10 5 L 22 5 L 27 10 L 24 28 L 8 28 Z M 10 11 L 22 11 L 20 24 L 12 24 Z M 16 13 L 13 19 L 19 19 Z"}
        };
        public static Sprite Create(string id, Color color)
        {
            const int size = 64;
            if (!Paths.TryGetValue(id, out var path)) return null;
            var pixels = new Color[size * size];
            var tokens = path.Split(' '); Vector2 point = Vector2.zero, start = point;
            void Line(Vector2 from, Vector2 to)
            {
                int steps = Mathf.Max(1, Mathf.CeilToInt(Vector2.Distance(from, to) * 2));
                for (int n = 0; n <= steps; n++)
                {
                    Vector2 p = Vector2.Lerp(from, to, (float)n / steps) * 2;
                    for (int dy = -1; dy <= 1; dy++) for (int dx = -1; dx <= 1; dx++)
                    {
                        int x = Mathf.RoundToInt(p.x) + dx, y = size - 1 - Mathf.RoundToInt(p.y) + dy;
                        if (x >= 0 && x < size && y >= 0 && y < size) pixels[y * size + x] = color;
                    }
                }
            }
            for (int n = 0; n < tokens.Length;)
            {
                string command = tokens[n++];
                if (command == "Z") { Line(point, start); point = start; continue; }
                var next = new Vector2(int.Parse(tokens[n++]), int.Parse(tokens[n++]));
                if (command == "M") start = next; else Line(point, next);
                point = next;
            }
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, name = "Prototype_" + id };
            texture.SetPixels(pixels); texture.Apply();
            return Sprite.Create(texture, new Rect(0, 0, size, size), Vector2.one * .5f, size);
        }
    }
}
