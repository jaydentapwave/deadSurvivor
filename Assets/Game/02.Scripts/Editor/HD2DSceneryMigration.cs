using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

public static class HD2DSceneryMigration
{
    public const string CatalogPath = "Assets/Game/03.Resources/Models/HD2D/Scenery/FieldModelCatalog.asset";
    private const string Root = "Assets/Game/03.Resources/";
    [Serializable] private sealed class SourceSize { public float x_width, depth, height; }
    [Serializable] private sealed class Source { public string fbx, albedo; public int tris; public SourceSize size_m; }
    [Serializable] private sealed class Sources
    {
        public Source hd2d_tree_v1, hd2d_tree_v2, hd2d_tree_v3, hd2d_bush_v1, hd2d_bush_v2;
        public Source hd2d_boulder_v1, hd2d_boulder_v2, hd2d_stone_v1, hd2d_stone_v2;
        public Source hd2d_wall_a, hd2d_wall_b, hd2d_grass_v1, hd2d_grass_v2;
        public Source hd2d_flower_white, hd2d_flower_yellow, hd2d_flower_purple;
    }
    [Serializable] private sealed class Manifest { public Sources assets; }
    [Serializable] private sealed class Audit { public string utc; public FieldModelEntry[] entries; }

    [MenuItem("DeadSurvivor/HD2D/3D Scenery/Import Adopted Assets")]
    public static void Import()
    {
        if (Application.isPlaying)
        {
            throw new InvalidOperationException("Scenery import requires Edit Mode.");
        }
        var manifest = JsonUtility.FromJson<Manifest>(File.ReadAllText("output/assets/hd2d-3d/manifest.json"));
        foreach (var folder in new[] { "Models", "Textures", "Materials", "Prefab" })
        {
            Directory.CreateDirectory(Root + folder + "/HD2D/Scenery");
        }
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        var catalog = AssetDatabase.LoadAssetAtPath<FieldModelCatalog>(CatalogPath);
        if (catalog == null)
        {
            catalog = ScriptableObject.CreateInstance<FieldModelCatalog>();
            AssetDatabase.CreateAsset(catalog, CatalogPath);
        }
        var entries = new System.Collections.Generic.List<FieldModelEntry>();
        foreach (var field in typeof(Sources).GetFields(BindingFlags.Public | BindingFlags.Instance))
        {
            var source = (Source)field.GetValue(manifest.assets);
            var id = field.Name.Substring("hd2d_".Length);
            var modelPath = Root + "Models/HD2D/Scenery/" + id + ".fbx";
            var texturePath = Root + "Textures/HD2D/Scenery/" + id + ".png";
            Copy(source.fbx, modelPath);
            Copy(source.albedo, texturePath);
            AssetDatabase.ImportAsset(modelPath, ImportAssetOptions.ForceSynchronousImport);
            AssetDatabase.ImportAsset(texturePath, ImportAssetOptions.ForceSynchronousImport);
            var model = (ModelImporter)AssetImporter.GetAtPath(modelPath);
            model.globalScale = 1;
            model.useFileScale = true;
            model.importAnimation = false;
            model.animationType = ModelImporterAnimationType.None;
            model.importNormals = ModelImporterNormals.Import;
            model.materialImportMode = ModelImporterMaterialImportMode.None;
            model.generateSecondaryUV = false;
            model.isReadable = true; // Needed by the spatial grass/flower mesh combiner.
            model.SaveAndReimport();
            var texture = (TextureImporter)AssetImporter.GetAtPath(texturePath);
            texture.textureType = TextureImporterType.Default;
            texture.filterMode = FilterMode.Point;
            texture.mipmapEnabled = false;
            texture.textureCompression = TextureImporterCompression.Uncompressed;
            texture.npotScale = TextureImporterNPOTScale.None;
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.sRGBTexture = true;
            texture.maxTextureSize = 4096;
            texture.SaveAndReimport();
            var albedo = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
            var material = Material(id, albedo, 0);
            var faded = Material(id + "_Fade", albedo, .65f);
            var root = new GameObject(id);
            try
            {
                var child = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(modelPath), root.transform);
                foreach (var renderer in child.GetComponentsInChildren<MeshRenderer>())
                {
                    renderer.gameObject.layer = HD2DBackgroundRig.BackgroundLayer;
                    renderer.sharedMaterial = material;
                    renderer.shadowCastingMode = id.StartsWith("grass_") || id.StartsWith("flower_") ? ShadowCastingMode.Off : ShadowCastingMode.On;
                }
                root.layer = HD2DBackgroundRig.BackgroundLayer;
                var bounds = Bounds(root);
                var expected = new Vector3(source.size_m.x_width, source.size_m.height, source.size_m.depth);
                var triangles = child.GetComponentsInChildren<MeshFilter>().Sum(f => (int)f.sharedMesh.GetIndexCount(0) / 3);
                if (Vector3.Distance(bounds.size, expected) > .003f || Mathf.Abs(bounds.min.y) > .003f || triangles != source.tris)
                {
                    throw new InvalidOperationException(id + " FBX axes/size/pivot/triangles disagree: " + bounds + ", expected " + expected + ", triangles " + triangles);
                }
                var prefabPath = Root + "Prefab/HD2D/Scenery/" + id + ".prefab";
                var prefab = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                var savedBounds = Bounds(prefab);
                if (Vector3.Distance(savedBounds.size, expected) > .003f)
                {
                    throw new InvalidOperationException("Saved prefab dimensions differ: " + id);
                }
                entries.Add(new FieldModelEntry { Id = id, Prefab = prefab, Material = material, FadedMaterial = faded, Bounds = savedBounds, Triangles = triangles });
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }
        catalog.Entries = entries.ToArray();
        EditorUtility.SetDirty(catalog);
        AssetDatabase.SaveAssets();
        Directory.CreateDirectory("output/hd2d-3d-part2");
        File.WriteAllText("output/hd2d-3d-part2/import-audit.json", JsonUtility.ToJson(new Audit { utc = DateTime.UtcNow.ToString("O"), entries = catalog.Entries }, true));
    }

