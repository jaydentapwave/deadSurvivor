using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class MainSceneHD2DBackgroundMigration
{
    private const string Root = "Assets/Game/03.Resources/";
    private const string ScenePath = "Assets/Game/01.Scene/Main.unity";

    [MenuItem("DeadSurvivor/HD2D/Migrate Main Background")]
    public static void Apply()
    {
        if (Application.isPlaying)
        {
            throw new InvalidOperationException("[MainSceneHD2DBackgroundMigration] Edit Mode required.");
        }
        var scene = EditorSceneManager.OpenScene(ScenePath);
        var field = UnityEngine.Object.FindFirstObjectByType<FieldView>();
        var pond = AssetDatabase.LoadAssetAtPath<Mesh>(Root + "Meshes/HD2D/FieldPond.asset");
        var layout = JsonUtility.FromJson<FieldLayoutData>(File.ReadAllText(Root + "Resources/Data/Field/field_layout.json"));
        BakeWaterOpening(pond, layout);
        foreach (var file in Directory.GetFiles(Root + "Textures/HD2D", "*.png"))
        {
            var importer = (TextureImporter)AssetImporter.GetAtPath(file);
            importer.filterMode = FilterMode.Point;
            importer.mipmapEnabled = false;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.maxTextureSize = 4096;
            importer.sRGBTexture = !file.Contains("Mask") && !file.Contains("Pattern");
            importer.SaveAndReimport();
        }
        foreach (var name in new[] { "Bridge", "Tent" })
        {
            var model = (ModelImporter)AssetImporter.GetAtPath(Root + "Models/HD2D/" + name + ".fbx");
            model.globalScale = 1;
            model.useFileScale = true;
            model.importAnimation = false;
            model.animationType = ModelImporterAnimationType.None;
            model.isReadable = false;
            model.generateSecondaryUV = false;
            model.importNormals = ModelImporterNormals.Calculate;
            model.normalSmoothingAngle = 0;
            model.materialImportMode = ModelImporterMaterialImportMode.None;
            model.SaveAndReimport();
        }
        var so = new SerializedObject(field);
        foreach (var name in new[] { "ground", "bank", "water" })
        {
            var title = char.ToUpperInvariant(name[0]) + name.Substring(1);
            so.FindProperty(name + "Material").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Material>(Root + "Materials/HD2D/HD2D_Field_" + title + ".mat");
        }
        so.FindProperty("pondMesh").objectReferenceValue = pond;
        so.FindProperty("cardMaterial").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Material>(Root + "Materials/HD2D/HD2D_SpriteLit.mat");
        so.FindProperty("footShadowMaterial").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Material>(Root + "Materials/HD2D/HD2D_FootShadow.mat");
        so.FindProperty("woodMaterial").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Material>(Root + "Materials/HD2D/HD2D_Field_Wood.mat");
        so.FindProperty("bridgeMaterial").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Material>(Root + "Materials/HD2D/HD2D_Prop_Bridge.mat");
        so.FindProperty("tentMaterial").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Material>(Root + "Materials/HD2D/HD2D_Prop_Tent.mat");
        so.FindProperty("hardShadowMaterial").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Material>(Root + "Materials/HD2D/HD2D_HardShadowCard.mat");
        foreach (var pair in new[] { ("ditherSpriteMaterial", "HD2D_DitherSprite"), ("ditherWoodMaterial", "HD2D_DitherWood"), ("ditherTentMaterial", "HD2D_DitherTent") })
        {
            so.FindProperty(pair.Item1).objectReferenceValue = AssetDatabase.LoadAssetAtPath<Material>(Root + "Materials/HD2D/" + pair.Item2 + ".mat");
        }
        so.FindProperty("bridgeModel").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>(Root + "Models/HD2D/Bridge.fbx");
        so.FindProperty("tentModel").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>(Root + "Models/HD2D/Tent.fbx");
        so.ApplyModifiedPropertiesWithoutUndo();
        var ground = AssetDatabase.LoadAssetAtPath<Material>(Root + "Materials/HD2D/HD2D_Field_Ground.mat");
        foreach (var guid in AssetDatabase.FindAssets("t:Material", new[] { Root + "Materials/HD2D" }))
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guid));
            if (material.shader == Shader.Find("HD2D/HD2D_FieldSurface"))
            {
                material.SetTexture("_Dirt", AssetDatabase.LoadAssetAtPath<Texture2D>(Root + "Textures/HD2D/FieldGravel.png"));
                EditorUtility.SetDirty(material);
            }
        }
        ground.SetFloat("_FlatDepth", 1);
        ground.SetTexture("_WaterMask", AssetDatabase.LoadAssetAtPath<Texture2D>(Root + "Textures/HD2D/FieldWaterMask.png"));
        EditorUtility.SetDirty(ground);
        var foot = AssetDatabase.LoadAssetAtPath<Material>(Root + "Materials/HD2D/HD2D_FootShadow.mat");
        foot.SetTexture("_BankMask", AssetDatabase.LoadAssetAtPath<Texture2D>(Root + "Textures/HD2D/FieldBankMask.png"));
        EditorUtility.SetDirty(foot);
        field.Tilemap.GetComponent<UnityEngine.Tilemaps.TilemapRenderer>().enabled = false;
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        EditorSceneManager.OpenScene(ScenePath);
        var saved = new SerializedObject(UnityEngine.Object.FindFirstObjectByType<FieldView>());
        if (saved.FindProperty("pondMesh").objectReferenceValue != pond || saved.FindProperty("groundMaterial").objectReferenceValue != ground)
        {
            throw new InvalidOperationException("[MainSceneHD2DBackgroundMigration] Background references not saved.");
        }
    }

    private static void BakeWaterOpening(Mesh pond, FieldLayoutData layout)
    {
        const int width = 3456;
        const int height = 1728;
        var colors = new Color32[width * height];
        void Raster(Mesh mesh)
        {
            var vertices = mesh.vertices;
            var triangles = mesh.triangles;
            for (var i = 0; i < triangles.Length; i += 3)
            {
                Vector2 Pixel(Vector3 v) => new((v.x + 36) * 48, (v.z + 36) * 24);
                var a = Pixel(vertices[triangles[i]]);
                var b = Pixel(vertices[triangles[i + 1]]);
                var c = Pixel(vertices[triangles[i + 2]]);
                float Cross(Vector2 u, Vector2 v) => u.x * v.y - u.y * v.x;
                var area = Cross(b - a, c - a);
                if (Mathf.Abs(area) < .0001f)
                {
                    continue;
                }
                var minX = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(a.x, Mathf.Min(b.x, c.x))));
                var maxX = Mathf.Min(width - 1, Mathf.CeilToInt(Mathf.Max(a.x, Mathf.Max(b.x, c.x))));
                var minY = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(a.y, Mathf.Min(b.y, c.y))));
                var maxY = Mathf.Min(height - 1, Mathf.CeilToInt(Mathf.Max(a.y, Mathf.Max(b.y, c.y))));
                for (var y = minY; y <= maxY; y++)
                {
                    for (var x = minX; x <= maxX; x++)
                    {
                        var p = new Vector2(x + .5f, y + .5f);
                        var u = Cross(b - p, c - p) / area;
                        var v = Cross(c - p, a - p) / area;
                        if (u >= 0 && v >= 0 && u + v <= 1)
                        {
                            colors[y * width + x] = new Color32(255, 255, 255, 255);
                        }
                    }
                }
            }
        }
        Raster(pond);
        foreach (var water in layout.water)
        {
            if (water.name != "stream")
            {
                continue;
            }
            var ribbon = RibbonMeshMath.Build(water.points, water.width, water.height);
            Raster(ribbon);
            UnityEngine.Object.DestroyImmediate(ribbon);
        }
        var texture = new Texture2D(width, height, TextureFormat.RGBA32, false, true);
        texture.SetPixels32(colors);
        texture.Apply();
        File.WriteAllBytes(Root + "Textures/HD2D/FieldWaterMask.png", texture.EncodeToPNG());
        colors = new Color32[width * height];
        foreach (var bank in layout.banks)
        {
            var mesh = BankMeshMath.Build(bank.rectangles, bank.cell, bank.height, bank.seed);
            Raster(mesh);
            UnityEngine.Object.DestroyImmediate(mesh);
        }
        texture.SetPixels32(colors);
        texture.Apply();
        File.WriteAllBytes(Root + "Textures/HD2D/FieldBankMask.png", texture.EncodeToPNG());
        UnityEngine.Object.DestroyImmediate(texture);
        AssetDatabase.Refresh();
    }
}
