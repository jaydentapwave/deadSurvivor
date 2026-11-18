using System.IO;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Presets;
using UnityEngine;

// 그림 넣기 AC2 — 캐릭터·몬스터(동작 프레임 포함)·바닥의 임포트 값(기획서 B절), Preset Manager 항목, 그림 폴더 CLAUDE.md의 프리셋 경로
public class ArtImportSettingsTests
{
    private const string GeneratedFolder = "Assets/Game/03.Resources/Art/AIGenerated";
    private const string PresetPath = "Assets/Game/03.Resources/Art/Presets/AIGeneratedSprite.preset";
    private const string PresetFilter = "glob:\"Assets/Game/03.Resources/Art/AIGenerated/**\"";
    private const string ArtPolicyPath = "Assets/Game/03.Resources/Art/CLAUDE.md";

    [Test]
    public void CharactersIncludingMotionMatchImportValues()
    {
        var guids = AssetDatabase.FindAssets("t:Texture2D", new[] { GeneratedFolder + "/Characters", GeneratedFolder + "/Monsters" });
        var characters = new CharacterRepository(new ResourceService()).CharacterDataList;
        // Existing bodies/states + 12 hero directions + 20 monster directions + 8 warning directions.
        var expected = 19 + 4 * 3 + 5 * 4 + 2 * 4;
        foreach (var character in characters)
        {
            expected += 2 * (character.Motion.WalkFrames + character.Motion.AttackFrames);
        }

        Assert.AreEqual(expected, guids.Length);
        AssertImportValues(guids);
    }

    // 바닥 넣기: 풀 4장 + 흙길 9장(아트 기준서 3.3)도 같은 프리셋 값
    [Test]
    public void ThirteenGroundSpritesMatchImportValues()
    {
        var guids = AssetDatabase.FindAssets("t:Texture2D", new[] { GeneratedFolder + "/Ground" });
        Assert.AreEqual(13, guids.Length);
        AssertImportValues(guids);
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

    private static void AssertImportValues(string[] guids)
    {
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
}
