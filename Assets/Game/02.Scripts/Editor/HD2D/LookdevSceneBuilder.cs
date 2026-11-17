using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

public static class LookdevSceneBuilder
{
    public const string ScenePath = "Assets/Game/01.Scene/Lookdev_HD2D.unity";
    public const string ArtPath = "Assets/Game/03.Resources/Art/HD2D";
    public const string SpritePath = "Assets/Game/03.Resources/Art/AIGenerated";
    private const string OldMapPath = "Assets/DeadSurvivor/Scenes/Map046/Map046_Full.unity";

    [MenuItem("DeadSurvivor/HD2D/Build Lookdev")]
    public static void Build()
    {
        if (EditorApplication.isPlaying)
        {
            throw new InvalidOperationException("Build requires stopped Play Mode.");
        }
        if (SceneManager.GetActiveScene().isDirty)
        {
            throw new InvalidOperationException("Save the current scene before building lookdev.");
        }
        Directory.CreateDirectory(ArtPath);
        AssetDatabase.Refresh();
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var spriteMaterial = MaterialAsset("PixelSprite", "HD2D/PixelSpriteLit");
        var effectMaterial = MaterialAsset("PixelEffect", "HD2D/PixelSpriteLit");
        effectMaterial.SetFloat("_Emission", 2f);
        var grassTexture = CreateGroundTexture();
        var groundMaterial = MaterialAsset("Grass", "HD2D/PixelSurface");
        groundMaterial.SetTexture("_MainTex", grassTexture);
        groundMaterial.SetFloat("_GroundTexture", 1f);
        var roadMaterial = MaterialAsset("Road", "HD2D/PixelSurface");
        roadMaterial.SetColor("_BaseColor", Hex("cfa36c"));
        var waterMaterial = MaterialAsset("Pond", "HD2D/PixelSurface");
        waterMaterial.SetColor("_BaseColor", Hex("658d8a"));
        var floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
        floor.name = "48 texels per unit grass";
        floor.transform.localScale = new Vector3(5f, 1f, 5f);
        floor.GetComponent<Renderer>().sharedMaterial = groundMaterial;
        Object.DestroyImmediate(floor.GetComponent<Collider>());
        var road = GameObject.CreatePrimitive(PrimitiveType.Cube);
        road.name = "046 main road study";
        road.transform.position = new Vector3(0f, -0.01f, 2.8f);
        road.transform.localScale = new Vector3(32f, 0.03f, 1.45f);
        road.GetComponent<Renderer>().sharedMaterial = roadMaterial;
        Object.DestroyImmediate(road.GetComponent<Collider>());
        ImportMapRegion(scene, waterMaterial);
        CreateProps();
        CreateActors(spriteMaterial, effectMaterial);
        var cameraObject = new GameObject("Lookdev Camera") { tag = "MainCamera" };
        var camera = cameraObject.AddComponent<Camera>();
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = Hex("b4aa85");
        camera.nearClipPlane = 0.1f;
        camera.farClipPlane = 160f;
        camera.allowMSAA = false;
        var data = cameraObject.AddComponent<UniversalAdditionalCameraData>();
        data.SetRenderer(0);
        data.renderPostProcessing = true;
        data.antialiasing = AntialiasingMode.None;
        TiltedCameraUtils.Configure(camera, 30f, true, 12f, 0.133f, new Vector3(0f, 0.4f, 0f));
        var sun = new GameObject("Afternoon sun").AddComponent<Light>();
        sun.type = LightType.Directional;
        sun.transform.rotation = Quaternion.Euler(48f, -38f, 0f);
        sun.color = new Color(1f, 0.89f, 0.74f);
        sun.intensity = 1.15f;
        sun.shadows = LightShadows.Hard;
        sun.shadowBias = 0.025f;
        sun.shadowNormalBias = 0.025f;
        RenderSettings.sun = sun;
        RenderSettings.ambientMode = AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = Hex("b6a58c");
        RenderSettings.ambientEquatorColor = Hex("85755e");
        RenderSettings.ambientGroundColor = Hex("665045");
        RenderSettings.skybox = null;
        var volume = new GameObject("HD2D global volume").AddComponent<Volume>();
        volume.isGlobal = true;
        volume.sharedProfile = CreateProfile();
        var rendererData = CreateRenderer();
        var scope = new GameObject("Transient rendering settings").AddComponent<LookdevRenderScope>();
        var serializedScope = new SerializedObject(scope);
        serializedScope.FindProperty("rendererData").objectReferenceValue = rendererData;
        serializedScope.FindProperty("mobilePipeline").objectReferenceValue = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>("Assets/Settings/Mobile_RPAsset.asset");
        serializedScope.ApplyModifiedPropertiesWithoutUndo();
        // Save only the new asset tree; do not save dirty user assets or project settings.
        foreach (var guid in AssetDatabase.FindAssets("", new[] { ArtPath }))
        {
            var asset = AssetDatabase.LoadMainAssetAtPath(AssetDatabase.GUIDToAssetPath(guid));
            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssetIfDirty(asset);
        }
        EditorSceneManager.SaveScene(scene, ScenePath);
    }

