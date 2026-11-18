using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;

// Portable production wiring of the adopted F8 background and the W3 sprite cards.
public static class MainSceneHD2DEffectsMigration
{
    private const string Settings = "Assets/Game/03.Resources/Settings/";
    private const string Materials = "Assets/Game/03.Resources/Materials/HD2D/HD2D_";

    [MenuItem("DeadSurvivor/HD2D/Migrate Main Effects")]
    public static void Apply()
    {
        if (Application.isPlaying)
        {
            throw new InvalidOperationException("Migrate Main in Edit Mode.");
        }
        var scene = EditorSceneManager.OpenScene("Assets/Game/01.Scene/Main.unity");
        foreach (var name in new[] { "BackgroundOutline", "BackgroundComposite", "BackgroundDepth" })
        {
            if (AssetDatabase.LoadAssetAtPath<Material>(Materials + name + ".mat") == null)
            {
                AssetDatabase.CreateAsset(new Material(Shader.Find("HD2D/HD2D_" + name)), Materials + name + ".mat");
            }
        }
        // r6.1 keeps the painted monster values stable under moving character shadows.
        // This is a shared M1 variant (same shader/state), not a per-monster material instance/block.
        var monsterMaterial = AssetDatabase.LoadAssetAtPath<Material>(Materials + "MonsterLit.mat");
        if (monsterMaterial == null)
        {
            monsterMaterial = new Material(Material("SpriteLit")) { name = "HD2D_MonsterLit" };
            AssetDatabase.CreateAsset(monsterMaterial, Materials + "MonsterLit.mat");
        }
        monsterMaterial.SetFloat("_ReceiveShadows", 0f);
        EditorUtility.SetDirty(monsterMaterial);
        var background = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(Settings + "HD2D_BackgroundRenderer.asset");
        if (background == null)
        {
            background = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<UniversalRendererData>("Assets/Settings/Mobile_Renderer.asset"));
            background.name = "HD2D_BackgroundRenderer";
            background.rendererFeatures.Clear();
            AssetDatabase.CreateAsset(background, Settings + "HD2D_BackgroundRenderer.asset");
        }
        AddBackgroundFeature(background, true);
        foreach (var prefix in new[] { "Mobile", "PC" })
        {
            var native = AssetDatabase.LoadAssetAtPath<UniversalRendererData>($"Assets/Settings/{prefix}_Renderer.asset");
            AddBackgroundFeature(native, false);
            var pipeline = new SerializedObject(AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>($"Assets/Settings/{prefix}_RPAsset.asset"));
            var renderers = pipeline.FindProperty("m_RendererDataList");
            renderers.arraySize = 3;
            renderers.GetArrayElementAtIndex(2).objectReferenceValue = background;
            pipeline.ApplyModifiedPropertiesWithoutUndo();
        }
        var camera = Camera.main;
        var rig = camera.GetComponent<HD2DBackgroundRig>();
        if (rig == null)
        {
            rig = camera.gameObject.AddComponent<HD2DBackgroundRig>();
        }
        Set(rig, "backgroundRendererIndex", 2);
        UnityEngine.Object.FindFirstObjectByType<FieldView>().Tilemap.gameObject.layer = HD2DBackgroundRig.BackgroundLayer;
        foreach (var member in UnityEngine.Object.FindObjectsByType<MemberView>(FindObjectsSortMode.None))
        {
            var so = new SerializedObject(member);
            var body = (SpriteRenderer)so.FindProperty("bodyRenderer").objectReferenceValue;
            var caster = body.GetComponent<UprightShadowCaster>();
            if (caster == null)
            {
                caster = body.gameObject.AddComponent<UprightShadowCaster>();
            }
            Set(member, "shadowCaster", caster);
            Set(member, "shadowCasterMaterial", Material("ShadowOnly"));
            foreach (var renderer in member.GetComponentsInChildren<SpriteRenderer>(true))
            {
                if (renderer == body || renderer.transform.IsChildOf(body.transform))
                {
                    continue;
                }
                renderer.sharedMaterial = Material("Indicator");
                renderer.gameObject.layer = 26;
            }
            var flash = (SpriteRenderer)so.FindProperty("flashRenderer").objectReferenceValue;
            flash.transform.localPosition = new Vector3(0f, 0f, -1f / Const.PixelsPerUnit);
        }
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        foreach (var name in new[] { "ProjectileView", "BurstView", "EnemyShotView", "DamageNumberView", "MonsterHpBarView", "MonsterView" })
        {
            var path = $"Assets/Game/03.Resources/Resources/Prefab/{name}.prefab";
            var prefab = PrefabUtility.LoadPrefabContents(path);
            if (name == "MonsterView")
            {
                var so = new SerializedObject(prefab.GetComponent<MonsterView>());
                ((SpriteRenderer)so.FindProperty("bodyRenderer").objectReferenceValue).sharedMaterial = monsterMaterial;
                foreach (var field in new[] { "dashLineRenderer", "blastAreaRenderer" })
                {
                    ((SpriteRenderer)so.FindProperty(field).objectReferenceValue).sharedMaterial = Material("SpriteFade");
                }
            }
            else
            {
                var upright = prefab.transform.Find("Upright");
                if (upright == null)
                {
                    upright = new GameObject("Upright").transform;
                    upright.SetParent(prefab.transform, false);
                }
                upright.localRotation = TiltedViewMath.UprightRotation(30f);
                var isIndicator = name == "DamageNumberView" || name == "MonsterHpBarView";
                foreach (var original in prefab.GetComponentsInChildren<SpriteRenderer>(true))
                {
                    var renderer = original;
                    if (original.transform == prefab.transform)
                    {
                        renderer = new GameObject("Body").AddComponent<SpriteRenderer>();
                        EditorUtility.CopySerialized(original, renderer);
                        foreach (var owner in prefab.GetComponents<MonoBehaviour>())
                        {
                            var serialized = new SerializedObject(owner);
                            var field = serialized.FindProperty("bodyRenderer");
                            if (field != null && field.objectReferenceValue == original)
                            {
                                field.objectReferenceValue = renderer;
                                serialized.ApplyModifiedPropertiesWithoutUndo();
                            }
                        }
                        UnityEngine.Object.DestroyImmediate(original);
                    }
                    renderer.transform.SetParent(upright, false);
                    renderer.sharedMaterial = Material(isIndicator ? "Indicator" : name == "BurstView" ? "SpriteFade" : "SpriteLit");
                    if (isIndicator)
                    {
                        renderer.gameObject.layer = 26;
                    }
                }
                if (name == "ProjectileView")
                {
                    Set(prefab.GetComponent<ProjectileView>(), "upright", upright);
                }
                if (name == "BurstView")
                {
                    Set(prefab.GetComponent<BurstView>(), "upright", upright);
                }
                if (name == "DamageNumberView")
                {
                    upright.gameObject.layer = 26;
                    Set(prefab.GetComponent<DamageNumberView>(), "upright", upright);
                    Set(prefab.GetComponent<DamageNumberView>(), "indicatorMaterial", Material("Indicator"));
                }
            }
            PrefabUtility.SaveAsPrefabAsset(prefab, path);
            PrefabUtility.UnloadPrefabContents(prefab);
        }
        var tags = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
        tags.FindProperty("layers").GetArrayElementAtIndex(HD2DBackgroundRig.BackgroundLayer).stringValue = "HD2DBackground";
        tags.ApplyModifiedPropertiesWithoutUndo();
        AssetDatabase.DeleteAsset("Assets/Game/03.Resources/Materials/SpriteFlashWhite.mat");
        AssetDatabase.SaveAssets();
    }

    private static void AddBackgroundFeature(UniversalRendererData renderer, bool isCapture)
    {
        HD2DBackgroundFeature feature = null;
        foreach (var item in renderer.rendererFeatures)
        {
            if (item is HD2DBackgroundFeature match)
            {
                feature = match;
            }
        }
        if (feature == null)
        {
            feature = ScriptableObject.CreateInstance<HD2DBackgroundFeature>();
            feature.name = isCapture ? "Capture point background" : "Compose point background";
            AssetDatabase.AddObjectToAsset(feature, renderer);
            renderer.rendererFeatures.Add(feature);
        }
        feature.captureBackground = isCapture;
        feature.outlineMaterial = Material("BackgroundOutline");
        feature.depthMaterial = Material("BackgroundDepth");
        feature.compositeMaterial = Material("BackgroundComposite");
        feature.Create();
        renderer.SetDirty();
        EditorUtility.SetDirty(feature);
        EditorUtility.SetDirty(renderer);
    }

    private static Material Material(string name) => AssetDatabase.LoadAssetAtPath<Material>(Materials + name + ".mat");
    private static void Set(UnityEngine.Object target, string field, UnityEngine.Object value)
    {
        var so = new SerializedObject(target);
        so.FindProperty(field).objectReferenceValue = value;
        so.ApplyModifiedPropertiesWithoutUndo();
    }
    private static void Set(UnityEngine.Object target, string field, int value)
    {
        var so = new SerializedObject(target);
        so.FindProperty(field).intValue = value;
        so.ApplyModifiedPropertiesWithoutUndo();
    }
}
