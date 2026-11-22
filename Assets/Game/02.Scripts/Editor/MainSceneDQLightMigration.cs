using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

// All authored scene/material/prefab changes for the approved DQ lighting stage live here.
public static class MainSceneDQLightMigration
{
    public const string Root = "Assets/Game/03.Resources/Materials/HD2D";
    public const string BlobPath = Root + "/DQ_BlobShadow.mat";
    public const string ProfilesPath = Root + "/DQ_CharacterLightProfiles.asset";
    public const string BodyPath = Root + "/DQ_CharacterLit.mat";
    public const string BlurPath = Root + "/DQ_BandBlur.mat";
    public const string TexturePath = "Assets/Game/03.Resources/Art/HD2D/DQAppliedCharacters/FootSoft.png";
    public const string VolumePath = "Assets/Game/03.Resources/Settings/DQ_LightVolume.asset";
    private const string MonsterPath = "Assets/Game/03.Resources/Resources/Prefab/MonsterView.prefab";
    private const string DustPath = "Assets/Game/03.Resources/Resources/Prefab/HD2DAmbientDust.prefab";

    public static string[] Targets => new[] { MainSceneDQScreenMigration.ScenePath, MainSceneDQScreenMigration.RendererPath,
        BlobPath, BodyPath, BlurPath, TexturePath, VolumePath, MonsterPath, DustPath, ProfilesPath,
        Root + "/DQ_CharacterLit_bruno.mat", Root + "/DQ_CharacterLit_lena.mat", Root + "/DQ_CharacterLit_mira.mat", Root + "/DQ_CharacterLit_bolt.mat",
        "Assets/Settings/PC_RPAsset.asset", "Assets/Settings/Mobile_RPAsset.asset" };

    [MenuItem("DeadSurvivor/DQ/Apply Main light")]
    public static void Apply() => ApplyCore(false);
    public static void ApplyForFailureTest() => ApplyCore(true);

