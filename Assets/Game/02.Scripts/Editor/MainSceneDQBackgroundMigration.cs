using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

// Main screen migration runs first. This migration owns only background outputs.
public static class MainSceneDQBackgroundMigration
{
    public const string Root = "Assets/Game/03.Resources/Art/HD2D/DQMainBackground";
    public const string CatalogPath = Root + "/Catalog.asset";
    public const string Output = "output/hd2d-dq/apply/6";
    private const string Data = "Assets/Game/03.Resources/Resources/Data/Field/";
    private static readonly string[] Adopted = { "dq_tree_round_a", "dq_tree_round_c", "dq_tree_light", "dq_bush_b", "dq_rock_big_a", "dq_rock_big_b_r2", "dq_rock_small_a_r2", "dq_rock_small_b_r2" };

    [MenuItem("DeadSurvivor/DQ/Apply Main background")]
    public static void Apply() => ApplyCore(null);

    public static void ApplyForFailureTest(string checkpoint) => ApplyCore(checkpoint);

    private static void ApplyCore(string checkpoint)
    {
        if (Application.isPlaying || EditorApplication.isCompiling)
        {
            throw new InvalidOperationException("DQ background requires ready Edit mode.");
        }
        var previous = EditorSceneManager.GetActiveScene();
        if (previous.isDirty)
        {
            throw new InvalidOperationException("Save the open scene before migration.");
        }
        var previousPath = previous.path;
        var directoriesBefore = Directory.Exists(Root) ? Directory.GetDirectories(Root, "*", SearchOption.AllDirectories).Append(Root).ToHashSet() : new HashSet<string>();
        var files = Directory.Exists(Root) ? Directory.GetFiles(Root, "*", SearchOption.AllDirectories).ToList() : new List<string>();
        files.AddRange(new[] { MainSceneDQScreenMigration.ScenePath, Data + "field_height.json", Data + "field_layout.json", Root + ".meta" });
        var before = files.Distinct().ToDictionary(p => p, p => File.Exists(p) ? File.ReadAllBytes(p) : null);
        // URP may upgrade source Lit materials while a referenced prefab is loaded.
        // They are reference-only inputs, including any pre-existing disk edits.
        var sourceMaterials = Directory.GetFiles("Assets/Game/03.Resources/Art/HD2D/RoundDQ3/Materials", "*.mat")
            .ToDictionary(p => p, File.ReadAllBytes);
        var unrelatedDirty = Resources.FindObjectsOfTypeAll<Object>()
            .Where(o => AssetDatabase.Contains(o) && EditorUtility.IsDirty(o) && !AssetDatabase.GetAssetPath(o).StartsWith(Root, StringComparison.Ordinal)).ToArray();
        try
        {
            Directory.CreateDirectory(Root);
            Directory.CreateDirectory(Output);
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            PreserveSource("field_height.json");
            PreserveSource("field_layout.json");
            BakeHeightAndLayout();
            BuildTextures();
            if (checkpoint == "textures")
            {
                throw new InvalidOperationException("Injected background texture failure");
            }
            var catalog = BuildCatalog();
            var scene = EditorSceneManager.OpenScene(MainSceneDQScreenMigration.ScenePath);
            var field = Object.FindFirstObjectByType<FieldView>();
            var serialized = new SerializedObject(field);
            var previousGround = (Material)serialized.FindProperty("groundMaterial").objectReferenceValue;
            var ground = Surface("Ground", previousGround, 0, "Soil");
            var bank = Surface("Bank", (Material)serialized.FindProperty("bankMaterial").objectReferenceValue, 3, "Soil");
            // Preserve the existing walk-through decorative bank reference-depth contract.
            ground.SetFloat("_FlatDepth", 1);
            bank.SetFloat("_FlatDepth", 1);
            var wood = Surface("Wood", null, 4, "Wood");
            var woodFade = Surface("Wood_Fade", wood, 4, "Wood", .65f);
            var tent = Surface("Tent", null, 5, "Cloth");
            var tentFade = Surface("Tent_Fade", tent, 5, "Cloth", .65f);
            var bridge = Surface("Bridge", null, 5, "Wood");
            bridge.SetFloat("_FlatDepth", 1);
            var flag = Surface("Flag", null, 5, "Flag");
            var flagFade = Surface("Flag_Fade", flag, 5, "Flag", .65f);
            var water = Save(new Material(Shader.Find("DeadSurvivor/DQ/Main water")) { name = "DQ Water", renderQueue = 3000 }, "Water.mat");
            water.SetTexture("_WaterMask", AssetDatabase.LoadAssetAtPath<Texture2D>(Root + "/WaterMask.png"));
            foreach (var pair in new[] { ("groundMaterial", ground), ("bankMaterial", bank), ("waterMaterial", water),
                ("woodMaterial", wood), ("ditherWoodMaterial", woodFade), ("bridgeMaterial", bridge),
                ("tentMaterial", tent), ("ditherTentMaterial", tentFade), ("flagMaterial", flag), ("ditherFlagMaterial", flagFade) })
            {
                serialized.FindProperty(pair.Item1).objectReferenceValue = pair.Item2;
            }
            serialized.FindProperty("modelCatalog").objectReferenceValue = catalog;
            serialized.FindProperty("useModelScenery").boolValue = true;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            var heights = JsonUtility.FromJson<FieldHeightData>(File.ReadAllText(Data + "field_height.json"));
            var grid = TerrainHeightGrid.FromMillimetres(heights.cell, heights.originX, heights.originZ, heights.width, heights.depth, heights.heightsMm);
            BuildOuterBackground(ground, catalog, grid);
            // Persist only our outputs, even if the editor contains another session's dirty assets.
            foreach (var asset in Resources.FindObjectsOfTypeAll<Object>().Where(o => AssetDatabase.Contains(o)
                && EditorUtility.IsDirty(o) && !AssetDatabase.GetAssetPath(o).StartsWith(Root, StringComparison.Ordinal)))
            {
                EditorUtility.ClearDirty(asset);
            }
            foreach (var path in Directory.GetFiles(Root).Where(p => !p.EndsWith(".meta", StringComparison.Ordinal)))
            {
                var asset = AssetDatabase.LoadMainAssetAtPath(path);
                if (asset != null)
                {
                    EditorUtility.SetDirty(asset);
                    AssetDatabase.SaveAssetIfDirty(asset);
                }
            }
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            if (checkpoint == "saved")
            {
                throw new InvalidOperationException("Injected background saved failure");
            }
            EditorSceneManager.OpenScene(MainSceneDQScreenMigration.ScenePath);
            var savedField = new SerializedObject(Object.FindFirstObjectByType<FieldView>());
            if (savedField.FindProperty("modelCatalog").objectReferenceValue != catalog || GameObject.Find("DQ background render only") == null)
            {
                throw new InvalidDataException("DQ background scene references did not survive reopen.");
            }
        }
        catch
        {
            if (Directory.Exists(Root))
            {
                foreach (var file in Directory.GetFiles(Root, "*", SearchOption.AllDirectories).Where(p => !before.ContainsKey(p)))
                {
                    File.Delete(file);
                }
            }
            foreach (var pair in before)
            {
                if (pair.Value != null)
                {
                    File.WriteAllBytes(pair.Key, pair.Value);
                }
                else if (File.Exists(pair.Key))
                {
                    File.Delete(pair.Key);
                }
            }
            // Remove only directories created by this invocation, deepest first, BEFORE Refresh.
            // Otherwise AssetDatabase recreates a folder meta after a first-run failure.
            if (Directory.Exists(Root))
            {
                foreach (var directory in Directory.GetDirectories(Root, "*", SearchOption.AllDirectories).Append(Root).OrderByDescending(p => p.Length))
                {
                    if (!directoriesBefore.Contains(directory) && !Directory.EnumerateFileSystemEntries(directory).Any())
                    {
                        Directory.Delete(directory);
                        if (File.Exists(directory + ".meta") && !before.ContainsKey(directory + ".meta")) File.Delete(directory + ".meta");
                    }
                }
            }
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            foreach (var path in before.Keys.Where(File.Exists).Where(p => !p.EndsWith(".meta", StringComparison.Ordinal)))
            {
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
            }
            if (!string.IsNullOrEmpty(previousPath))
            {
                EditorSceneManager.OpenScene(previousPath);
            }
            throw;
        }
        finally
        {
            foreach (var pair in sourceMaterials)
            {
                if (!File.ReadAllBytes(pair.Key).SequenceEqual(pair.Value))
                {
                    File.WriteAllBytes(pair.Key, pair.Value);
                    AssetDatabase.ImportAsset(pair.Key, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
                }
            }
            foreach (var asset in unrelatedDirty)
            {
                if (asset != null)
                {
                    EditorUtility.SetDirty(asset);
                }
            }
        }
    }

    private static void PreserveSource(string name)
    {
        var path = Root + "/Source-" + name;
        if (!File.Exists(path))
        {
            File.Copy(Data + name, path);
        }
    }

    private static void BakeHeightAndLayout()
    {
        var height = JsonUtility.FromJson<FieldHeightData>(File.ReadAllText(Root + "/Source-field_height.json"));
        for (var i = 0; i < height.heightsMm.Length; i++)
        {
            height.heightsMm[i] = Mathf.RoundToInt(height.heightsMm[i] * .58f);
        }
        var grid = TerrainHeightGrid.FromMillimetres(height.cell, height.originX, height.originZ, height.width, height.depth, height.heightsMm);
        if (grid.ReliefWithin(32) > .273f || grid.MaxSlope > .2f || grid.MaxVertexCurvature > .08f)
        {
            throw new InvalidDataException("Shared DQ height exceeds unchanged terrain depth/slope contract.");
        }
        var heightDocument = JObject.Parse(File.ReadAllText(Root + "/Source-field_height.json"));
        heightDocument["heightsMm"] = JArray.FromObject(height.heightsMm);
        File.WriteAllText(Data + "field_height.json", heightDocument.ToString() + "\n");
        var layout = JsonUtility.FromJson<FieldLayoutData>(File.ReadAllText(Root + "/Source-field_layout.json"));
        // Patch only permitted view values on the original JSON tree. DTO round-trips drop
        // generator and any future metadata which the runtime deliberately does not read.
        var layoutDocument = JObject.Parse(File.ReadAllText(Root + "/Source-field_layout.json"));
        var placements = (JArray)layoutDocument["placements"];
        for (var i = layout.placements.Length - 1; i >= 0; i--)
        {
            var p = layout.placements[i];
            if (p.kind == "card" && (p.sprite.StartsWith("tree_", StringComparison.Ordinal) || p.sprite.StartsWith("bush_", StringComparison.Ordinal)) && p.Floor.magnitude < 12) placements.RemoveAt(i);
        }
        File.WriteAllText(Data + "field_layout.json", layoutDocument.ToString() + "\n");
        AssetDatabase.ImportAsset(Data + "field_height.json", ImportAssetOptions.ForceSynchronousImport);
        AssetDatabase.ImportAsset(Data + "field_layout.json", ImportAssetOptions.ForceSynchronousImport);
    }

    private static T Save<T>(T value, string name) where T : Object
    {
        var path = Root + "/" + name;
        var previous = AssetDatabase.LoadAssetAtPath<T>(path);
        if (previous == null)
        {
            AssetDatabase.CreateAsset(value, path);
            return value;
        }
        if (value is Mesh mesh && previous is Mesh target)
        {
            target.Clear();
            target.indexFormat = mesh.indexFormat;
            target.vertices = mesh.vertices;
            target.normals = mesh.normals;
            target.uv = mesh.uv;
            target.uv2 = mesh.uv2;
            target.triangles = mesh.triangles;
            target.RecalculateBounds();
            target.UploadMeshData(false);
        }
        else
        {
            EditorUtility.CopySerialized(value, previous);
        }
        Object.DestroyImmediate(value);
        EditorUtility.SetDirty(previous);
        return previous;
    }

    private static void BuildTextures()
    {
        foreach (var kind in new[] { "Soil", "Wood", "Cloth", "Flag", "Stone", "StonePath", "Plant", "FlowerWhite", "FlowerYellow", "FlowerPurple" })
        {
            const int size = 512;
            var image = new Texture2D(size, size, TextureFormat.RGBA32, false);
            try
            {
                var pixels = new Color[size * size];
                var color = kind == "Soil" ? new Color(.72f, .64f, .44f) : kind == "Wood" ? new Color(.48f, .30f, .13f) :
                    kind == "Cloth" ? new Color(.80f, .69f, .43f) : kind == "Flag" ? new Color(.56f, .17f, .14f) :
                    kind == "StonePath" ? new Color(.41f, .39f, .29f) : kind == "Stone" ? new Color(.41f, .49f, .42f) : kind == "Plant" ? new Color(.22f, .58f, .19f) :
                    kind == "FlowerWhite" ? new Color(.89f, .89f, .80f) : kind == "FlowerPurple" ? new Color(.56f, .28f, .68f) : new Color(.87f, .76f, .37f);
                for (var y = 0; y < size; y++)
                {
                    for (var x = 0; x < size; x++)
                    {
                        var u = x / (float)size;
                        var v = y / (float)size;
                        var noise = Mathf.PerlinNoise(u * 8 + 17, v * 8 + 31);
                        var variation = .90f + .16f * noise;
                        if (kind == "Wood")
                        {
                            variation += .045f * Mathf.Sin((u + .013f * Mathf.Sin(v * 12)) * Mathf.PI * 32);
                        }
                        pixels[y * size + x] = color * variation;
                        pixels[y * size + x].a = 1;
                    }
                }
                image.SetPixels(pixels);
                image.Apply();
                File.WriteAllBytes(Root + "/" + kind + ".png", image.EncodeToPNG());
            }
            finally
            {
                Object.DestroyImmediate(image);
            }
        }
        foreach (var kind in new[] { "Water" })
        {
            File.Copy("Assets/Game/03.Resources/Textures/HD2D/Field" + kind + "Mask.png", Root + "/" + kind + "Mask.png", true);
        }
        var layout = JsonUtility.FromJson<FieldLayoutData>(File.ReadAllText(Data + "field_layout.json"));
        foreach (var kind in new[] { "Path", "Zone" })
        {
            const int maskSize = 1024;
            var image = new Texture2D(maskSize, maskSize, TextureFormat.RGBA32, false, true);
            try
            {
                var pixels = new Color[maskSize * maskSize];
                for (var y = 0; y < maskSize; y++)
                {
                    for (var x = 0; x < maskSize; x++)
                    {
                        var point = new Vector2(-36 + (x + .5f) * 72 / maskSize, -36 + (y + .5f) * 72 / maskSize);
                        if (kind == "Path")
                        {
                            var distance = Mathf.Min(PathDistance(point, layout.path.points), PathDistance(point, layout.path.branch));
                            var width = layout.path.halfWidth + .10f * (Mathf.PerlinNoise(point.x * 1.4f + 18, point.y * 1.4f) - .5f);
                            var value = 1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(width - .12f, width + .20f, distance));
                            pixels[y * maskSize + x] = new Color(value, value, value, 1);
                        }
                        else
                        {
                            var gravel = .95f * Mathf.Exp(-((point.x + 7) * (point.x + 7) / 9 + (point.y + 4) * (point.y + 4) / 3));
                            gravel += .8f * Mathf.Exp(-((point.x + 6) * (point.x + 6) / 5 + (point.y - 6) * (point.y - 6) / 4));
                            pixels[y * maskSize + x] = new Color(0, Mathf.Clamp01(gravel), .5f, 0);
                        }
                    }
                }
                image.SetPixels(pixels);
                image.Apply();
                File.WriteAllBytes(Root + "/" + kind + "Mask.png", image.EncodeToPNG());
            }
            finally
            {
                Object.DestroyImmediate(image);
            }
        }
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        foreach (var path in Directory.GetFiles(Root, "*.png"))
        {
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Default;
            importer.filterMode = FilterMode.Trilinear;
            importer.mipmapEnabled = true;
            importer.wrapMode = path.Contains("Mask") ? TextureWrapMode.Clamp : TextureWrapMode.Repeat;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.maxTextureSize = 4096;
            importer.sRGBTexture = !path.Contains("Mask");
            importer.SaveAndReimport();
        }
    }

