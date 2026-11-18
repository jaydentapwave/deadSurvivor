using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

// F8 shore extraction adapted from Editor/HD2D/Round8/FieldLookdevScene8.cs.
// Original SHA256 2340a8d0bc890d0995a19bda2999edae7d68ca8fa730b9e3c02d2d81560b16df
public static class FieldLayoutBaker
{
    private const string DataPath = "Assets/Game/03.Resources/Resources/Data/Field/field_layout.json";
    private static readonly Vector2[] Stream = { new(3, 24.5f), new(9.5f, 19), new(13.5f, 14.5f), new(23.5f, 5.5f), new(24, 4.7f) };

    [MenuItem("DeadSurvivor/HD2D/Bake Field Layout")]
    public static void Bake()
    {
        var layout = Generate();
        File.WriteAllText(DataPath, JsonUtility.ToJson(layout, true) + "\n");
        const string folder = "Assets/Game/03.Resources/Meshes/HD2D";
        Directory.CreateDirectory(folder);
        const string source = "Assets/Game/03.Resources/Art/HD2D/OB-006_Water.asset";
        var destination = folder + "/FieldPond.asset";
        if (!File.Exists(destination))
        {
            AssetDatabase.CopyAsset(source, destination);
        }
        var sourceList = new[] { source, "Assets/Game/02.Scripts/Editor/HD2D/Round8/FieldLookdevScene8.cs" };
        File.WriteAllText(folder + "/source.txt", string.Join("\n", sourceList.Select(p => p + " SHA256 " + BitConverter.ToString(System.Security.Cryptography.SHA256.Create().ComputeHash(File.ReadAllBytes(p))).Replace("-", "").ToLowerInvariant())));
        AssetDatabase.Refresh();
    }