    [MenuItem("DeadSurvivor/HD2D/3D Scenery/Enable In Main")]
    public static void Apply()
    {
        SetEnabled(true);
    }

    [MenuItem("DeadSurvivor/HD2D/3D Scenery/Restore Legacy Cards In Main")]
    public static void RestoreCards()
    {
        SetEnabled(false);
    }

    private static void SetEnabled(bool enabled)
    {
        if (Application.isPlaying)
        {
            throw new InvalidOperationException("Scenery migration requires Edit Mode.");
        }
        const string scenePath = "Assets/Game/01.Scene/Main.unity";
        var scene = EditorSceneManager.OpenScene(scenePath);
        var field = new SerializedObject(Object.FindFirstObjectByType<FieldView>());
        var catalog = AssetDatabase.LoadAssetAtPath<FieldModelCatalog>(CatalogPath);
        if (catalog == null)
        {
            throw new InvalidOperationException("Import adopted scenery first.");
        }
        var flagTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(Root + "Art/AIGenerated/Decor/flag_01.png");
        var flagMaterial = Material("flag_cloth", flagTexture, 0);
        var flagFade = Material("flag_cloth_Fade", flagTexture, .65f);
        foreach (var material in new[] { flagMaterial, flagFade })
        {
            material.SetFloat("_AlphaClip", 1);
            material.SetInt("_Cull", 0);
            EditorUtility.SetDirty(material);
        }
        AssetDatabase.SaveAssets();
        field.FindProperty("flagMaterial").objectReferenceValue = flagMaterial;
        field.FindProperty("ditherFlagMaterial").objectReferenceValue = flagFade;
        field.FindProperty("modelCatalog").objectReferenceValue = catalog;
        field.FindProperty("useModelScenery").boolValue = enabled;
        field.ApplyModifiedPropertiesWithoutUndo();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        EditorSceneManager.OpenScene(scenePath);
        var saved = new SerializedObject(Object.FindFirstObjectByType<FieldView>());
        if (saved.FindProperty("useModelScenery").boolValue != enabled || saved.FindProperty("modelCatalog").objectReferenceValue != catalog)
        {
            throw new InvalidOperationException("Scenery scene references were not saved.");
        }
    }

    private static void Copy(string source, string destination)
    {
        if (!File.Exists(destination) || !File.ReadAllBytes(source).SequenceEqual(File.ReadAllBytes(destination)))
        {
            File.Copy(source, destination, true);
        }
    }

    private static Material Material(string id, Texture2D texture, float dither)
    {
        var path = Root + "Materials/HD2D/Scenery/" + id + ".mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = new Material(Shader.Find("HD2D/HD2D_FieldSurface"));
            AssetDatabase.CreateAsset(material, path);
        }
        material.SetTexture("_MainTex", texture);
        material.SetFloat("_Category", 5);
        material.SetFloat("_FlatDepth", 0);
        material.SetFloat("_Dither", dither);
        EditorUtility.SetDirty(material);
        return material;
    }

    private static Bounds Bounds(GameObject root)
    {
        var filters = root.GetComponentsInChildren<MeshFilter>();
        var bounds = new Bounds();
        var hasBounds = false;
        foreach (var filter in filters)
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
}