    private static float PathDistance(Vector2 point, Vector2[] path)
    {
        if (path == null || path.Length == 0) throw new ArgumentException("Path must contain a point.", nameof(path));
        var distance = Vector2.Distance(point, path[0]);
        for (var i = 1; i < path.Length; i++)
        {
            var delta = path[i] - path[i - 1];
            var t = delta.sqrMagnitude > 0 ? Mathf.Clamp01(Vector2.Dot(point - path[i - 1], delta) / delta.sqrMagnitude) : 0;
            distance = Mathf.Min(distance, Vector2.Distance(point, path[i - 1] + delta * t));
        }
        return distance;
    }

    private static Material Surface(string name, Material source, float category, string texture, float fade = 0)
    {
        var material = source != null ? new Material(source) : new Material(Shader.Find("DeadSurvivor/DQ/Main surface"));
        material.shader = Shader.Find("DeadSurvivor/DQ/Main surface");
        material.name = name;
        material.SetFloat("_Category", category);
        material.SetFloat("_Dither", fade);
        material.SetFloat("_WorldTexture", category == 5 ? 1 : 0);
        material.SetFloat("_TerrainLightSlope", 8);
        material.SetFloat("_FlatDepth", 0);
        material.SetColor("_AlbedoTint", Color.white);
        material.SetTexture("_MainTex", AssetDatabase.LoadAssetAtPath<Texture2D>(Root + "/" + texture + ".png"));
        material.SetTexture("_Dirt", AssetDatabase.LoadAssetAtPath<Texture2D>(Root + "/Soil.png"));
        material.SetTexture("_Stone", AssetDatabase.LoadAssetAtPath<Texture2D>(Root + "/StonePath.png"));
        material.SetTexture("_Ground", AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Game/03.Resources/Art/HD2D/RoundDQ1/Velvet.png"));
        material.SetTexture("_PathMask", AssetDatabase.LoadAssetAtPath<Texture2D>(Root + "/PathMask.png"));
        material.SetTexture("_WaterMask", AssetDatabase.LoadAssetAtPath<Texture2D>(Root + "/WaterMask.png"));
        material.SetTexture("_ZoneMask", AssetDatabase.LoadAssetAtPath<Texture2D>(Root + "/ZoneMask.png"));
        material.SetInt("_Cull", 0);
        material.DisableKeyword("_TERRAIN_HEIGHT");
        return Save(material, name + ".mat");
    }

    private static FieldModelCatalog BuildCatalog()
    {
        var source = AssetDatabase.LoadAssetAtPath<FieldModelCatalog>(HD2DSceneryMigration.CatalogPath);
        var entries = new List<FieldModelEntry>();
        foreach (var entry in source.Entries)
        {
            var index = entry.Id.StartsWith("tree_") ? int.Parse(entry.Id.Substring(6)) - 1 :
                entry.Id.StartsWith("bush_") ? 3 : entry.Id.StartsWith("boulder_") ? 4 + int.Parse(entry.Id.Substring(9)) - 1 :
                entry.Id.StartsWith("stone_") ? 6 + int.Parse(entry.Id.Substring(7)) - 1 : -1;
            var prefab = index >= 0 ? AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Game/03.Resources/Art/HD2D/RoundDQ3/Prefabs/" + Adopted[index] + ".prefab") : entry.Prefab;
            var instance = Object.Instantiate(prefab);
            try
            {
                instance.name = entry.Id;
                var texture = entry.Id.StartsWith("wall") || entry.Id.StartsWith("stone") || entry.Id.StartsWith("boulder") ? "Stone" : entry.Id == "flower_white" ? "FlowerWhite" : entry.Id == "flower_yellow" ? "FlowerYellow" : entry.Id == "flower_purple" ? "FlowerPurple" : "Plant";
                var material = Surface(entry.Id, null, 5, texture);
                if (index >= 0)
                {
                    material.SetTexture("_MainTex", prefab.GetComponentInChildren<Renderer>().sharedMaterial.GetTexture("_BaseMap"));
                    material.SetFloat("_WorldTexture", 0);
                    if (index < 4)
                    {
                        material.SetFloat("_LeafDetail", 1);
                    }
                }
                var faded = new Material(material);
                faded.name = entry.Id + "_Fade";
                faded.SetFloat("_Dither", .65f);
                faded = Save(faded, entry.Id + "_Fade.mat");
                foreach (var renderer in instance.GetComponentsInChildren<Renderer>())
                {
                    renderer.sharedMaterials = renderer.sharedMaterials.Select(_ => material).ToArray();
                    renderer.gameObject.layer = HD2DBackgroundRig.BackgroundLayer;
                }
                instance.layer = HD2DBackgroundRig.BackgroundLayer;
                if (index >= 0 && index < 4)
                {
                    AddLeafEdges(instance, material, entry.Id);
                }
                var bounds = MeasureBounds(instance);
                var saved = PrefabUtility.SaveAsPrefabAsset(instance, Root + "/" + entry.Id + ".prefab");
                entries.Add(new FieldModelEntry { Id = entry.Id, Prefab = saved, Material = material, FadedMaterial = faded, Bounds = bounds,
                    Triangles = instance.GetComponentsInChildren<MeshFilter>().Sum(f => f.sharedMesh.triangles.Length / 3) });
            }
            finally
            {
                Object.DestroyImmediate(instance);
            }
        }
        var catalog = Save(ScriptableObject.CreateInstance<FieldModelCatalog>(), "Catalog.asset");
        catalog.Entries = entries.ToArray();
        EditorUtility.SetDirty(catalog);
        return catalog;
    }

    private static void AddLeafEdges(GameObject instance, Material material, string id)
    {
        var bounds = MeasureBounds(instance);
        var vertices = new List<Vector3>();
        var uv = new List<Vector2>();
        var triangles = new List<int>();
        var random = new System.Random(83);
        for (var i = 0; i < 64; i++)
        {
            var angle = i * 2.399963f;
            var level = .42f + (float)random.NextDouble() * .48f;
            var radius = Mathf.Sin(level * Mathf.PI) * .50f;
            var center = new Vector3(Mathf.Cos(angle) * bounds.size.x * radius, bounds.min.y + bounds.size.y * level,
                Mathf.Sin(angle) * bounds.size.z * radius);
            var right = new Vector3(Mathf.Cos(angle), .18f, Mathf.Sin(angle)) * (bounds.size.y * .020f);
            var up = Vector3.up * (bounds.size.y * .009f);
            var start = vertices.Count;
            vertices.Add(center - right);
            vertices.Add(center + up);
            vertices.Add(center + right);
            vertices.Add(center - up);
            uv.AddRange(new[] { new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(.5f, .5f) });
            triangles.AddRange(new[] { start, start + 1, start + 2, start, start + 2, start + 3 });
        }
        var mesh = new Mesh { name = id + " leaf edge silhouettes" };
        mesh.SetVertices(vertices);
        mesh.SetUVs(0, uv);
        mesh.uv2 = Enumerable.Repeat(new Vector2(0, 1), vertices.Count).ToArray();
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateNormals();
        var child = new GameObject("Leaf edge silhouettes", typeof(MeshFilter), typeof(MeshRenderer));
        child.transform.SetParent(instance.transform, false);
        child.layer = HD2DBackgroundRig.BackgroundLayer;
        child.GetComponent<MeshFilter>().sharedMesh = Save(mesh, id + "-LeafEdges.asset");
        child.GetComponent<MeshRenderer>().sharedMaterial = material;
    }

    private static Bounds MeasureBounds(GameObject root)
    {
        var bounds = new Bounds();
        var hasBounds = false;
        foreach (var filter in root.GetComponentsInChildren<MeshFilter>())
        {
            foreach (var vertex in filter.sharedMesh.vertices)
            {
                var point = root.transform.InverseTransformPoint(filter.transform.TransformPoint(vertex));
                if (!hasBounds)
                {
                    bounds = new Bounds(point, Vector3.zero);
                    hasBounds = true;
                }
                bounds.Encapsulate(point);
            }
        }
        return bounds;
    }

    private static float NorthHeight(float t, float offset)
    {
        // The approved .5..3m belt stays >=1.5m above the existing terrain.
        // A varying rounded crown replaces the straight constant-height shelf;
        // the narrow front connection and broad rear connection have zero end slope.
        float Crown(float distance) => 1.5f + .35f * Mathf.Clamp01(Mathf.PerlinNoise(t * 9, 5 + .65f * Mathf.SmoothStep(0, 1, Mathf.Clamp01((distance - .5f) / 2.5f))));
        if (offset < .5f) return Crown(.5f) * Mathf.SmoothStep(0, 1, Mathf.Clamp01(offset / .5f));
        if (offset <= 3) return Crown(offset);
        var u = Mathf.Clamp01((offset - 3) / 17);
        return Crown(3) * (1 - Mathf.SmoothStep(0, 1, u));
    }

    private static void BuildOuterBackground(Material ground, FieldModelCatalog catalog, TerrainHeightGrid grid)
    {
        var root = GameObject.Find("DQ background render only");
        if (root == null)
        {
            root = new GameObject("DQ background render only");
        }
        var apron = GameObject.Find("DQ render-only outer floor");
        var generated = GroundDecalMath.Build(grid, new Vector3(0, 0, 20), new Vector2(220, 240), 0, grid.Cell, -.025f);
        var vertices = generated.vertices;
        var triangles = generated.triangles;
        generated.SetTriangles(Enumerable.Range(0, triangles.Length / 3).Where(i =>
        {
            var center = (vertices[triangles[i * 3]] + vertices[triangles[i * 3 + 1]] + vertices[triangles[i * 3 + 2]]) / 3;
            return Mathf.Abs(center.x) + Mathf.Abs(center.z) > 34.5f;
        }).SelectMany(i => new[] { triangles[i * 3], triangles[i * 3 + 1], triangles[i * 3 + 2] }).ToArray(), 0);
        generated.uv2 = vertices.Select(v => new Vector2(v.y + .025f, 0)).ToArray();
        apron.GetComponent<MeshFilter>().sharedMesh = Save(generated, "Apron.asset");
        apron.GetComponent<MeshRenderer>().sharedMaterial = ground;
        var hills = new List<Vector3>();
        var indices = new List<int>();
        const int count = 128;
        var offsets = Enumerable.Range(0, 81).Select(i => i * .25f).ToArray();
        for (var j = 0; j < offsets.Length; j++)
        {
            var offset = offsets[j];
            for (var i = 0; i <= count; i++)
            {
                var t = i / (float)count;
                var rim = t <= .5f ? Vector2.Lerp(new Vector2(-32, 0), new Vector2(0, 32), t * 2) : Vector2.Lerp(new Vector2(0, 32), new Vector2(32, 0), (t - .5f) * 2);
                var normal = t <= .5f ? new Vector2(-1, 1).normalized : new Vector2(1, 1).normalized;
                var p = rim * (1 + Mathf.Sqrt(2) * offset / 32);
                var height = NorthHeight(t, offset);
                hills.Add(new Vector3(p.x, TerrainHeightMath.Sample(grid, p.x, p.y) + height, p.y));
                if (i < count && j < offsets.Length - 1)
                {
                    var a = j * (count + 1) + i;
                    indices.AddRange(new[] { a, a + count + 1, a + count + 2, a, a + count + 2, a + 1 });
                }
            }
        }
        var hillMesh = new Mesh { name = "North render-only hills" };
        hillMesh.SetVertices(hills);
        hillMesh.SetTriangles(indices, 0);
        hillMesh.RecalculateNormals();
        hillMesh.uv2 = hills.Select(v => new Vector2(v.y, 0)).ToArray();
        var hillTransform = root.transform.Find("North spawn mask hills");
        var hill = hillTransform != null ? hillTransform.gameObject : new GameObject("North spawn mask hills", typeof(MeshFilter), typeof(MeshRenderer));
        hill.transform.SetParent(root.transform, false);
        hill.layer = HD2DBackgroundRig.BackgroundLayer;
        hill.GetComponent<MeshFilter>().sharedMesh = Save(hillMesh, "NorthHills.asset");
        var farGround = Surface("FarGround", ground, 3, "Soil");
        farGround.SetFloat("_WorldTexture", 1);
        farGround.SetFloat("_TerrainLightSlope", 1);
        farGround.SetTexture("_PathMask", Texture2D.blackTexture);
        farGround.SetTexture("_WaterMask", Texture2D.blackTexture);
        hill.GetComponent<MeshRenderer>().sharedMaterial = farGround;
        for (var i = 0; i < 72; i++)
        {
            var t = Mathf.Clamp01((i + .5f + .8f * Mathf.Sin(i * 2.39996f)) / 72);
            var rim = t < .5f ? Vector2.Lerp(new Vector2(-32, 0), new Vector2(0, 32), t * 2) : Vector2.Lerp(new Vector2(0, 32), new Vector2(32, 0), (t - .5f) * 2);
            var n = t < .5f ? new Vector2(-1, 1).normalized : new Vector2(1, 1).normalized;
            var offset = .6f + 5.5f * Mathf.PerlinNoise(i * .71f, 8);
            var p = rim * (1 + Mathf.Sqrt(2) * offset / 32);
            var entry = catalog.Find("tree_v" + (i % 3 + 1));
            var existing = root.transform.Find("Far forest " + i);
            var tree = existing != null ? existing.gameObject : (GameObject)PrefabUtility.InstantiatePrefab(entry.Prefab);
            tree.name = "Far forest " + i;
            tree.transform.SetParent(root.transform, false);
            tree.transform.position = new Vector3(p.x, TerrainHeightMath.Sample(grid, p.x, p.y) + NorthHeight(t, offset), p.y);
            var treeHeight = 1.05f + .45f * Mathf.PerlinNoise(i * .31f, 19);
            if (offset <= 3) treeHeight = Mathf.Min(treeHeight, 3 - NorthHeight(t, offset));
            tree.transform.localScale = Vector3.one * (treeHeight / entry.Bounds.size.y);
            tree.transform.rotation = Quaternion.Euler(0, i * 137, 0);
        }
        // Root positions stay outside .5m; low foliage naturally overhangs the rim.
        // This is draw-only understory, with no collider or gameplay/spawn modification.
        for (var i = 0; i < 128; i++)
        {
            var t = Mathf.Clamp01((i + .5f + .2f * Mathf.Sin(i * 2.39996f)) / 128);
            var rim = t < .5f ? Vector2.Lerp(new Vector2(-32, 0), new Vector2(0, 32), t * 2) : Vector2.Lerp(new Vector2(0, 32), new Vector2(32, 0), (t - .5f) * 2);
            var offset = .52f + .22f * Mathf.PerlinNoise(i * .41f, 27) + (Mathf.PerlinNoise(i * .13f, 9) > .55f ? 1.8f : 0);
            var p = rim * (1 + Mathf.Sqrt(2) * offset / 32);
            var entry = catalog.Find("bush_v1");
            var existing = root.transform.Find("Far forest understory " + i);
            var bush = existing != null ? existing.gameObject : (GameObject)PrefabUtility.InstantiatePrefab(entry.Prefab);
            bush.name = "Far forest understory " + i;
            bush.SetActive(Mathf.PerlinNoise(i * .13f, 9) > .4f);
            bush.transform.SetParent(root.transform, false);
            // Low front foliage overhangs the short transition, rather than being
            // perched above the crest and exposing a continuous wall underneath.
            bush.transform.position = new Vector3(p.x, TerrainHeightMath.Sample(grid,p.x,p.y) + .05f, p.y);
            bush.transform.localScale = Vector3.one * ((1.6f + .25f * Mathf.PerlinNoise(i * .31f,31)) / entry.Bounds.size.y);
            bush.transform.rotation = Quaternion.Euler(0,i*137,0);
        }
    }
}
