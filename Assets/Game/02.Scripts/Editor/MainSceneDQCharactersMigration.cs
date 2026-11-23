using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.U2D;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.U2D;

// Only this menu edits DQ importer/atlas assets. Main/prefabs/materials need no edits:
// existing views resolve their visual data at the established binding points.
public static class MainSceneDQCharactersMigration
{
    public const string DataPath = "Assets/Game/03.Resources/Resources/Data/DQ/dq_character_clips.json";
    public const string AtlasFolder = "Assets/Game/03.Resources/Resources/Art/DQ";

    [MenuItem("DeadSurvivor/DQ/Apply characters")]
    public static void Apply() => ApplyCore(false);

    public static void ApplyForFailureTest() => ApplyCore(true);

    private static void ApplyCore(bool injectFailure)
    {
        if (Application.isPlaying || EditorApplication.isCompiling)
        {
            throw new InvalidOperationException("DQ characters migration requires ready Edit mode.");
        }
        for (var i = 0; i < EditorSceneManager.sceneCount; i++)
        {
            var open = EditorSceneManager.GetSceneAt(i);
            if (open.isDirty && open.path != MainSceneDQScreenMigration.ScenePath)
            {
                throw new InvalidOperationException("Refusing to save an unrelated dirty scene: " + open.path);
            }
        }
        var catalog = JsonUtility.FromJson<DQClipCatalog>(File.ReadAllText(DataPath));
        foreach (var actor in catalog.actors)
        {
            foreach (var clip in actor.clips)
            {
                clip.Validate();
            }
            if (!File.Exists(actor.sheet))
            {
                throw new InvalidOperationException("Missing DQ sheet: " + actor.sheet);
            }
        }
        Directory.CreateDirectory(AtlasFolder);
        AssetDatabase.Refresh();
        var targets = catalog.actors.SelectMany(a => new[] { a.sheet + ".meta", AtlasPath(a), AtlasPath(a) + ".meta" })
            .Concat(new[] { MainSceneDQScreenMigration.ScenePath, MainSceneDQScreenMigration.ScenePath + ".meta" }).ToArray();
        var telegraphFolder = "Assets/Game/03.Resources/Art/HD2D/DQAppliedCharacters/BoarTelegraphR2";
        var telegraph = Directory.Exists(telegraphFolder) ? Directory.GetFiles(telegraphFolder, "*.png") : Array.Empty<string>();
        targets = targets.Concat(telegraph.Select(p => p + ".meta")).ToArray();
        var before = targets.ToDictionary(p => p, p => File.Exists(p) ? File.ReadAllBytes(p) : null);
        try
        {
            foreach (var actor in catalog.actors)
            {
                Import(actor);
                BuildAtlas(actor);
                if (injectFailure)
                {
                    throw new InvalidOperationException("Injected DQ characters migration failure");
                }
            }
            foreach (var path in telegraph)
            {
                var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.spritePixelsPerUnit = 48;
                importer.filterMode = FilterMode.Point;
                importer.mipmapEnabled = false;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.npotScale = TextureImporterNPOTScale.None;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.SaveAndReimport();
            }
            SpriteAtlasUtility.PackAllAtlases(EditorUserBuildSettings.activeBuildTarget);
            if (!EditorSceneManager.SaveOpenScenes())
            {
                throw new InvalidOperationException("DQ characters migration could not save its open Main scene.");
            }
        }
        catch
        {
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
            AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
            throw;
        }
    }

    private static string AtlasPath(DQActorData actor) => AtlasFolder + "/" + actor.actorId + ".spriteatlasv2";

    private static void Import(DQActorData actor)
    {
        var importer = (TextureImporter)AssetImporter.GetAtPath(actor.sheet);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Multiple;
        importer.spritePixelsPerUnit = 48;
        importer.filterMode = FilterMode.Point;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.mipmapEnabled = false;
        importer.npotScale = TextureImporterNPOTScale.None;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.sRGBTexture = true;
        importer.maxTextureSize = 2048;
        var settings = new TextureImporterSettings();
        importer.ReadTextureSettings(settings);
        settings.spriteMeshType = SpriteMeshType.FullRect;
        importer.SetTextureSettings(settings);
        importer.GetSourceTextureWidthAndHeight(out var sourceWidth, out var sourceHeight);
        var rows = new List<SpriteMetaData>();
        foreach (var sprite in actor.sprites)
        {
            rows.Add(new SpriteMetaData
            {
                name = sprite.key,
                rect = new Rect(sprite.index % actor.columns * actor.stride + 4,
                    sourceHeight - (sprite.index / actor.columns * actor.stride + 4) - sprite.size, sprite.size, sprite.size),
                alignment = (int)SpriteAlignment.Custom,
                pivot = new Vector2(sprite.pivotX / (float)sprite.size, 1f - sprite.pivotY / (float)sprite.size)
            });
        }
#pragma warning disable CS0618
        importer.spritesheet = rows.ToArray();
#pragma warning restore CS0618
        foreach (var platform in new[] { "DefaultTexturePlatform", "Android", "iPhone" })
        {
            importer.SetPlatformTextureSettings(new TextureImporterPlatformSettings
            {
                name = platform, overridden = platform != "DefaultTexturePlatform", maxTextureSize = 2048,
                format = TextureImporterFormat.RGBA32, textureCompression = TextureImporterCompression.Uncompressed
            });
        }
        importer.SaveAndReimport();
    }

    private static void BuildAtlas(DQActorData actor)
    {
        var atlas = new SpriteAtlasAsset();
        atlas.Add(new UnityEngine.Object[] { AssetDatabase.LoadAssetAtPath<Texture2D>(actor.sheet) });
        SpriteAtlasAsset.Save(atlas, AtlasPath(actor));
        AssetDatabase.Refresh();
        var importer = (SpriteAtlasImporter)AssetImporter.GetAtPath(AtlasPath(actor));
        importer.packingSettings = new SpriteAtlasPackingSettings { enableRotation = false, enableTightPacking = false, padding = 4 };
        importer.textureSettings = new SpriteAtlasTextureSettings { filterMode = FilterMode.Point, generateMipMaps = false, sRGB = true, readable = false };
        foreach (var platform in new[] { "DefaultTexturePlatform", "Android", "iPhone" })
        {
            importer.SetPlatformSettings(new TextureImporterPlatformSettings
            {
                name = platform, overridden = platform != "DefaultTexturePlatform", maxTextureSize = 2048,
                format = TextureImporterFormat.RGBA32, textureCompression = TextureImporterCompression.Uncompressed
            });
        }
        importer.SaveAndReimport();
    }
}