    public static SpriteRenderer AddSprite(string name, string path, Vector3 position, Material material, bool canCastShadow)
    {
        var body = new GameObject(name).AddComponent<SpriteRenderer>();
        body.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if (body.sprite == null)
        {
            throw new FileNotFoundException(path);
        }
        body.sharedMaterial = material;
        body.transform.position = position;
        body.transform.rotation = Quaternion.Euler(30f, 0f, 0f);
        body.shadowCastingMode = ShadowCastingMode.Off;
        body.receiveShadows = true;
        if (name.StartsWith("Member_") || name.StartsWith("Monster_"))
        {
            var shadowMaterial = MaterialAsset("PixelShadow", "HD2D/PixelSpriteLit");
            shadowMaterial.SetFloat("_SpriteRenderer", 0f);
            body.gameObject.AddComponent<PixelShadowCaster>().Initialize(body, shadowMaterial, canCastShadow);
        }
        return body;
    }

    private static void CreateActors(Material material, Material effectMaterial)
    {
        var actors = new GameObject("Actors").transform;
        var names = new[] { "bruno", "lena", "mira", "bolt" };
        var positions = new[] { new Vector3(0,0,-0.4f), new Vector3(-1.4f,0,-1.2f), new Vector3(1.35f,0,-1f), new Vector3(-0.8f,0,-2.4f) };
        for (var i = 0; i < names.Length; i++)
        {
            var direction = i % 2 == 0 ? "front" : "back";
            var body = AddSprite("Member_" + names[i], $"{SpritePath}/Characters/{names[i]}/{direction}/{names[i]}_{direction}.png", positions[i], material, true);
            body.transform.SetParent(actors, true);
            body.flipX = i % 2 == 1;
            body.color = i == 1 ? new Color(0.75f, 0.93f, 1f) : Color.white;
            if (i < 2)
            {
                var frames = Directory.GetFiles($"{SpritePath}/Characters/{names[i]}/walk_{direction}", "*.png").OrderBy(p => p).Select(AssetDatabase.LoadAssetAtPath<Sprite>).ToArray();
                var animation = body.gameObject.AddComponent<PixelWalkPreview>();
                var serialized = new SerializedObject(animation);
                serialized.FindProperty("bodyRenderer").objectReferenceValue = body;
                var frameProperty = serialized.FindProperty("frames");
                frameProperty.arraySize = frames.Length;
                for (var f = 0; f < frames.Length; f++)
                {
                    frameProperty.GetArrayElementAtIndex(f).objectReferenceValue = frames[f];
                }
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }
        }
        var monsterMaterial = MaterialAsset("PixelMonster", "HD2D/PixelSpriteLit");
        monsterMaterial.SetFloat("_HueShift", 0.68f);
        var monsters = new GameObject("Monsters").transform;
        var types = new[] { "slime", "mushroom", "rock", "boar", "puffer" };
        for (var i = 0; i < 15; i++)
        {
            var type = types[i % 5];
            var angle = i * Mathf.PI * 2f / 15;
            var position = new Vector3(Mathf.Cos(angle) * 3.5f, 0, Mathf.Sin(angle) * 3.7f);
            var body = AddSprite("Monster_" + type, $"{SpritePath}/Monsters/{type}/{type}.png", position, monsterMaterial, false);
            body.flipX = i % 2 == 0;
            body.transform.SetParent(monsters, true);
            var shadow = AddSprite("Blob shadow", SpritePath + "/Common/shadow.png", position + Vector3.up * 0.025f, AssetDatabase.GetBuiltinExtraResource<Material>("Sprites-Default.mat"), false);
            shadow.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            shadow.color = new Color(0.27f, 0.16f, 0.14f, 0.65f);
            shadow.transform.SetParent(body.transform, true);
        }
        AddSprite("Golden sword arc", SpritePath + "/Effects/slash_sword.png", new Vector3(0.65f,0,-0.1f), effectMaterial, false).transform.SetParent(actors, true);
        AddSprite("Fire glow", SpritePath + "/Effects/fireball.png", new Vector3(2.2f,0,0.4f), effectMaterial, false).transform.SetParent(actors, true);
    }