    private static void ApplyCore(bool injectFailure)
    {
        if (Application.isPlaying || EditorApplication.isCompiling || EditorSceneManager.GetActiveScene().isDirty)
        {
            throw new InvalidOperationException("DQ light requires saved Edit mode.");
        }
        var previous = EditorSceneManager.GetActiveScene().path;
        var before = Targets.Concat(Targets.Select(p => p + ".meta")).ToDictionary(p => p, p => File.Exists(p) ? File.ReadAllBytes(p) : null);
        try
        {
            BuildTexture();
            var blob = MaterialAt(BlobPath, "DeadSurvivor/DQ/Soft foot shadow");
            blob.SetTexture("_MainTex", AssetDatabase.LoadAssetAtPath<Texture2D>(TexturePath));
            var legacy = AssetDatabase.LoadAssetAtPath<Material>(Root + "/HD2D_FootShadow.mat");
            blob.SetTexture("_BankMask", legacy.GetTexture("_BankMask"));
            blob.SetVector("_FieldRect", new Vector4(-36, -36, 72, 72));
            blob.SetColor("_ShadowTint", new Color(.18f, .25f, .19f));
            blob.SetFloat("_Opacity", .65f);
            blob.renderQueue = 2990;
            var body = MaterialAt(BodyPath, "HD2D/HD2D_SpriteLit");
            body.SetFloat("_DQLighting", 1);
            body.SetFloat("_DQExposure", .95f);
            body.SetFloat("_ReceiveShadows", 1);
            body.SetFloat("_SpriteRenderer", 1);
            body.SetColor("_BaseColor", Color.white);
            // Full-alpha native Main body/floor measurements, not gameplay stats or source-pixel edits.
            var profiles = AssetDatabase.LoadAssetAtPath<DQCharacterLightProfiles>(ProfilesPath);
            if (profiles == null)
            {
                profiles = ScriptableObject.CreateInstance<DQCharacterLightProfiles>();
                AssetDatabase.CreateAsset(profiles, ProfilesPath);
            }
            var gains = new Dictionary<string, float> { { "bruno", 2.37f }, { "lena", 2.55f }, { "mira", 1.58f }, { "bolt", .78f } };
            profiles.Entries = gains.Select(pair =>
            {
                var material = MaterialAt(Root + "/DQ_CharacterLit_" + pair.Key + ".mat", "HD2D/HD2D_SpriteLit");
                material.CopyPropertiesFromMaterial(body);
                material.SetFloat("_DQExposure", pair.Value);
                EditorUtility.SetDirty(material);
                AssetDatabase.SaveAssetIfDirty(material);
                return new DQCharacterLightProfiles.Entry { ActorId = pair.Key, Material = material };
            }).ToArray();
            EditorUtility.SetDirty(profiles);
            AssetDatabase.SaveAssetIfDirty(profiles);
            // The four-tap shader requires rectangular, unrotated atlas cells.
            var catalog = JsonUtility.FromJson<DQClipCatalog>(File.ReadAllText(MainSceneDQCharactersMigration.DataPath));
            foreach (var actor in catalog.actors)
            {
                var importer = (TextureImporter)AssetImporter.GetAtPath(actor.sheet);
                var settings = new TextureImporterSettings();
                importer.ReadTextureSettings(settings);
                var atlasImporter = (UnityEditor.U2D.SpriteAtlasImporter)AssetImporter.GetAtPath(MainSceneDQCharactersMigration.AtlasFolder + "/" + actor.actorId + ".spriteatlasv2");
                if (settings.spriteMeshType != SpriteMeshType.FullRect || atlasImporter == null || atlasImporter.packingSettings.enableRotation || atlasImporter.packingSettings.enableTightPacking)
                {
                    throw new InvalidOperationException("DQ light requires FullRect/unrotated atlas: " + actor.actorId);
                }
            }
            var blur = MaterialAt(BlurPath, "HD2D/HD2D_BandBlur");
            blur.SetFloat("_DQDepthFocus", 1);
            blur.SetFloat("_BlurRadius", 5);
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(VolumePath);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<VolumeProfile>();
                AssetDatabase.CreateAsset(profile, VolumePath);
            }
            var vignette = Override<Vignette>(profile);
            vignette.intensity.Override(0);
            // Keep the override enabled: disabling this component falls back to default-volume intensity .2.
            vignette.active = true;
            var bloom = Override<Bloom>(profile);
            bloom.threshold.Override(1.4f);
            bloom.intensity.Override(.1f);
            bloom.scatter.Override(.4f);
            var tone = Override<Tonemapping>(profile);
            tone.mode.Override(TonemappingMode.None);
            var grading = Override<ColorAdjustments>(profile);
            grading.postExposure.Override(0);
            grading.saturation.Override(0);
            grading.colorFilter.Override(Color.white);
            foreach (var component in profile.components)
            {
                if (!AssetDatabase.Contains(component))
                {
                    AssetDatabase.AddObjectToAsset(component, profile);
                }
                EditorUtility.SetDirty(component);
            }
            EditorUtility.SetDirty(profile);
            foreach (var prefix in new[] { "PC", "Mobile" })
            {
                var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>("Assets/Settings/" + prefix + "_RPAsset.asset");
                var serialized = new SerializedObject(pipeline);
                serialized.FindProperty("m_SoftShadowsSupported").boolValue = true;
                serialized.FindProperty("m_ShadowDistance").floatValue = 80;
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }
            var renderer = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(MainSceneDQScreenMigration.RendererPath);
            var focus = renderer.rendererFeatures.OfType<FullScreenPassRendererFeature>().Single();
            focus.requirements = ScriptableRenderPassInput.Depth;
            focus.fetchColorBuffer = true;
            focus.passMaterial = blur;
            focus.injectionPoint = FullScreenPassRendererFeature.InjectionPoint.AfterRenderingPostProcessing;
            renderer.rendererFeatures.Remove(focus);
            renderer.rendererFeatures.Insert(0, focus);
            // Force copy after opaque surfaces and before the transparent contact shadows.
            var rendererSerialized = new SerializedObject(renderer);
            rendererSerialized.FindProperty("m_CopyDepthMode").enumValueIndex = 0;
            rendererSerialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(focus);
            EditorUtility.SetDirty(renderer);
            var monster = PrefabUtility.LoadPrefabContents(MonsterPath);
            try
            {
                SetReference(monster.GetComponent<MonsterView>(), "dqBlobShadowMaterial", blob);
                SetBodyMaterials(monster, body);
                PrefabUtility.SaveAsPrefabAsset(monster, MonsterPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(monster);
            }
            var dust = PrefabUtility.LoadPrefabContents(DustPath);
            try
            {
                var particles = dust.GetComponent<ParticleSystem>();
                var main = particles.main;
                main.maxParticles = 16;
                main.startSize = .035f;
                main.startColor = new Color(1, .92f, .63f, .65f);
                var emission = particles.emission;
                emission.rateOverTime = 1.4f;
                PrefabUtility.SaveAsPrefabAsset(dust, DustPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(dust);
            }
            var scene = EditorSceneManager.OpenScene(MainSceneDQScreenMigration.ScenePath);
            // Opening a scene can unload newly-created assets held only by local managed refs.
            // Reload after the transition, then validate the persisted scene references below.
            blob = AssetDatabase.LoadAssetAtPath<Material>(BlobPath);
            body = AssetDatabase.LoadAssetAtPath<Material>(BodyPath);
            profiles = AssetDatabase.LoadAssetAtPath<DQCharacterLightProfiles>(ProfilesPath);
            profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(VolumePath);
            var camera = Camera.main;
            if (camera.GetComponent<DQSceneLighting>() == null)
            {
                camera.gameObject.AddComponent<DQSceneLighting>();
            }
            camera.GetUniversalAdditionalCameraData().requiresDepthTexture = true;
            camera.GetUniversalAdditionalCameraData().renderPostProcessing = true;
            var lights = Object.FindObjectsByType<Light>(FindObjectsSortMode.None).Where(l => l.type == LightType.Directional).ToArray();
            if (lights.Length != 1)
            {
                throw new InvalidOperationException("Main must have exactly one directional sun.");
            }
            var sun = lights[0];
            sun.transform.rotation = Quaternion.Euler(48, 35, 0);
            sun.intensity = 1.05f;
            sun.color = new Color(1, .96f, .86f);
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = 1;
            sun.shadowBias = .03f;
            sun.shadowNormalBias = .1f;
            RenderSettings.sun = sun;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(.035f, .09f, .065f);
            RenderSettings.ambientIntensity = 1;
            foreach (var volume in Object.FindObjectsByType<Volume>(FindObjectsSortMode.None))
            {
                volume.sharedProfile = profile;
                volume.isGlobal = true;
                volume.weight = 1;
            }
            SetReference(Object.FindFirstObjectByType<FieldView>(), "dqBlobShadowMaterial", blob);
            foreach (var member in Object.FindObjectsByType<MemberView>(FindObjectsSortMode.None))
            {
                SetReference(member, "dqBlobShadowMaterial", blob);
                SetReference(member, "dqLightProfiles", profiles);
                SetBodyMaterials(member.gameObject, body);
            }
            foreach (var asset in new Object[] { blob, body, blur })
            {
                EditorUtility.SetDirty(asset);
                AssetDatabase.SaveAssetIfDirty(asset);
            }
            AssetDatabase.SaveAssetIfDirty(profile);
            AssetDatabase.SaveAssetIfDirty(focus);
            AssetDatabase.SaveAssetIfDirty(renderer);
            foreach (var path in Targets.Where(p => p.EndsWith("RPAsset.asset")))
            {
                AssetDatabase.SaveAssetIfDirty(AssetDatabase.LoadAssetAtPath<Object>(path));
            }
            if (injectFailure)
            {
                throw new InvalidOperationException("Injected DQ light rollback test.");
            }
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene))
            {
                throw new InvalidOperationException("Could not save Main light migration.");
            }
            // Unity writes an empty name with a trailing space for the newly added component.
            var sceneText = File.ReadAllText(MainSceneDQScreenMigration.ScenePath);
            sceneText = sceneText.Replace("  m_Name: \n  m_EditorClassIdentifier: HD2D.View::DQSceneLighting",
                "  m_Name:\n  m_EditorClassIdentifier: HD2D.View::DQSceneLighting");
            File.WriteAllText(MainSceneDQScreenMigration.ScenePath, sceneText);
            EditorSceneManager.OpenScene(MainSceneDQScreenMigration.ScenePath);
            var savedProfiles = AssetDatabase.LoadAssetAtPath<DQCharacterLightProfiles>(ProfilesPath);
            var savedBlob = AssetDatabase.LoadAssetAtPath<Material>(BlobPath);
            var savedMembers = Object.FindObjectsByType<MemberView>(FindObjectsSortMode.None);
            if (savedMembers.Length != 4 || savedProfiles == null || savedBlob == null)
            {
                throw new InvalidOperationException("DQ light saved scene is missing its four members or shared assets.");
            }
            foreach (var member in savedMembers)
            {
                var serialized = new SerializedObject(member);
                if (serialized.FindProperty("dqLightProfiles").objectReferenceValue != savedProfiles
                    || serialized.FindProperty("dqBlobShadowMaterial").objectReferenceValue != savedBlob)
                {
                    throw new InvalidOperationException("DQ light reference did not survive save/reopen: " + member.name);
                }
            }
            if (Object.FindFirstObjectByType<FieldView>().TerrainShadowMaterial != savedBlob)
            {
                throw new InvalidOperationException("DQ field material supplier lost the shared blob asset.");
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
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
            EditorSceneManager.OpenScene(previous);
            throw;
        }
    }

