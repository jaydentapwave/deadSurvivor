using System.IO;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Presets;
using UnityEngine;

// 그림 넣기 AC2 — 19장(13장 + 쓰러짐·예고 6장)의 임포트 값(기획서 B절), Preset Manager 항목, 그림 폴더 CLAUDE.md의 프리셋 경로
public class ArtImportSettingsTests
{
    private const string GeneratedFolder = "Assets/Game/03.Resources/Art/AIGenerated";
    private const string PresetPath = "Assets/Game/03.Resources/Art/Presets/AIGeneratedSprite.preset";
    private const string PresetFilter = "glob:\"Assets/Game/03.Resources/Art/AIGenerated/**\"";
    private const string ArtPolicyPath = "Assets/Game/03.Resources/Art/CLAUDE.md";

    [Test]
    public void NineteenSpritesMatchImportValues()
    {
        var guids = AssetDatabase.FindAssets("t:Texture2D", new[] { GeneratedFolder });
        Assert.AreEqual(19, guids.Length);
        foreach (var guid in guids)
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);

            Assert.AreEqual(TextureImporterType.Sprite, importer.textureType, path);
            Assert.AreEqual(SpriteImportMode.Single, importer.spriteImportMode, path);
            Assert.AreEqual(Const.PixelsPerUnit, importer.spritePixelsPerUnit, path);
            Assert.AreEqual((int)SpriteAlignment.BottomCenter, settings.spriteAlignment, path);
            Assert.AreEqual(FilterMode.Point, importer.filterMode, path);
            Assert.AreEqual(TextureImporterCompression.Uncompressed, importer.textureCompression, path);
            Assert.IsFalse(importer.mipmapEnabled, path);
            Assert.IsTrue(importer.alphaIsTransparency, path);
            Assert.AreEqual(TextureWrapMode.Clamp, importer.wrapMode, path);
            foreach (var platform in new[] { "Android", "iPhone" })
            {
                var platformSettings = importer.GetPlatformTextureSettings(platform);
                Assert.IsTrue(platformSettings.overridden, $"{path} {platform}");
                Assert.AreEqual(TextureImporterFormat.RGBA32, platformSettings.format, $"{path} {platform}");
                Assert.AreEqual(2048, platformSettings.maxTextureSize, $"{path} {platform}");
            }
        }
    }

    [Test]
    public void PresetManagerHasGeneratedFolderFilter()
    {
        var preset = AssetDatabase.LoadAssetAtPath<Preset>(PresetPath);
        Assert.IsNotNull(preset);
        var anyImporter = AssetImporter.GetAtPath(AssetDatabase.GUIDToAssetPath(
            AssetDatabase.FindAssets("t:Texture2D", new[] { GeneratedFolder })[0]));

        var hasEntry = false;
        foreach (var defaultPreset in Preset.GetDefaultPresetsForType(new PresetType(anyImporter)))
        {
            hasEntry |= defaultPreset.filter == PresetFilter && defaultPreset.preset == preset && defaultPreset.enabled;
        }

        Assert.IsTrue(hasEntry);
    }

    [Test]
    public void ArtPolicyNamesPresetPath()
    {
        var policy = File.ReadAllText(ArtPolicyPath);

        StringAssert.Contains($"| 임포트 프리셋 | `{PresetPath}` |", policy);
    }
}