    public static FieldLayoutData Generate()
    {
        var pathData = JsonUtility.FromJson<GroundPathData>(File.ReadAllText("Assets/Game/03.Resources/Resources/Data/Field/ground_path.json"));
        var pathList = pathData.Cells.Select(c => IsoProjection.IsoCellToFloor(new Vector3Int(c.X, c.Y, 0))).ToArray();
        var random = new System.Random(181108);
        var placementList = new List<FieldLayoutPlacement>();
        var densityDict = new Dictionary<string, List<Vector2>>();
        bool CanAdd(string group, Vector2 p, int cap)
        {
            if (!densityDict.TryGetValue(group, out var pointList))
            {
                pointList = new List<Vector2>();
                densityDict.Add(group, pointList);
            }
            pointList.Add(p);
            if (FieldLayoutMath.MaxWindowCount(pointList, cap) > cap)
            {
                pointList.RemoveAt(pointList.Count - 1);
                return false;
            }
            return true;
        }
        bool AddCard(string sprite, Vector2 p, string group, bool shadow = false, bool push = true)
        {
            if (push)
            {
                p = FieldLayoutMath.PushOutside(p, pathList, 3f);
            }
            var cap = group == "tree" ? 6 : group == "boulder" || group == "stone" ? 4 : group == "bush" ? 10 : group == "flowers" ? 150 : int.MaxValue;
            if (!FieldLayoutMath.IsOutsideCorners(p) || FieldLayoutMath.L1(p) <= 3.5f || (sprite == "flower_02" && FieldLayoutMath.L1(p) <= 6f))
            {
                return false;
            }
            if (cap < int.MaxValue && !CanAdd(group, p, cap))
            {
                return false;
            }
            placementList.Add(new FieldLayoutPlacement { sprite = sprite, x = p.x, y = p.y, group = group, isFlipped = random.Next(2) == 0, isCastingShadow = shadow, height = p.x < -3.5f && p.x > -5.5f && p.y >= 6 && p.y <= 8.5f ? .36f : 0 });
            return true;
        }
        void AddProp(string prop, float x, float y, float yaw = 0, bool roadside = false, float scale = 1)
        {
            var p = prop == "bridge" ? new Vector2(x, y) : FieldLayoutMath.PushOutside(new Vector2(x, y), pathList, roadside ? 1.2f : 3f);
            placementList.Add(new FieldLayoutPlacement { kind = "prop", prop = prop, group = prop, x = p.x, y = p.y, yaw = yaw, scale = scale, isRoadside = roadside, isCastingShadow = prop == "tent" || prop == "bridge" });
        }
        foreach (var side in new[] { -1, 1 })
        {
            for (var i = 0; i < 7; i++)
            {
                var p = new Vector2(side * (i % 2 == 0 ? 4.5f : 6.1f), -4f + 1.8f * i);
                var group = i % 3 == 0 ? "tree" : i % 3 == 1 ? "boulder" : "stone";
                AddCard(group == "tree" ? "tree_02" : group == "boulder" ? "boulder_01" : "rock_01", p, group, group != "stone");
            }
            for (var i = 0; i < 5; i++)
            {
                AddCard(i % 2 == 0 ? "bush_01" : "bush_02", new Vector2(side * (4.4f + .6f * (i % 2)), side < 0 ? -5 + 1.1f * i : -3 + 2.1f * i), "bush", true);
            }
            AddProp("fence", side * 8.2f, .3f);
        }
        var targets = new[] { ("tree", "tree_02", 20), ("boulder", "boulder_01", 7), ("stone", "rock_02", 20), ("bush", "bush_02", 34) };
        foreach (var target in targets)
        {
            for (var attempt = 0; placementList.Count(p => p.group == target.Item1) < target.Item3 && attempt < 10000; attempt++)
            {
                var p = new Vector2((float)(random.NextDouble() * 48 - 24), (float)(random.NextDouble() * 50 - 25));
                if (Mathf.Abs(p.x) < 9 && Mathf.Abs(p.y) < 9 || FieldLayoutMath.L1(p) > 32 || (target.Item1 != "stone" && !FieldLayoutMath.IsVisible(p)))
                {
                    continue;
                }
                AddCard(target.Item2, p, target.Item1, target.Item1 != "stone");
            }
        }
        for (var i = 0; i < 12; i++)
        {
            var x = (i < 6 ? -1 : 1) * (10.5f + i % 6 * 1.8f);
            AddCard("tree_02", new Vector2(x, 33f - Mathf.Abs(x)), "rim", true, false);
        }
        var clusterList = new List<Vector2> { new(-7, -7), new(-2, -5.8f), new(3, -7.5f), new(7, -4.8f), new(-7, 1), new(2, 5.5f), new(7, 4.3f) };
        for (var i = 0; i < 21; i++)
        {
            var angle = i * Mathf.PI * 2 / 21;
            clusterList.Add(new Vector2(Mathf.Cos(angle) * (i % 2 == 0 ? 18 : 13), Mathf.Sin(angle) * (i % 2 == 0 ? 20 : 15)));
        }
        for (var i = 0; i < 3000 && placementList.Count(p => p.group == "flowers") < 450; i++)
        {
            var center = clusterList[i % clusterList.Count];
            var angle = (float)random.NextDouble() * Mathf.PI * 2;
            var radius = Mathf.Sqrt((float)random.NextDouble()) * 1.3f;
            var p = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
            if (!FieldLayoutMath.IsVisible(p))
            {
                continue;
            }
            var sprite = i % 9 == 0 ? "flower_01" : i % 9 == 1 ? "flower_02" : i % 9 == 2 ? "flower_03" : i % 2 == 0 ? "grass_tuft_01" : "grass_tuft_02";
            AddCard(sprite, p, "flowers");
        }
        // Original shore connected components, mixed at approximately 4:6; small components have 20% gaps.
        foreach (var bounds in ShoreComponents())
        {
            if (bounds.size.x < .15f && random.Next(5) == 0)
            {
                continue;
            }
            var p = new Vector2(bounds.center.x, bounds.center.z) + new Vector2(random.Next(-1, 2), random.Next(-1, 2)) / 48f;
            AddCard(random.Next(10) < 4 ? "rock_01" : "rock_02", p, "shore", false, false);
        }
        for (var i = 0; i < 50; i++)
        {
            var segment = i / 13;
            var p = Vector2.Lerp(Stream[segment], Stream[segment + 1], (i % 13) / 13f) + Vector2.right * (i % 2 == 0 ? -.8f : .8f);
            if (FieldLayoutMath.PathDistance(p, pathList) >= 3f)
            {
                AddCard(i % 5 < 2 ? "rock_01" : "rock_02", p, "shore", false, false);
            }
        }
        for (var i = 0; i < 10; i++)
        {
            var p = i < 6 ? Vector2.Lerp(new(-17, 12), new(-9, 20.5f), i / 5f) : Vector2.Lerp(new(17, -8), new(19.8f, -4), (i - 6) / 3f);
            for (var j = 0; j < 4; j++)
            {
                AddCard(j % 2 == 0 ? "rock_01" : "rock_02", p + new Vector2(j * .26f, j * .08f), "wall_" + i, false, false);
            }
        }
        for (var i = 0; i < 10; i++)
        {
            AddProp("fence", -15 + (i / 2) * 1.2f + (i % 2 == 0 ? -1.2f : 1.2f), -10.5f + (i / 2) * 1.1f, 45, true);
        }
        AddProp("sign", -14.5f, -9.5f, 45, true);
        AddProp("sign", 13.5f, 10.5f, 45, true);
        AddProp("bridge", 16.1f, 12.1f, 45);
        AddProp("tent", -16, 6, 160);
        AddProp("tent", -14, 8.5f, 200, false, .95f);
        AddProp("flag", -17.5f, 8);
        AddProp("flag", -15, 4.5f);
        AddProp("flag", -13, 10);
        return new FieldLayoutData
        {
            placements = placementList.ToArray(),
            banks = new[]
            {
                new FieldBankLayout { name = "pond-bank", seed = 181108, rectangles = new[] { new Rect(-6.75f, 8.25f, 5.625f, 1), new Rect(-5.5f, 6, 2, 2.5f) } },
                new FieldBankLayout { name = "stream-bank", seed = 181109, rectangles = new[] { new Rect(8.5f, 19, 1.25f, 1.5f) } }
            },
            water = new[]
            {
                new FieldWaterLayout { name = "pond", mesh = "FieldPond", points = Array.Empty<Vector2>(), height = -.015f },
                new FieldWaterLayout { name = "stream", points = Stream, width = 1.3f, height = -.03f },
                new FieldWaterLayout { name = "waterfall", points = new[] { new Vector2(9.25f, 19), new Vector2(9.25f, 18.8f) }, width = .8f, height = .36f }
            }
        };
    }

