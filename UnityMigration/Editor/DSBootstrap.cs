using System;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using TMPro;
using UnityEngine.TextCore.LowLevel;
namespace DeadSurvivor.Editor
{
    public static class DSBootstrap
    {
        [MenuItem("Dead Survivor/Build Prototype Scene")]
        public static string Setup()
        {
            string root = "Assets/DeadSurvivor/Resources/DeadSurvivor";
            foreach (var file in Directory.GetFiles(root + "/Sprites", "*.png", SearchOption.AllDirectories))
            {
                var path = file.Replace('\\', '/');
                var ti = (TextureImporter)AssetImporter.GetAtPath(path);
                ti.textureType = TextureImporterType.Sprite;
                ti.spriteImportMode = SpriteImportMode.Single;
                ti.spritePixelsPerUnit = 168;
                ti.alphaIsTransparency = true;
                ti.mipmapEnabled = false;
                ti.filterMode = FilterMode.Bilinear;
                ti.textureCompression = TextureImporterCompression.Uncompressed;
                ti.maxTextureSize = 1024;
                ti.spritePivot = Vector2.one * .5f;
                var settings = new TextureImporterSettings();
                ti.ReadTextureSettings(settings);
                settings.spriteMeshType = SpriteMeshType.FullRect;
                ti.SetTextureSettings(settings);
                ti.SaveAndReimport();
            }
            string fontPath = root + "/UI/PrototypeFont.asset";
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(fontPath);
            if (font == null)
            {
                var source = AssetDatabase.LoadAssetAtPath<Font>(root + "/UI/NotoSansKR.ttf");
                font = TMP_FontAsset.CreateFontAsset(source, 48, 5, GlyphRenderMode.SDFAA, 2048, 2048, AtlasPopulationMode.Dynamic, true);
                font.name = "PrototypeFont";
                font.isMultiAtlasTexturesEnabled = true;
                AssetDatabase.CreateAsset(font, fontPath);
                AssetDatabase.AddObjectToAsset(font.material, font);
                foreach (var t in font.atlasTextures)
                if (t != null)
                    AssetDatabase.AddObjectToAsset(t, font);
                font.TryAddCharacters("데드서바이버브루노한스레나유키볼트미라탱커딜러서포트전투웨이브인벤토리캐릭터공격방어회복장비합성확장승리패배설정출전선발생존자무료부활공용보관함스쿼드재편성0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz");
                EditorUtility.SetDirty(font);
            }
            var active = EditorSceneManager.GetActiveScene();
            if (active.isDirty)
                EditorSceneManager.SaveScene(active);
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var cameraGO = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
            cameraGO.tag = "MainCamera";
            var camera = cameraGO.GetComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 748f / 84;
            camera.transform.position = new Vector3(0, 0, -10);
            camera.backgroundColor = new Color(.098f, .137f, .149f);
            camera.clearFlags = CameraClearFlags.SolidColor;
            var game = new GameObject("DeadSurvivor", typeof(DSGame));
            Directory.CreateDirectory("Assets/DeadSurvivor/Scenes");
            EditorSceneManager.SaveScene(scene, "Assets/DeadSurvivor/Scenes/DeadSurvivor.unity");
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene("Assets/DeadSurvivor/Scenes/DeadSurvivor.unity", true) };
            PlayerSettings.productName = "Dead Survivor";
            PlayerSettings.defaultScreenWidth = 1600;
            PlayerSettings.defaultScreenHeight = 900;
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.AutoRotation;
            PlayerSettings.allowedAutorotateToPortrait = false;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.allowedAutorotateToLandscapeLeft = true;
            PlayerSettings.allowedAutorotateToLandscapeRight = true;
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
            PlayerSettings.defaultIsNativeResolution = false;
            AssetDatabase.SaveAssets();
            return "DeadSurvivor scene and sprite/font assets ready";
        }
        public static string Build()
        {
            var options = new BuildPlayerOptions { scenes = new[] { "Assets/DeadSurvivor/Scenes/DeadSurvivor.unity" }, locationPathName = "Builds/macOS/Dead Survivor.app", target = BuildTarget.StandaloneOSX, options = BuildOptions.Development };
            Directory.CreateDirectory("Builds/macOS");
            var r = BuildPipeline.BuildPlayer(options);
            return r.summary.result + " errors=" + r.summary.totalErrors + " bytes=" + r.summary.totalSize;
        }
    }
}