    private static void ImportMapRegion(Scene targetScene, Material waterMaterial)
    {
        var source = EditorSceneManager.OpenScene(OldMapPath, OpenSceneMode.Additive);
        try
        {
            var renderers = source.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<MeshRenderer>());
            foreach (var renderer in renderers.Where(r => r.name == "OB-006_Water" || r.name == "OB-006_Stones"))
            {
                var mesh = Object.Instantiate(renderer.GetComponent<MeshFilter>().sharedMesh);
                var vertices = mesh.vertices;
                for (var i = 0; i < vertices.Length; i++)
                {
                    vertices[i] = (renderer.transform.TransformPoint(vertices[i]) - new Vector3(-13.6f, 0, 29f)) * 1.25f + new Vector3(-4.8f, 0, 6.5f);
                }
                mesh.vertices = vertices;
                mesh.RecalculateNormals();
                mesh.RecalculateBounds();
                var meshPath = ArtPath + "/" + renderer.name + ".asset";
                var existing = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
                if (existing == null)
                {
                    AssetDatabase.CreateAsset(mesh, meshPath);
                }
                else
                {
                    EditorUtility.CopySerialized(mesh, existing);
                    Object.DestroyImmediate(mesh);
                    mesh = existing;
                }
                var prop = new GameObject("046_" + renderer.name);
                SceneManager.MoveGameObjectToScene(prop, targetScene);
                prop.AddComponent<MeshFilter>().sharedMesh = mesh;
                var shoreMaterial = MaterialAsset("ShorePalette", "HD2D/PixelSurface");
                shoreMaterial.SetTexture("_MainTex", CreatePaletteTexture((Texture2D)AssetDatabase.LoadAssetAtPath<Material>("Assets/DeadSurvivor/Art/Environment/Map/Stone045s2.mat").GetTexture("_Palette")));
                prop.AddComponent<MeshRenderer>().sharedMaterials = renderer.name.Contains("Water") ? new[] { waterMaterial } : new[] { shoreMaterial };
            }
        }
        finally
        {
            EditorSceneManager.CloseScene(source, true);
            SceneManager.SetActiveScene(targetScene);
        }
    }

    private static void CreateProps()
    {
        var parent = new GameObject("Props palette left and world pixels right").transform;
        var paths = new[] { "Assets/DeadSurvivor/Art/Environment/Props/tree045/tree045.fbx", "Assets/DeadSurvivor/Art/Environment/Props/boulder045/boulder045.fbx", "Assets/DeadSurvivor/Scenes/Proto045/Art_s2/Tripo/stone045.fbx" };
        for (var side = 0; side < 2; side++)
        {
            for (var i = 0; i < 7; i++)
            {
                var type = i % 3;
                var instance = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(paths[type]), parent);
                instance.name = $"{(side == 0 ? "Palette" : "WorldPixels")}_{i}";
                instance.transform.position = new Vector3((side == 0 ? -1 : 1) * (4.5f + i % 2 * 1.6f), 0f, -4f + i * 1.8f);
                instance.transform.localScale *= 1.25f;
                instance.transform.rotation = Quaternion.Euler(0, i * 37, 0) * instance.transform.rotation;
                var bounds = instance.GetComponentsInChildren<Renderer>()[0].bounds;
                foreach (var childRenderer in instance.GetComponentsInChildren<Renderer>())
                {
                    bounds.Encapsulate(childRenderer.bounds);
                }
                instance.transform.position -= Vector3.up * bounds.min.y;
                foreach (var renderer in instance.GetComponentsInChildren<MeshRenderer>())
                {
                    var material = MaterialAsset($"{(side == 0 ? "Palette" : "World")}_{type}", "HD2D/PixelSurface");
                    var original = AssetDatabase.LoadAssetAtPath<Material>("Assets/DeadSurvivor/Art/Environment/Map/" + (type == 0 ? "Foliage045s2.mat" : "Stone045s2.mat"));
                    var baselinePath = ArtPath + "/Baseline_" + type + ".mat";
                    if (AssetDatabase.LoadAssetAtPath<Material>(baselinePath) == null)
                    {
                        AssetDatabase.CreateAsset(new Material(original), baselinePath);
                    }
                    material.SetTexture("_MainTex", CreatePaletteTexture((Texture2D)original.GetTexture("_Palette")));
                    material.SetTexture("_WorldTex", AssetDatabase.LoadAssetAtPath<Texture2D>(ArtPath + "/GrassSquare.png"));
                    material.SetColor("_BaseColor", Color.white);
                    material.SetFloat("_WorldTexture", side);
                    renderer.sharedMaterial = material;
                    renderer.shadowCastingMode = ShadowCastingMode.On;
                }
            }
        }
    }

    private static Texture2D CreatePaletteTexture(Texture2D source)
    {
        var path = ArtPath + "/WarmPalette.png";
        var existing = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        if (existing != null)
        {
            return existing;
        }
        var target = RenderTexture.GetTemporary(source.width, source.height, 0);
        var previous = RenderTexture.active;
        Graphics.Blit(source, target);
        RenderTexture.active = target;
        var texture = new Texture2D(source.width, source.height, TextureFormat.RGBA32, false);
        texture.ReadPixels(new Rect(0, 0, source.width, source.height), 0, 0);
        RenderTexture.active = previous;
        RenderTexture.ReleaseTemporary(target);
        var pixels = texture.GetPixels();
        for (var i = 0; i < pixels.Length; i++)
        {
            Color.RGBToHSV(pixels[i], out var hue, out var saturation, out var value);
            pixels[i] = Color.HSVToRGB(hue > 0.16f && hue < 0.5f ? 0.155f : 0.105f, Mathf.Min(saturation, 0.42f), value);
        }
        texture.SetPixels(pixels);
        File.WriteAllBytes(path, texture.EncodeToPNG());
        Object.DestroyImmediate(texture);
        AssetDatabase.ImportAsset(path);
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.filterMode = FilterMode.Point;
        importer.mipmapEnabled = false;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    private static Texture2D CreateGroundTexture()
    {
        var source = AssetDatabase.LoadAssetAtPath<Texture2D>(SpritePath + "/Ground/grass/grass_01.png");
        var target = RenderTexture.GetTemporary(source.width, source.height, 0);
        Graphics.Blit(source, target);
        var previous = RenderTexture.active;
        RenderTexture.active = target;
        var readable = new Texture2D(source.width, source.height, TextureFormat.RGBA32, false);
        readable.ReadPixels(new Rect(0, 0, source.width, source.height), 0, 0);
        readable.Apply();
        RenderTexture.active = previous;
        RenderTexture.ReleaseTemporary(target);
        var texture = new Texture2D(48, 48, TextureFormat.RGBA32, false);
        for (var y = 0; y < 48; y++)
        {
            for (var x = 0; x < 48; x++)
            {
                // Inverse 2:1 diamond: (u,v) square -> diamond interior; repeat at 48 texels/m.
                var u = (x + 0.5f) / 48f;
                var v = (y + 0.5f) / 48f;
                var sx = Mathf.Clamp((int)((u - v + 1f) * 0.5f * source.width), 0, source.width - 1);
                var sy = Mathf.Clamp((int)((u + v) * 0.5f * source.height), 0, source.height - 1);
                var color = readable.GetPixel(sx, sy);
                if (color.a < 0.5f)
                {
                    color = Hex("a1985d");
                }
                color.a = 1f;
                texture.SetPixel(x, y, color);
            }
        }
        File.WriteAllBytes(ArtPath + "/GrassSquare.png", texture.EncodeToPNG());
        Object.DestroyImmediate(readable);
        Object.DestroyImmediate(texture);
        AssetDatabase.ImportAsset(ArtPath + "/GrassSquare.png");
        var importer = (TextureImporter)AssetImporter.GetAtPath(ArtPath + "/GrassSquare.png");
        importer.textureType = TextureImporterType.Default;
        importer.filterMode = FilterMode.Point;
        importer.wrapMode = TextureWrapMode.Repeat;
        importer.mipmapEnabled = false;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Texture2D>(ArtPath + "/GrassSquare.png");
    }

    private static ScriptableRendererData CreateRenderer()
    {
        var renderer = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(ArtPath + "/LookdevRenderer.asset");
        if (renderer != null)
        {
            return renderer;
        }
        var source = AssetDatabase.LoadAssetAtPath<UniversalRendererData>("Assets/Settings/Mobile_Renderer.asset");
        renderer = Object.Instantiate(source);
        renderer.name = "LookdevRenderer";
        renderer.rendererFeatures.Clear();
        AssetDatabase.CreateAsset(renderer, ArtPath + "/LookdevRenderer.asset");
        var feature = ScriptableObject.CreateInstance<FullScreenPassRendererFeature>();
        feature.name = "Gentle top and bottom blur";
        feature.passMaterial = MaterialAsset("ScreenBandBlur", "HD2D/ScreenBandBlur");
        feature.fetchColorBuffer = true;
        feature.injectionPoint = FullScreenPassRendererFeature.InjectionPoint.AfterRenderingPostProcessing;
        AssetDatabase.AddObjectToAsset(feature, renderer);
        renderer.rendererFeatures.Add(feature);
        feature.Create();
        renderer.SetDirty();
        return renderer;
    }

    private static VolumeProfile CreateProfile()
    {
        var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(ArtPath + "/LookdevVolume.asset");
        if (profile != null)
        {
            return profile;
        }
        profile = ScriptableObject.CreateInstance<VolumeProfile>();
        AssetDatabase.CreateAsset(profile, ArtPath + "/LookdevVolume.asset");
        var bloom = profile.Add<Bloom>(true);
        bloom.threshold.value = 1.2f;
        bloom.intensity.value = 0.25f;
        bloom.filter.value = BloomFilterMode.Dual;
        bloom.downscale.value = BloomDownscaleMode.Quarter;
        bloom.maxIterations.value = 3;
        bloom.highQualityFiltering.value = false;
        profile.Add<Tonemapping>(true).mode.value = TonemappingMode.Neutral;
        var vignette = profile.Add<Vignette>(true);
        vignette.intensity.value = 0.2f;
        vignette.smoothness.value = 0.5f;
        profile.Add<ColorAdjustments>(true).saturation.value = -3f;
        foreach (var component in profile.components)
        {
            AssetDatabase.AddObjectToAsset(component, profile);
        }
        return profile;
    }

    private static Material MaterialAsset(string name, string shader)
    {
        var path = ArtPath + "/" + name + ".mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = new Material(Shader.Find(shader)) { name = name };
            AssetDatabase.CreateAsset(material, path);
        }
        return material;
    }

    private static Color Hex(string value)
    {
        ColorUtility.TryParseHtmlString("#" + value, out var color);
        return color;
    }
}
