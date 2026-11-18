using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Tilemaps;

// Reproducible migration of the game scene, leaving the lookdev scenes untouched.
public static class MainSceneTiltedViewMigration
{
    private const string MaterialRoot = "Assets/Game/03.Resources/Materials/HD2D/HD2D_";
    [MenuItem("DeadSurvivor/HD2D/Migrate Main View")]
    public static void Apply()
    {
        if (Application.isPlaying) throw new InvalidOperationException("Migrate Main in Edit Mode.");
        var scene = EditorSceneManager.OpenScene("Assets/Game/01.Scene/Main.unity");
        var rules = new ResourceService().LoadJson<ViewRulesData>("Data/View/view_rules");
        var camera = Camera.main;

        foreach (var component in camera.GetComponents<Component>())
            if (component.GetType().Name == "PixelPerfectCamera") UnityEngine.Object.DestroyImmediate(component);
        var rig = camera.GetComponent<TiltedCameraRig>();
        if (rig == null) rig = camera.gameObject.AddComponent<TiltedCameraRig>();
        rig.Initialize(rules.TiltDegrees, rules.Distance, rules.NearClip, rules.FarClip, rules.TargetFraction, rules.MinScale);
        var cameraData = camera.GetUniversalAdditionalCameraData();
        cameraData.SetRenderer(0); cameraData.renderPostProcessing = true; cameraData.antialiasing = AntialiasingMode.None;
        camera.allowMSAA = false;
        var field = UnityEngine.Object.FindFirstObjectByType<FieldView>();
        var grid = field.Tilemap.GetComponentInParent<Grid>();
        grid.transform.rotation = Quaternion.Euler(90,0,0); grid.cellSize = Vector3.one;
        field.Tilemap.GetComponent<TilemapRenderer>().sharedMaterial = Material("SpriteLit");
        foreach (var member in UnityEngine.Object.FindObjectsByType<MemberView>(FindObjectsSortMode.None))
        {
            var upright = member.transform.Find("Upright");
            if (upright == null) { upright = new GameObject("Upright").transform; upright.SetParent(member.transform,false); }
            var so = new SerializedObject(member);
            so.FindProperty("shadowMaterial").objectReferenceValue = Material("SpriteFade"); so.ApplyModifiedPropertiesWithoutUndo();
            foreach (var fieldName in new[] {"bodyRenderer","hpBackRenderer","hpFillRenderer"})
            {
                var renderer = (SpriteRenderer)so.FindProperty(fieldName).objectReferenceValue;
                renderer.transform.SetParent(upright,false); renderer.sharedMaterial = Material("SpriteLit");
            }
            ((SpriteRenderer)so.FindProperty("flashRenderer").objectReferenceValue).sharedMaterial = Material("SpriteFlash");
            upright.localRotation = TiltedViewMath.UprightRotation(rules.TiltDegrees);
        }
        var sunObject = GameObject.Find("HD2D Sun");
        if (sunObject == null) sunObject = new GameObject("HD2D Sun",typeof(Light));
        sunObject.transform.rotation = Quaternion.Euler(32,135,0);
        var sun = sunObject.GetComponent<Light>(); sun.type = LightType.Directional; sun.shadows = LightShadows.Hard;
        sun.shadowBias = .05f; sun.shadowNormalBias = .02f; sun.intensity = 1;
        RenderSettings.sun = sun; RenderSettings.ambientMode = AmbientMode.Flat; RenderSettings.ambientLight = new Color(.43f,.32f,.27f);
        var volumeObject = GameObject.Find("HD2D Battle Volume");
        if (volumeObject == null) volumeObject = new GameObject("HD2D Battle Volume",typeof(Volume));
        var volume = volumeObject.GetComponent<Volume>(); volume.isGlobal = true;
        volume.sharedProfile = AssetDatabase.LoadAssetAtPath<VolumeProfile>("Assets/Game/03.Resources/Settings/HD2D_BattleVolume.asset");
        EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
        foreach(var guid in AssetDatabase.FindAssets("t:Prefab",new[]{"Assets/Game/03.Resources"}))
        {
            var path=AssetDatabase.GUIDToAssetPath(guid);
            if(!path.EndsWith("MonsterView.prefab")) continue;
            var prefab=PrefabUtility.LoadPrefabContents(path);
            var so = new SerializedObject(prefab.GetComponent<MonsterView>());
            so.FindProperty("shadowMaterial").objectReferenceValue = Material("SpriteFade"); so.ApplyModifiedPropertiesWithoutUndo();
            var body=(SpriteRenderer)so.FindProperty("bodyRenderer").objectReferenceValue;
            var upright=prefab.transform.Find("Upright");
            if(upright==null) {upright=new GameObject("Upright").transform; upright.SetParent(prefab.transform,false);}
            body.transform.SetParent(upright,false); upright.localRotation=TiltedViewMath.UprightRotation(rules.TiltDegrees); body.sharedMaterial=Material("SpriteLit");
            PrefabUtility.SaveAsPrefabAsset(prefab,path); PrefabUtility.UnloadPrefabContents(prefab);
        }
    }
    private static Material Material(string name) => AssetDatabase.LoadAssetAtPath<Material>(MaterialRoot+name+".mat");
}
