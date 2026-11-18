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
        atlas.Remove(new UnityEngine.Object[] { folder });
        atlas.Add(new UnityEngine.Object[] { folder });
        SpriteAtlasAsset.Save(atlas, AtlasPath);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }
}
