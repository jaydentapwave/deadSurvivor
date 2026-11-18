using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.Presets;
using UnityEditor.U2D;
using UnityEngine;
using UnityEngine.U2D;

public static class HudUiImport
{
    public const string Folder = "Assets/Game/03.Resources/Art/HD2D/UI";
    public const string AtlasPath = "Assets/Game/03.Resources/Resources/Art/HudUi.spriteatlasv2";
    public const string PresetPath = "Assets/Game/03.Resources/Art/Presets/HudUiSprite.preset";

    public static Vector4 Border(string name)
    {
        var json = File.ReadAllText(Folder + "/slices.json");
        var match = Regex.Match(json, "\"" + name + "\"\\s*:\\s*\\{\\s*\"spriteBorder\"\\s*:\\s*\\{\\s*\"x\":\\s*(\\d+),\\s*\"y\":\\s*(\\d+),\\s*\"z\":\\s*(\\d+),\\s*\"w\":\\s*(\\d+)");
        if (!match.Success)
        {
            throw new InvalidOperationException("Missing UI border: " + name);
        }
        return new Vector4(int.Parse(match.Groups[1].Value), int.Parse(match.Groups[2].Value), int.Parse(match.Groups[3].Value), int.Parse(match.Groups[4].Value));
    }

    [MenuItem("DeadSurvivor/Art/Import HD2D UI")]
    public static void Apply()
    {
        if (Application.isPlaying)
        {
            throw new InvalidOperationException("HD2D UI import requires Edit Mode");
        }
        AssetDatabase.Refresh();
        var paths = Directory.GetFiles(Folder, "*.png").OrderBy(p => p).ToArray();
        foreach (var path in paths)
        {
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 100;
            importer.filterMode = FilterMode.Bilinear;
            importer.mipmapEnabled = false;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.sRGBTexture = true;
            importer.alphaIsTransparency = true;
            importer.npotScale = TextureImporterNPOTScale.None;
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteMeshType = SpriteMeshType.FullRect;
            settings.spriteAlignment = (int)SpriteAlignment.Center;
            importer.SetTextureSettings(settings);
            importer.spriteBorder = Border(Path.GetFileNameWithoutExtension(path));
            foreach (var platform in new[] { "Android", "iPhone" })
            {
                importer.SetPlatformTextureSettings(new TextureImporterPlatformSettings { name = platform, overridden = true, maxTextureSize = 2048, format = TextureImporterFormat.RGBA32, textureCompression = TextureImporterCompression.Uncompressed });
            }
            importer.SaveAndReimport();
        }
        var first = (TextureImporter)AssetImporter.GetAtPath(paths[0]);
        var preset = AssetDatabase.LoadAssetAtPath<Preset>(PresetPath);
        var cleanBorder = first.spriteBorder;
        first.spriteBorder = Vector4.zero;
        var snapshot = new Preset(first);
        first.spriteBorder = cleanBorder;
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
        var atlas = AssetDatabase.LoadAssetAtPath<SpriteAtlasAsset>(AtlasPath);
        if (atlas == null)
        {
            atlas = new SpriteAtlasAsset();
        }
        atlas.SetPackingSettings(new SpriteAtlasPackingSettings { enableRotation = false, enableTightPacking = false, padding = 4 });
        atlas.SetTextureSettings(new SpriteAtlasTextureSettings { filterMode = FilterMode.Bilinear, generateMipMaps = false, sRGB = true, readable = false });
        atlas.Add(new UnityEngine.Object[] { AssetDatabase.LoadAssetAtPath<DefaultAsset>(Folder) });
        SpriteAtlasAsset.Save(atlas, AtlasPath);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        // V2 persists packing/texture settings on SpriteAtlasImporter, not on
        // the transient SpriteAtlasAsset used to write the packable list.
        var atlasImporter = (SpriteAtlasImporter)AssetImporter.GetAtPath(AtlasPath);
        atlasImporter.packingSettings = new SpriteAtlasPackingSettings
        {
            enableRotation = false,
            enableTightPacking = false,
            padding = 4
        };
        atlasImporter.textureSettings = new SpriteAtlasTextureSettings
        {
            filterMode = FilterMode.Bilinear,
            generateMipMaps = false,
            sRGB = true,
            readable = false
        };
        atlasImporter.SetPlatformSettings(new TextureImporterPlatformSettings
        {
            name = "DefaultTexturePlatform",
            maxTextureSize = 2048,
            format = TextureImporterFormat.RGBA32,
            textureCompression = TextureImporterCompression.Uncompressed
        });
        atlasImporter.SaveAndReimport();
    }
}
