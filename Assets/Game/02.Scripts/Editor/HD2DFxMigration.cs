using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Presets;
using UnityEditor.U2D;
using UnityEngine;

// FX 전용 이행. 장면·볼륨·배경은 건드리지 않는다.
public static class HD2DFxMigration
{
    public const string Folder = "Assets/Game/03.Resources/Art/HD2D/Effects";
    public const string PresetPath = "Assets/Game/03.Resources/Art/Presets/EffectsSprite.preset";
    private const string AtlasPath = "Assets/Game/03.Resources/Resources/Art/Characters.spriteatlasv2";

    [MenuItem("DeadSurvivor/HD2D/FX/01 Import Frames")]
    public static void ImportFrames()
    {
        if (Application.isPlaying)
        {
            throw new InvalidOperationException("FX import requires Edit Mode");
        }
        AssetDatabase.Refresh();
        var paths = Directory.GetFiles(Folder, "*.png").OrderBy(p => p).ToArray();
        var first = (TextureImporter)AssetImporter.GetAtPath(paths[0]);
        first.textureType = TextureImporterType.Sprite;
        first.spriteImportMode = SpriteImportMode.Single;
        first.spritePixelsPerUnit = 48;
        first.filterMode = FilterMode.Point;
        first.mipmapEnabled = false;
        first.textureCompression = TextureImporterCompression.Uncompressed;
        first.wrapMode = TextureWrapMode.Clamp;
        first.sRGBTexture = true;
        first.alphaIsTransparency = true;
        first.npotScale = TextureImporterNPOTScale.None;
        var settings = new TextureImporterSettings();
        first.ReadTextureSettings(settings);
        settings.spriteMeshType = SpriteMeshType.FullRect;
        settings.spriteAlignment = (int)SpriteAlignment.Center;
        first.SetTextureSettings(settings);
        foreach (var platform in new[] { "Android", "iPhone" })
        {
            first.SetPlatformTextureSettings(new TextureImporterPlatformSettings { name = platform, overridden = true, maxTextureSize = 2048, format = TextureImporterFormat.RGBA32 });
        }
        var preset = AssetDatabase.LoadAssetAtPath<Preset>(PresetPath);
        var snapshot = new Preset(first);
        if (preset == null)
        {
            AssetDatabase.CreateAsset(snapshot, PresetPath);
            preset = snapshot;
        }
        else
        {
            EditorUtility.CopySerialized(snapshot, preset);
            UnityEngine.Object.DestroyImmediate(snapshot);
        }
        var type = new PresetType(first);
        var defaults = Preset.GetDefaultPresetsForType(type).Where(p => p.preset != preset).ToList();
        defaults.Add(new DefaultPreset("glob:\"" + Folder + "/**\"", preset));
        Preset.SetDefaultPresetsForType(type, defaults.ToArray());
        foreach (var path in paths)
        {
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            preset.ApplyTo(importer);
            importer.SaveAndReimport();
        }
        var atlas = SpriteAtlasAsset.Load(AtlasPath);
        var folder = AssetDatabase.LoadAssetAtPath<DefaultAsset>(Folder);
        var importedAtlas = AssetDatabase.LoadAssetAtPath<UnityEngine.U2D.SpriteAtlas>(AtlasPath);
        if (!SpriteAtlasExtensions.GetPackables(importedAtlas).Contains(folder))
        {
            atlas.Add(new UnityEngine.Object[] { folder });
            SpriteAtlasAsset.Save(atlas, AtlasPath);
        }
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }
    [MenuItem("DeadSurvivor/HD2D/FX/02 Wire Materials")]
    public static void WireMaterials()
    {
        if (Application.isPlaying)
        {
            throw new InvalidOperationException("FX wiring requires Edit Mode");
        }
        const string root = "Assets/Game/03.Resources/Materials/HD2D/";
        var names = new[] { "EffectGlow_Blade", "EffectGlow_Fire", "EffectGlow_Enemy", "EffectGround_Heal", "EffectGround_Pulse", "EffectGround_Boom", "EffectGround_Warn" };
        var intensities = new[] { 1f, 2.4f, 1.6f, 1.6f, 2.2f, 1.6f, 1f };
        var materials = new Material[names.Length];
        for (var i = 0; i < names.Length; i++)
        {
            var path = root + "HD2D_" + names[i] + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find("HD2D/HD2D_EffectGlow"));
                AssetDatabase.CreateAsset(material, path);
            }
            material.SetFloat("_CoreIntensity", intensities[i]);
            material.SetFloat("_BodyIntensity", 1f);
            material.SetFloat("_ZWrite", i < 3 ? 1f : 0f);
            material.renderQueue = i < 3 ? 2450 : 2440;
            EditorUtility.SetDirty(material);
            materials[i] = material;
        }
        foreach (var name in new[] { "BurstView", "EnemyShotView", "MonsterView" })
        {
            var path = "Assets/Game/03.Resources/Resources/Prefab/" + name + ".prefab";
            var prefab = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var owner = prefab.GetComponent(name);
                var so = new SerializedObject(owner);
                if (name == "MonsterView")
                {
                    foreach (var field in new[] { "dashLineRenderer", "blastAreaRenderer" })
                    {
                        ((SpriteRenderer)so.FindProperty(field).objectReferenceValue).sharedMaterial = materials[6];
                    }
                }
                else
                {
                    ((SpriteRenderer)so.FindProperty("bodyRenderer").objectReferenceValue).sharedMaterial = materials[name == "BurstView" ? 1 : 2];
                    if (name == "BurstView")
                    {
                        var array = so.FindProperty("effectMaterials");
                        array.arraySize = materials.Length;
                        for (var i = 0; i < materials.Length; i++)
                        {
                            array.GetArrayElementAtIndex(i).objectReferenceValue = materials[i];
                        }
                        so.ApplyModifiedPropertiesWithoutUndo();
                    }
                }
                PrefabUtility.SaveAsPrefabAsset(prefab, path);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(prefab);
            }
        }
        AssetDatabase.SaveAssets();
    }

    [MenuItem("DeadSurvivor/HD2D/FX/03 Wire Projectile Shadow")]
    public static void WireProjectileShadow()
    {
        if (Application.isPlaying)
        {
            throw new InvalidOperationException("FX migration requires Edit Mode");
        }
        const string path = "Assets/Game/03.Resources/Resources/Prefab/ProjectileView.prefab";
        var prefab = PrefabUtility.LoadPrefabContents(path);
        try
        {
            var view = prefab.GetComponent<ProjectileView>();
            var so = new SerializedObject(view);
            var upright = (Transform)so.FindProperty("upright").objectReferenceValue;
            upright.localPosition = TiltedViewMath.UprightOffset(.5f, 30f);
            var shadow = prefab.transform.Find("Shadow");
            if (shadow == null)
            {
                shadow = new GameObject("Shadow").transform;
                shadow.SetParent(prefab.transform, false);
                shadow.gameObject.AddComponent<SpriteRenderer>();
            }
            shadow.localPosition = new Vector3(0, .001f, 0);
            shadow.localRotation = Quaternion.Euler(90, 0, 0);
            shadow.localScale = new Vector3(1, 2, 1);
            var renderer = shadow.GetComponent<SpriteRenderer>();
            renderer.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(Folder + "/shadow_dot.png");
            const string materials = "Assets/Game/03.Resources/Materials/HD2D/HD2D_";
            renderer.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(materials + "HardShadowCard.mat");
            so.FindProperty("shadowRenderer").objectReferenceValue = renderer;
            so.FindProperty("litMaterial").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Material>(materials + "SpriteLit.mat");
            so.FindProperty("glowMaterial").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Material>(materials + "EffectGlow_Fire.mat");
            so.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(prefab, path);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(prefab);
        }
    }

    [MenuItem("DeadSurvivor/HD2D/FX/04 Wire Enemy Shot")]
    public static void WireEnemyShot()
    {
        if (Application.isPlaying)
        {
            throw new InvalidOperationException("FX migration requires Edit Mode");
        }
        const string path = "Assets/Game/03.Resources/Resources/Prefab/EnemyShotView.prefab";
        var prefab = PrefabUtility.LoadPrefabContents(path);
        try
        {
            var so = new SerializedObject(prefab.GetComponent<EnemyShotView>());
            var body = (SpriteRenderer)so.FindProperty("bodyRenderer").objectReferenceValue;
            var upright = prefab.transform.Find("Upright");
            if (upright == null)
            {
                upright = new GameObject("Upright").transform;
                upright.SetParent(prefab.transform, false);
            }
            upright.localRotation = TiltedViewMath.UprightRotation(30);
            upright.localPosition = TiltedViewMath.UprightOffset(.5f, 30);
            body.transform.SetParent(upright, false);
            body.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(Folder + "/spore_1.png");
            body.color = Color.white;
            var shadow = prefab.transform.Find("Shadow");
            if (shadow == null)
            {
                shadow = new GameObject("Shadow").transform;
                shadow.SetParent(prefab.transform, false);
                shadow.gameObject.AddComponent<SpriteRenderer>();
            }
            shadow.localPosition = new Vector3(0, .001f, 0);
            shadow.localRotation = Quaternion.Euler(90, 0, 0);
            shadow.localScale = new Vector3(1, 2, 1);
            var renderer = shadow.GetComponent<SpriteRenderer>();
            renderer.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(Folder + "/shadow_dot.png");
            renderer.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/Game/03.Resources/Materials/HD2D/HD2D_HardShadowCard.mat");
            so.FindProperty("upright").objectReferenceValue = upright;
            so.FindProperty("shadowRenderer").objectReferenceValue = renderer;
            so.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(prefab, path);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(prefab);
        }
    }

    [MenuItem("DeadSurvivor/HD2D/FX/05 Retire Legacy References")]
    public static void RetireLegacyReferences()
    {
        if (Application.isPlaying)
        {
            throw new InvalidOperationException("FX migration requires Edit Mode");
        }
        var atlas = SpriteAtlasAsset.Load(AtlasPath);
        atlas.Remove(new UnityEngine.Object[] { AssetDatabase.LoadAssetAtPath<DefaultAsset>("Assets/Game/03.Resources/Art/AIGenerated/Effects") });
        SpriteAtlasAsset.Save(atlas, AtlasPath);
        foreach (var name in new[] { "BurstView", "ProjectileView", "EnemyShotView", "MonsterView" })
        {
            var path = "Assets/Game/03.Resources/Resources/Prefab/" + name + ".prefab";
            var prefab = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var so = new SerializedObject(prefab.GetComponent(name));
                foreach (var field in name == "MonsterView" ? new[] { "dashLineRenderer", "blastAreaRenderer" } : new[] { "bodyRenderer" })
                {
                    var renderer = (SpriteRenderer)so.FindProperty(field).objectReferenceValue;
                    var key = field == "dashLineRenderer" ? "dash_line_1" : field == "blastAreaRenderer" ? "ground_blast_1"
                        : name == "ProjectileView" ? "arrow_right_1" : name == "EnemyShotView" ? "spore_1" : "hit_star_1";
                    renderer.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(Folder + "/" + key + ".png");
                    renderer.color = Color.white;
                }
                PrefabUtility.SaveAsPrefabAsset(prefab, path);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(prefab);
            }
        }
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }

}