    private static T Override<T>(VolumeProfile profile) where T : VolumeComponent
    {
        return profile.TryGet<T>(out var value) ? value : profile.Add<T>(true);
    }

    private static Material MaterialAt(string path, string shaderName)
    {
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        var shader = Shader.Find(shaderName);
        if (shader == null)
        {
            throw new InvalidOperationException("Missing shader " + shaderName);
        }
        if (material == null)
        {
            material = new Material(shader);
            AssetDatabase.CreateAsset(material, path);
        }
        material.shader = shader;
        material.enableInstancing = true;
        return material;
    }

    private static void SetReference(Object target, string name, Object value)
    {
        var serialized = new SerializedObject(target);
        serialized.FindProperty(name).objectReferenceValue = value;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void SetBodyMaterials(GameObject root, Material material)
    {
        foreach (var renderer in root.GetComponentsInChildren<SpriteRenderer>(true))
        {
            if (renderer.sharedMaterial != null && renderer.sharedMaterial.shader.name == "HD2D/HD2D_SpriteLit")
            {
                renderer.sharedMaterial = material;
            }
        }
    }

    private static void BuildTexture()
    {
        var texture = new Texture2D(64, 64, TextureFormat.RGBA32, false, true);
        try
        {
            var pixels = new Color[4096];
            for (var y = 0; y < 64; y++)
            {
                for (var x = 0; x < 64; x++)
                {
                    var radius = new Vector2((x + .5f) / 32 - 1, (y + .5f) / 32 - 1).magnitude;
                    pixels[y * 64 + x] = new Color(1, 1, 1, 1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.25f, 1, radius)));
                }
            }
            texture.SetPixels(pixels);
            texture.Apply();
            File.WriteAllBytes(TexturePath, texture.EncodeToPNG());
        }
        finally
        {
            Object.DestroyImmediate(texture);
        }
        AssetDatabase.ImportAsset(TexturePath, ImportAssetOptions.ForceSynchronousImport);
        var importer = (TextureImporter)AssetImporter.GetAtPath(TexturePath);
        importer.textureType = TextureImporterType.Default;
        importer.filterMode = FilterMode.Bilinear;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.mipmapEnabled = false;
        importer.sRGBTexture = false;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.SaveAndReimport();
    }
}
