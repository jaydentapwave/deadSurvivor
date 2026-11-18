using System.IO;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.U2D;

public class HudUiImportTests
{
    [Test]
    public void AllFortyFourSpritesUseHighResolutionImportAndManifestBorders()
    {
        const string folder = "Assets/Game/03.Resources/Art/HD2D/UI";
        var paths = Directory.GetFiles(folder, "*.png");
        Assert.AreEqual(44, paths.Length);
        var importType = System.Type.GetType("HudUiImport, Game.Editor");
        foreach (var path in paths)
        {
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            Assert.AreEqual(FilterMode.Bilinear, importer.filterMode, path);
            Assert.AreEqual(100, importer.spritePixelsPerUnit, path);
            Assert.IsFalse(importer.mipmapEnabled, path);
            Assert.AreEqual(TextureImporterCompression.Uncompressed, importer.textureCompression, path);
            var expected = (Vector4)importType.GetMethod("Border").Invoke(null, new object[] { Path.GetFileNameWithoutExtension(path) });
            Assert.AreEqual(expected, importer.spriteBorder, path);
            foreach (var platform in new[] { "Android", "iPhone" })
            {
                var setting = importer.GetPlatformTextureSettings(platform);
                Assert.IsTrue(setting.overridden, path);
                Assert.AreEqual(TextureImporterFormat.RGBA32, setting.format, path);
            }
        }
    }

    [Test]
    public void HudAtlasContainsEverySpriteAndUsesBilinear()
    {
        var atlas = AssetDatabase.LoadAssetAtPath<SpriteAtlas>("Assets/Game/03.Resources/Resources/Art/HudUi.spriteatlasv2");
        Assert.IsNotNull(atlas);
        UnityEditor.U2D.SpriteAtlasUtility.PackAtlases(new[] { atlas }, EditorUserBuildSettings.activeBuildTarget);
        Assert.AreEqual(44, atlas.spriteCount);
        Assert.AreEqual(FilterMode.Bilinear, UnityEditor.U2D.SpriteAtlasExtensions.GetTextureSettings(atlas).filterMode);
    }
}
