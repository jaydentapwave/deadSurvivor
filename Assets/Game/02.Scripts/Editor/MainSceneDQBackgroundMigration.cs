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
    // Painted tileable textures (Codex image generation, colour matched by Tools/dq_painted_textures.py).
    private const string Painted = "Assets/Game/03.Resources/Art/HD2D/DQPainted/";
    private static readonly string[] PaintedKinds = { "Soil", "Wood", "Cloth", "Flag", "StonePath", "Plant", "Grass", "Leaves", "Stone", "FlowerWhite", "FlowerYellow", "FlowerPurple" };
    // Step 13: trees and bush take the step-12 r2 models (DQEnvironmentR2Import); rocks are unchanged.
    private static readonly string[] Adopted = { "dq_tree_round_a_r2", "dq_tree_round_c_r2", "dq_tree_light_r2", "dq_bush_b_r2", "dq_rock_big_a", "dq_rock_big_b_r2", "dq_rock_small_a_r2", "dq_rock_small_b_r2" };

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
            // Step 11: view-only hills give the floor real slopes (up to .12); the 8x light exaggeration meant for
            // centimetre banks turns them into near-black bands, so the meadow floor alone uses a milder factor.
            ground.SetFloat("_TerrainLightSlope", 3);
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
            serialized.FindProperty("flagModel").objectReferenceValue = BuildFlagModel(wood, flag);
            serialized.FindProperty("modelCatalog").objectReferenceValue = catalog;
            serialized.FindProperty("useModelScenery").boolValue = true;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            var heights = JsonUtility.FromJson<FieldHeightData>(File.ReadAllText(Data + "field_height.json"));
            var grid = TerrainHeightGrid.FromMillimetres(heights.cell, heights.originX, heights.originZ, heights.width, heights.depth, heights.heightsMm);
            // Step 11: the render-only outer floor and north hills follow the same drawn grid as the runtime floor
            // (judgment + MeadowHillMath hills, built from the shipped layout the game loads).
            var layout = JsonUtility.FromJson<FieldLayoutData>(File.ReadAllText(Data + "field_layout.json"));
            BuildOuterBackground(ground, catalog, MeadowHillMath.Build(grid, layout));
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
        foreach (var kind in PaintedKinds)
        {
            File.Copy(Painted + kind + ".png", Root + "/" + kind + ".png", true);
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
            // Keep leaf clusters crisp at canopy distance instead of averaging them to a flat colour.
            importer.mipMapBias = path.EndsWith("/Leaves.png", StringComparison.Ordinal) ? -1.5f : 0;
            importer.sRGBTexture = !path.Contains("Mask") && !path.EndsWith("/Grass.png", StringComparison.Ordinal) && !path.EndsWith("/Leaves.png", StringComparison.Ordinal);
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
        material.SetTexture("_GrassAlbedo", AssetDatabase.LoadAssetAtPath<Texture2D>(Root + "/Grass.png"));
        material.SetFloat("_GrassTile", 4);
        material.SetFloat("_GrassWeight", 1);
        material.SetFloat("_GrassContrast", 5.5f);
        material.SetFloat("_PathEdge", 1);
        material.SetVector("_MeadowSlope", new Vector4(.12f, .04f, 0, 0));
        material.SetVector("_MeadowLight", new Vector4(.09f, 1f, .15f, .2f));
        material.SetVector("_ShadeColor", new Vector4(.03f, .045f, .035f, 1));
        material.SetTexture("_Leaves", AssetDatabase.LoadAssetAtPath<Texture2D>(Root + "/Leaves.png"));
        material.SetVector("_LeafPaint", new Vector4(.55f, .55f, .55f, 1.2f));
        ConfigureFade(material, fade);
        material.DisableKeyword("_TERRAIN_HEIGHT");
        return Save(material, name + ".mat");
    }

    // Occluding scenery fades as soft alpha over actors (drawn earlier in AlphaTest) instead of a dot screen.
    private static void ConfigureFade(Material material, float fade)
    {
        material.SetFloat("_Dither", fade);
        var isSoft = fade > 0;
        material.SetFloat("_SoftFade", isSoft ? 1 : 0);
        material.SetFloat("_SrcBlend", isSoft ? (float)BlendMode.SrcAlpha : (float)BlendMode.One);
        material.SetFloat("_DstBlend", isSoft ? (float)BlendMode.OneMinusSrcAlpha : (float)BlendMode.Zero);
        material.SetFloat("_ZWrite", 1);
        material.renderQueue = isSoft ? (int)RenderQueue.Transparent : -1;
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
            // Flowers: the step-12 white flower mesh; yellow/purple are the same mesh with recoloured petals.
            var adoptedId = index >= 0 ? Adopted[index] : entry.Id.StartsWith("flower_", StringComparison.Ordinal) ? entry.Id : null;
            var prefab = adoptedId != null ? AssetDatabase.LoadAssetAtPath<GameObject>(DQEnvironmentR2Import.Art + "/Prefabs/" + adoptedId + ".prefab") : entry.Prefab;
            var instance = Object.Instantiate(prefab);
            try
            {
                instance.name = entry.Id;
                var texture = entry.Id.StartsWith("wall") || entry.Id.StartsWith("stone") || entry.Id.StartsWith("boulder") ? "Stone" : entry.Id == "flower_white" ? "FlowerWhite" : entry.Id == "flower_yellow" ? "FlowerYellow" : entry.Id == "flower_purple" ? "FlowerPurple" : "Plant";
                var material = Surface(entry.Id, null, 5, texture);
                if (adoptedId != null)
                {
                    material.SetTexture("_MainTex", prefab.GetComponentInChildren<Renderer>().sharedMaterial.GetTexture("_BaseMap"));
                    material.SetFloat("_WorldTexture", 0);
                    if (index >= 0 && index < 4)
                    {
                        material.SetFloat("_LeafDetail", 1);
                    }
                }
                var faded = new Material(material);
                faded.name = entry.Id + "_Fade";
                ConfigureFade(faded, .65f);
                faded = Save(faded, entry.Id + "_Fade.mat");
                foreach (var renderer in instance.GetComponentsInChildren<Renderer>())
                {
                    renderer.sharedMaterials = renderer.sharedMaterials.Select(_ => material).ToArray();
                    renderer.gameObject.layer = HD2DBackgroundRig.BackgroundLayer;
                }
                instance.layer = HD2DBackgroundRig.BackgroundLayer;
                // Bounds before the leaf cards, so placement sizes stay those of the step-13 body.
                var bounds = MeasureBounds(instance);
                Material leafMaterial = null;
                Material fadedLeafMaterial = null;
                if (adoptedId != null && index >= 0 && index < 4)
                {
                    leafMaterial = LeafClusterMaterial(entry.Id);
                    fadedLeafMaterial = new Material(leafMaterial) { name = entry.Id + "_Leaves_Fade" };
                    ConfigureFade(fadedLeafMaterial, .65f);
                    fadedLeafMaterial = Save(fadedLeafMaterial, entry.Id + "_Leaves_Fade.mat");
                    AddLeafClusters(instance, adoptedId, leafMaterial, entry.Id, index == 3 ? BushLeafCards : TreeLeafCards, index == 3 ? BushLeafCardSize : LeafCardSize);
                }
                var saved = PrefabUtility.SaveAsPrefabAsset(instance, Root + "/" + entry.Id + ".prefab");
                entries.Add(new FieldModelEntry { Id = entry.Id, Prefab = saved, Material = material, FadedMaterial = faded,
                    LeafMaterial = leafMaterial, FadedLeafMaterial = fadedLeafMaterial, Bounds = bounds,
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

    // Step 14 (K41): leaf-cluster alpha cards laid over the crown surface; the step-13 body stays as the inner mass
    // and shows through the gaps as the dark between clumps. Cards light with the crown normal (lit top, dark underside).
    private const string LeafClusterTexture = Root + "/LeafClusters.png";
    private const int TreeLeafCards = 320;
    private const int BushLeafCards = 160;
    // Bushes are scaled up to 1.6 m in the far understory, so their clumps are a smaller share of the crown.
    private const float BushLeafCardSize = .13f;
    // Card width as a share of the crown width; large clumps (step-10 lesson: small detail reads as noise).
    private const float LeafCardSize = .22f;

    private static Material LeafClusterMaterial(string id)
    {
        var material = Surface(id + "_Leaves", null, 5, "Plant");
        material.SetTexture("_MainTex", AssetDatabase.LoadAssetAtPath<Texture2D>(LeafClusterTexture));
        material.SetFloat("_WorldTexture", 0);
        material.SetFloat("_AlphaClip", 1);
        material.SetFloat("_LeafDetail", 0);
        // Toward the crown body's teal green (기준서 3.5 숲 덩어리 #045737 / 잎 #58b847) so clumps sit on the body.
        material.SetColor("_AlbedoTint", new Color(.62f, .80f, .70f, 1));
        EditorUtility.SetDirty(material);
        return material;
    }

    private static void AddLeafClusters(GameObject instance, string adoptedId, Material leafMaterial, string id, int cardCount, float cardSize)
    {
        var body = instance.GetComponentInChildren<MeshFilter>();
        var toRoot = instance.transform.worldToLocalMatrix * body.transform.localToWorldMatrix;
        var bodyMesh = body.sharedMesh;
        var positions = bodyMesh.vertices.Select(v => toRoot.MultiplyPoint3x4(v)).ToArray();
        var uvs = bodyMesh.uv;
        var triangles = bodyMesh.triangles;
        var albedo = new Texture2D(2, 2);
        albedo.LoadImage(File.ReadAllBytes(DQEnvironmentR2Import.Art + "/Converted/" + adoptedId + "/basecolor.png"));
        // Crown = triangles whose albedo is leaf (green over red); trunk and roots stay bare.
        var leafTriangleList = new List<int>();
        var areaList = new List<float>();
        var total = 0f;
        var crownMin = Vector3.positiveInfinity;
        var crownMax = Vector3.negativeInfinity;
        for (var t = 0; t < triangles.Length; t += 3)
        {
            var uv = (uvs[triangles[t]] + uvs[triangles[t + 1]] + uvs[triangles[t + 2]]) / 3f;
            var color = albedo.GetPixelBilinear(uv.x, uv.y);
            if (color.g <= color.r * 1.1f)
            {
                continue;
            }
            var a = positions[triangles[t]];
            var b = positions[triangles[t + 1]];
            var c = positions[triangles[t + 2]];
            var area = Vector3.Cross(b - a, c - a).magnitude * .5f;
            // A zero-area triangle would tie in the cumulative list and could be picked; it carries no surface.
            if (area <= 1e-8f)
            {
                continue;
            }
            total += area;
            leafTriangleList.Add(t);
            areaList.Add(total);
            crownMin = Vector3.Min(crownMin, Vector3.Min(a, Vector3.Min(b, c)));
            crownMax = Vector3.Max(crownMax, Vector3.Max(a, Vector3.Max(b, c)));
        }
        Object.DestroyImmediate(albedo);
        if (leafTriangleList.Count == 0)
        {
            throw new InvalidDataException("[MainSceneDQBackgroundMigration] No leaf triangles on " + adoptedId);
        }
        var center = (crownMin + crownMax) * .5f;
        var crownWidth = Mathf.Max(crownMax.x - crownMin.x, crownMax.z - crownMin.z);
        var random = new System.Random(1409 + cardCount + leafTriangleList.Count);
        var vertexList = new List<Vector3>();
        var normalList = new List<Vector3>();
        var uvList = new List<Vector2>();
        var indexList = new List<int>();
        for (var k = 0; k < cardCount; k++)
        {
            var pick = areaList.BinarySearch((float)random.NextDouble() * total);
            var t = leafTriangleList[pick < 0 ? Mathf.Min(~pick, leafTriangleList.Count - 1) : pick];
            var a = positions[triangles[t]];
            var b = positions[triangles[t + 1]];
            var c = positions[triangles[t + 2]];
            var u = (float)random.NextDouble();
            var v = (float)random.NextDouble();
            if (u + v > 1)
            {
                u = 1 - u;
                v = 1 - v;
            }
            var point = a + (b - a) * u + (c - a) * v;
            var outward = (point - center).normalized;
            var normal = Vector3.Cross(b - a, c - a).normalized;
            if (Vector3.Dot(normal, outward) < 0)
            {
                normal = -normal;
            }
            // Crown normal: the surface normal leaned toward the crown radius, with a little jitter so clumps overlap.
            normal = (normal + outward + new Vector3((float)random.NextDouble() - .5f, (float)random.NextDouble() - .5f, (float)random.NextDouble() - .5f) * .3f).normalized;
            var size = crownWidth * cardSize * (.8f + .4f * (float)random.NextDouble());
            var right = Vector3.Cross(normal, Mathf.Abs(normal.y) > .9f ? Vector3.forward : Vector3.up).normalized;
            var up = Vector3.Cross(right, normal);
            var angle = (float)random.NextDouble() * Mathf.PI * 2;
            var rotatedRight = (right * Mathf.Cos(angle) + up * Mathf.Sin(angle)) * size * .5f;
            var rotatedUp = (up * Mathf.Cos(angle) - right * Mathf.Sin(angle)) * size * .5f;
            // Half sunk into the body: only the upper leaves of a clump stand out, so the silhouette stays round.
            var cardCenter = point - normal * size * .05f;
            var cell = random.Next(4);
            var cellOrigin = new Vector2(cell % 2, cell / 2) * .5f;
            var start = vertexList.Count;
            vertexList.Add(cardCenter - rotatedRight - rotatedUp);
            vertexList.Add(cardCenter + rotatedRight - rotatedUp);
            vertexList.Add(cardCenter + rotatedRight + rotatedUp);
            vertexList.Add(cardCenter - rotatedRight + rotatedUp);
            for (var corner = 0; corner < 4; corner++)
            {
                normalList.Add(normal);
            }
            uvList.Add(cellOrigin + new Vector2(.01f, .01f));
            uvList.Add(cellOrigin + new Vector2(.49f, .01f));
            uvList.Add(cellOrigin + new Vector2(.49f, .49f));
            uvList.Add(cellOrigin + new Vector2(.01f, .49f));
            indexList.AddRange(new[] { start, start + 1, start + 2, start, start + 2, start + 3 });
        }
        var mesh = new Mesh { name = id + " leaf clusters" };
        mesh.SetVertices(vertexList);
        mesh.SetNormals(normalList);
        mesh.SetUVs(0, uvList);
        mesh.SetTriangles(indexList, 0);
        mesh.RecalculateBounds();
        var child = new GameObject("Leaf clusters", typeof(MeshFilter), typeof(MeshRenderer));
        child.transform.SetParent(instance.transform, false);
        child.layer = HD2DBackgroundRig.BackgroundLayer;
        child.GetComponent<MeshFilter>().sharedMesh = Save(mesh, id + "-Leaves.asset");
        var renderer = child.GetComponent<MeshRenderer>();
        renderer.sharedMaterial = leafMaterial;
        renderer.shadowCastingMode = ShadowCastingMode.On;
    }

    // Step 13: the step-12 flag (pole + cloth with thickness) replaces the box pole and flat sprite cloth.
    // Origin is the pole foot, the anchor FieldView.BuildProps places.
    private static GameObject BuildFlagModel(Material wood, Material flag)
    {
        var root = new GameObject("Flag model");
        try
        {
            root.layer = HD2DBackgroundRig.BackgroundLayer;
            foreach (var (part, name, material) in new[] { ("flag_pole", "Flag pole", wood), ("flag_cloth", "Flag cloth", flag) })
            {
                var child = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
                child.transform.SetParent(root.transform, false);
                child.layer = HD2DBackgroundRig.BackgroundLayer;
                var meshPath = DQEnvironmentR2Import.Art + "/Meshes/" + part + ".asset";
                var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
                if (mesh == null)
                {
                    throw new InvalidDataException("Missing flag mesh " + meshPath + "; run Import environment r2 models first.");
                }
                child.GetComponent<MeshFilter>().sharedMesh = mesh;
                var renderer = child.GetComponent<MeshRenderer>();
                renderer.sharedMaterial = material;
                renderer.shadowCastingMode = ShadowCastingMode.On;
            }
            return PrefabUtility.SaveAsPrefabAsset(root, Root + "/Flag.prefab");
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
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
        // Fine rows near the field, then 2 m rows out to 64 m so the corners and wide screens never see past the land.
        var offsets = Enumerable.Range(0, 81).Select(i => i * .25f).Concat(Enumerable.Range(1, 22).Select(i => 20 + i * 2f)).ToArray();
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
            // Step 14: belt trees stay on the flat crest (offset <= 2.5). On the back slope (3..6) the crest hid their
            // feet and they stood against the far painting (floating trees in the top corners).
            var offset = .6f + 1.9f * Mathf.PerlinNoise(i * .71f, 8);
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
