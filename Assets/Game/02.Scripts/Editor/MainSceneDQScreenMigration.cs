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

// Idempotent production migration. Never rebuild Main or copy another worktree's scene.
public static class MainSceneDQScreenMigration
{
    public const string ScenePath = "Assets/Game/01.Scene/Main.unity";
    public const string RendererPath = "Assets/Game/03.Resources/Settings/DQ_MainRenderer.asset";
    public const string ApronPath = "Assets/Game/03.Resources/Models/HD2D/DQ_VisualApron.asset";
    private const string SettingsPath = "Assets/Settings/";
    private static readonly string[] Prefabs = { "MonsterView", "ProjectileView", "EnemyShotView", "BurstView", "DamageNumberView", "MonsterHpBarView" };

    [MenuItem("DeadSurvivor/DQ/Apply Main screen")]
    public static void Apply() => ApplyCore(null);

    public static void ApplyForFailureTest(string checkpoint) => ApplyCore(checkpoint);

    private static void ApplyCore(string failAfter)
    {
        if (Application.isPlaying || EditorApplication.isCompiling)
        {
            throw new InvalidOperationException("[MainSceneDQScreenMigration] Requires ready Edit mode.");
        }
        var previousScene = EditorSceneManager.GetActiveScene();
        var previousScenePath = previousScene.path;
        if (previousScene.isDirty)
        {
            throw new InvalidOperationException("[MainSceneDQScreenMigration] Open scene has unsaved changes.");
        }
        var targets = Prefabs.Select(PrefabPath).Concat(new[] { ScenePath, RendererPath, ApronPath,
            SettingsPath + "Mobile_RPAsset.asset", SettingsPath + "PC_RPAsset.asset" }).ToArray();
        var before = targets.Concat(targets.Select(p => p + ".meta"))
            .ToDictionary(p => p, p => File.Exists(p) ? File.ReadAllBytes(p) : null);
        try
        {
            var renderer = BuildRenderer();
            foreach (var prefix in new[] { "Mobile", "PC" })
            {
                var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(SettingsPath + prefix + "_RPAsset.asset");
                var serialized = new SerializedObject(pipeline);
                var list = serialized.FindProperty("m_RendererDataList");
                list.GetArrayElementAtIndex(0).objectReferenceValue = renderer;
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }
            foreach (var name in Prefabs)
            {
                var path = PrefabPath(name);
                var prefab = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    var upright = prefab.transform.Find("Upright");
                    if (upright != null)
                    {
                        upright.localRotation = Quaternion.Euler(18, 0, 0);
                        upright.localPosition = name == "ProjectileView" || name == "EnemyShotView"
                            ? TiltedViewMath.UprightRotation(18) * (Vector3.up * .5f) : Vector3.zero;
                    }
                    if (failAfter == "prefabs" && upright != null) upright.localPosition += Vector3.right;
                    PrefabUtility.SaveAsPrefabAsset(prefab, path);
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(prefab);
                }
            }
            if (failAfter == "prefabs") throw new InvalidOperationException("Injected after prefab save");
            var scene = EditorSceneManager.OpenScene(ScenePath);
            if (failAfter == "opened") throw new InvalidOperationException("Injected after scene open");
            var camera = Camera.main;
            foreach (var component in camera.GetComponents<Component>())
            {
                if (component.GetType().Name == "PixelPerfectCamera")
                {
                    Object.DestroyImmediate(component);
                }
            }
            var oldBackground = camera.GetComponent<HD2DBackgroundRig>();
            if (oldBackground != null)
            {
                Object.DestroyImmediate(oldBackground);
            }
            camera.cullingMask |= 1 << HD2DBackgroundRig.BackgroundLayer;
            // Indicators must enter camera culling; renderer filters draw them only after focus.
            camera.cullingMask |= 1 << 26;
            var rules = new ResourceService().LoadJson<ViewRulesData>("Data/View/view_rules");
            var rig = camera.GetComponent<TiltedCameraRig>();
            if (rig == null)
            {
                rig = camera.gameObject.AddComponent<TiltedCameraRig>();
            }
            rig.Initialize(rules.TiltDegrees, rules.Distance, rules.NearClip, rules.FarClip, rules.TargetFraction, rules.MinScale);
            rig.SetFocus(Vector3.zero);
            var data = camera.GetUniversalAdditionalCameraData();
            data.SetRenderer(0);
            data.renderPostProcessing = true;
            data.antialiasing = AntialiasingMode.None;
            camera.allowMSAA = false;
            foreach (var member in Object.FindObjectsByType<MemberView>(FindObjectsSortMode.None))
            {
                var upright = member.transform.Find("Upright");
                upright.localRotation = Quaternion.Euler(18, 0, 0);
                upright.localPosition = Vector3.zero;
            }
            BuildApron();
            // Saving a scene can also persist dirty referenced assets in this Editor version.
            // Preserve their in-memory edits/dirty flags while saving only declared outputs.
            var unrelatedDirty = Resources.FindObjectsOfTypeAll<Object>()
                .Where(o => AssetDatabase.Contains(o) && EditorUtility.IsDirty(o)
                    && !targets.Contains(AssetDatabase.GetAssetPath(o))).ToArray();
            foreach (var asset in unrelatedDirty) EditorUtility.ClearDirty(asset);
            try
            {
                if (failAfter == "saved") camera.fieldOfView = 99; // Prove bytes really roll back.
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                foreach (var path in targets.Where(p => p != ScenePath))
                {
                    var asset = AssetDatabase.LoadMainAssetAtPath(path);
                    if (asset != null) AssetDatabase.SaveAssetIfDirty(asset);
                }
            }
            finally
            {
                foreach (var asset in unrelatedDirty) if (asset != null) EditorUtility.SetDirty(asset);
            }
            if (failAfter == "saved") throw new InvalidOperationException("Injected after target save");
            if (Camera.main.orthographic || Camera.main.GetComponent<HD2DBackgroundRig>() != null)
            {
                throw new InvalidOperationException("[MainSceneDQScreenMigration] Projection wiring did not persist.");
            }
        }
        catch
        {
            // Only declared migration outputs are restored. Other sessions' files are untouched.
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
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            foreach (var path in targets.Where(File.Exists))
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
            EditorSceneManager.OpenScene(previousScenePath);
            throw;
        }
    }

    private static string PrefabPath(string name) => "Assets/Game/03.Resources/Resources/Prefab/" + name + ".prefab";

    private static UniversalRendererData BuildRenderer()
    {
        var renderer = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(RendererPath);
        if (renderer != null)
        {
            return renderer;
        }
        var source = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(SettingsPath + "Mobile_Renderer.asset");
        renderer = Object.Instantiate(source);
        renderer.name = "DQ_MainRenderer";
        renderer.rendererFeatures.Clear();
        AssetDatabase.CreateAsset(renderer, RendererPath);
        foreach (var feature in source.rendererFeatures)
        {
            if (feature is HD2DBackgroundFeature)
            {
                continue;
            }
            var copy = Object.Instantiate(feature);
            copy.name = feature.name;
            AssetDatabase.AddObjectToAsset(copy, renderer);
            renderer.rendererFeatures.Add(copy);
        }
        var serialized = new SerializedObject(renderer);
        serialized.FindProperty("m_OpaqueLayerMask").intValue |= 1 << HD2DBackgroundRig.BackgroundLayer;
        serialized.FindProperty("m_TransparentLayerMask").intValue |= 1 << HD2DBackgroundRig.BackgroundLayer;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(renderer);
        return renderer;
    }

    private static void BuildApron()
    {
        // Adopted stage-6 geometry is authoritative. Height data changed after its original bake;
        // re-running a screen migration must not silently re-bake background geometry.
        if (AssetDatabase.LoadAssetAtPath<Mesh>(ApronPath) != null) return;
        var field = Object.FindFirstObjectByType<FieldView>();
        var serialized = new SerializedObject(field);
        var material = (Material)serialized.FindProperty("groundMaterial").objectReferenceValue;
        var root = GameObject.Find("DQ render-only outer floor");
        if (root == null)
        {
            root = new GameObject("DQ render-only outer floor", typeof(MeshFilter), typeof(MeshRenderer));
        }
        root.layer = HD2DBackgroundRig.BackgroundLayer;
        root.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        var heights = new ResourceService().LoadJson<FieldHeightData>("Data/Field/field_height");
        var grid = TerrainHeightGrid.FromMillimetres(heights.cell, heights.originX, heights.originZ, heights.width, heights.depth, heights.heightsMm);
        // Render-only extension under the existing terrain. Rebuilt from current height data.
        var generated = GroundDecalMath.Build(grid, new Vector3(0, 0, 20), new Vector2(220, 240), 0, grid.Cell, -.025f);
        generated.name = "DQ visual coverage only";
        var layout = new ResourceService().LoadJson<FieldLayoutData>("Data/Field/field_layout");
        var vertices = generated.vertices;
        var outerTriangles = new List<int>();
        var triangles = generated.triangles;
        for (var i = 0; i < triangles.Length; i += 3)
        {
            var center = (vertices[triangles[i]] + vertices[triangles[i + 1]] + vertices[triangles[i + 2]]) / 3f;
            if (Mathf.Abs(center.x) + Mathf.Abs(center.z) > layout.groundHalfDiagonal + .001f)
            {
                outerTriangles.Add(triangles[i]);
                outerTriangles.Add(triangles[i + 1]);
                outerTriangles.Add(triangles[i + 2]);
            }
        }
        // Surface shader depth uses the actual local terrain reference, including outside the grid.
        generated.uv2 = vertices.Select(v => new Vector2(v.y + .025f, 0)).ToArray();
        generated.SetTriangles(outerTriangles, 0);
        var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(ApronPath);
        if (mesh == null)
        {
            mesh = generated;
            AssetDatabase.CreateAsset(mesh, ApronPath);
        }
        else
        {
            mesh.Clear();
            mesh.indexFormat = generated.indexFormat;
            mesh.vertices = generated.vertices;
            mesh.uv = generated.uv;
            mesh.uv2 = generated.uv2;
            mesh.normals = generated.normals;
            mesh.triangles = generated.triangles;
            mesh.RecalculateBounds();
            mesh.UploadMeshData(false);
            Object.DestroyImmediate(generated);
            EditorUtility.SetDirty(mesh);
        }
        root.GetComponent<MeshFilter>().sharedMesh = mesh;
        var renderer = root.GetComponent<MeshRenderer>();
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
    }
}
