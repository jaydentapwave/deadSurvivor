using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

// Rebuilds the four runtime warning frames from all twelve adopted r2 tiles.
public static class MainSceneDQFinishMigration
{
    public const string AssetPath = "Assets/Game/03.Resources/Resources/Art/DQ/BoarTelegraph.asset";
    public const string AttackFxPath = "Assets/Game/03.Resources/Resources/Art/DQ/AttackFx.asset";
    private const string FxManifest = "Assets/Game/03.Resources/Art/HD2D/DQAppliedFx/manifest.json";
    private const string Tiles = "Assets/Game/03.Resources/Art/HD2D/DQAppliedCharacters/BoarTelegraphR2/";

    [MenuItem("DeadSurvivor/DQ/Apply Main finish")]
    public static void Apply() => ApplyCore(false);
    public static void ApplyForFailureTest() => ApplyCore(true);

    private static void ApplyCore(bool injectFailure)
    {
        if (Application.isPlaying || EditorApplication.isCompiling || EditorSceneManager.GetActiveScene().isDirty)
        {
            throw new InvalidOperationException("DQ finish requires saved Edit mode.");
        }
        var manifest = JObject.Parse(File.ReadAllText(FxManifest));
        var targets = new[] { AssetPath, AttackFxPath }.Concat(manifest["entries"].Select(e => (string)e["sheet"] + ".meta")).ToArray();
        var before = targets.Concat(new[] { AssetPath + ".meta", AttackFxPath + ".meta" })
            .ToDictionary(path => path, path => File.Exists(path) ? File.ReadAllBytes(path) : null);
        try
        {
            var assembly = JObject.Parse(File.ReadAllText(Tiles + "assembly.json"));
            if ((int)assembly["tile_width"] != 34 || (int)assembly["body_height"] != 32
                || !assembly["origin"].Values<int>().SequenceEqual(new[] { 17, 192 })
                || !assembly["end"].Values<int>().SequenceEqual(new[] { 17, 17 })
                || !assembly["duration_ms"].Values<int>().SequenceEqual(new[] { 260, 240, 200, 100 }))
            {
                throw new InvalidDataException("Adopted r2 telegraph assembly no longer matches the runtime adapter.");
            }
            var frames = new DQTelegraphTiles.Frame[4];
            var fronts = assembly["stage_fill_front_y"].Values<int>().ToArray();
            for (var stage = 0; stage < 4; stage++)
            {
                var pixels = new Color32[DQTelegraphTiles.Width * DQTelegraphTiles.Height];
                var body = Read("body", stage);
                for (var top = 17; top < 192; top += 32)
                {
                    Blit(pixels, body, 32, top);
                }
                Clear(pixels, 0, 62);
                Blit(pixels, Read("tip", stage), 60, 2);
                Clear(pixels, 192, 209);
                Blit(pixels, Read("start", stage), 17, 192);
                if (stage > 0)
                {
                    for (var y = 0; y < fronts[stage]; y++)
                    {
                        for (var x = 0; x < 34; x++)
                        {
                            var i = (208 - y) * 34 + x;
                            pixels[i] = frames[0].pixels[i];
                        }
                    }
                }
                frames[stage] = new DQTelegraphTiles.Frame { pixels = pixels };
            }
            var asset = AssetDatabase.LoadAssetAtPath<DQTelegraphTiles>(AssetPath);
            if (asset == null)
            {
                asset = ScriptableObject.CreateInstance<DQTelegraphTiles>();
                AssetDatabase.CreateAsset(asset, AssetPath);
            }
            asset.frames = frames;
            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssetIfDirty(asset);
            BuildAttackFx(manifest);
            if (injectFailure)
            {
                throw new InvalidOperationException("Injected DQ finish rollback check.");
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

    private static void BuildAttackFx(JObject manifest)
    {
        var asset = AssetDatabase.LoadAssetAtPath<DQAttackFxLibrary>(AttackFxPath);
        if (asset == null)
        {
            asset = ScriptableObject.CreateInstance<DQAttackFxLibrary>();
            AssetDatabase.CreateAsset(asset, AttackFxPath);
        }
        var sprites = AssetDatabase.LoadAllAssetsAtPath(AttackFxPath).OfType<Sprite>().ToDictionary(s => s.name);
        var entries = new List<DQAttackFxLibrary.Entry>();
        var used = new HashSet<string>();
        foreach (var source in manifest["entries"])
        {
            var path = (string)source["sheet"];
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Default;
            importer.filterMode = FilterMode.Point;
            importer.mipmapEnabled = false;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.sRGBTexture = true;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.maxTextureSize = 1024;
            importer.SaveAndReimport();
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            var actor = (string)source["actor"];
            var direction = (string)source["direction"];
            var frames = new Sprite[7];
            foreach (var frameData in source["sources"])
            {
                var frame = (int)frameData["frame"];
                var name = "dq_fx_" + actor + "_" + direction + "_" + frame;
                used.Add(name);
                if (!sprites.TryGetValue(name, out var sprite))
                {
                    sprite = Sprite.Create(texture, new Rect(frame * 72, 0, 72, 72), new Vector2(.5f, 8f / 72), 48, 0, SpriteMeshType.FullRect);
                    sprite.name = name;
                    AssetDatabase.AddObjectToAsset(sprite, asset);
                    sprites.Add(name, sprite);
                }
                frames[frame] = sprite;
            }
            var facing = direction switch { "down" => FacingDirection.Down, "front" => FacingDirection.DownRight,
                "right" => FacingDirection.Right, "upright" => FacingDirection.UpRight, "up" => FacingDirection.Up,
                _ => throw new InvalidDataException("Unknown adopted attack FX direction.") };
            entries.Add(new DQAttackFxLibrary.Entry { actor = actor, direction = facing, eventFrame = (int)source["eventFrame"], frames = frames });
        }
        foreach (var pair in sprites)
        {
            if (!used.Contains(pair.Key))
            {
                Object.DestroyImmediate(pair.Value, true);
            }
        }
        asset.entries = entries.ToArray();
        const string materials = "Assets/Game/03.Resources/Materials/HD2D/";
        asset.bladeMaterial = AssetDatabase.LoadAssetAtPath<Material>(materials + "HD2D_EffectGlow_Blade.mat");
        asset.magicMaterial = AssetDatabase.LoadAssetAtPath<Material>(materials + "HD2D_EffectGlow_Fire.mat");
        if (asset.bladeMaterial == null || asset.magicMaterial == null)
        {
            throw new InvalidDataException("Missing adopted attack FX shared materials.");
        }
        EditorUtility.SetDirty(asset);
        AssetDatabase.SaveAssetIfDirty(asset);
    }

    private static Color32[] Read(string kind, int stage)
    {
        var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        try
        {
            if (!texture.LoadImage(File.ReadAllBytes(Tiles + kind + "_" + stage.ToString("00") + ".png")))
            {
                throw new InvalidDataException("Cannot decode adopted telegraph tile.");
            }
            if (texture.width != 34 || texture.height != (kind == "body" ? 32 : kind == "tip" ? 60 : 17))
            {
                throw new InvalidDataException("Unexpected adopted telegraph tile dimensions.");
            }
            return texture.GetPixels32();
        }
        finally
        {
            Object.DestroyImmediate(texture);
        }
    }

    private static void Clear(Color32[] pixels, int fromTop, int toTop)
    {
        for (var y = fromTop; y < toTop; y++)
        {
            Array.Clear(pixels, (208 - y) * 34, 34);
        }
    }

    private static void Blit(Color32[] target, Color32[] source, int height, int top)
    {
        for (var y = 0; y < height && y + top < 209; y++)
        {
            Array.Copy(source, (height - 1 - y) * 34, target, (208 - y - top) * 34, 34);
        }
    }
}