    private static IEnumerable<Bounds> ShoreComponents()
    {
        var mesh = AssetDatabase.LoadAssetAtPath<Mesh>("Assets/Game/03.Resources/Art/HD2D/OB-006_Stones.asset");
        var vertices = mesh.vertices;
        var parent = Enumerable.Range(0, vertices.Length).ToArray();
        int Find(int i)
        {
            while (parent[i] != i)
            {
                parent[i] = parent[parent[i]];
                i = parent[i];
            }
            return i;
        }
        void Join(int a, int b) => parent[Find(a)] = Find(b);
        var weldedDict = new Dictionary<Vector3Int, int>();
        for (var i = 0; i < vertices.Length; i++)
        {
            var key = Vector3Int.RoundToInt(vertices[i] * 10000);
            if (weldedDict.TryGetValue(key, out var other))
            {
                Join(i, other);
            }
            else
            {
                weldedDict.Add(key, i);
            }
        }
        var triangles = mesh.triangles;
        for (var i = 0; i < triangles.Length; i += 3)
        {
            Join(triangles[i], triangles[i + 1]);
            Join(triangles[i], triangles[i + 2]);
        }
        foreach (var group in Enumerable.Range(0, vertices.Length).GroupBy(Find).OrderBy(g => g.Key))
        {
            var bounds = new Bounds(vertices[group.First()], Vector3.zero);
            foreach (var i in group)
            {
                bounds.Encapsulate(vertices[i]);
            }
            if (bounds.size.y >= .03f && bounds.size.x >= .03f)
            {
                yield return bounds;
            }
        }
    }
}
