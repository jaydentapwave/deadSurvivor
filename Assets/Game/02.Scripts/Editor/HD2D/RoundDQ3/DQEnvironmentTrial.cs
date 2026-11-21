using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

// E1 is a reversible plate over the saved H1. Never calls builders that rewrite Q1/Q2 art.
public static class DQEnvironmentTrial
{
    public const string Art = "Assets/Game/03.Resources/Art/HD2D/RoundDQ3";
    public const string Output = "output/hd2d-dq/env-r1";
    private static readonly string[] Ids = { "dq_tree_round_a", "dq_tree_round_b_r2", "dq_tree_round_c", "dq_tree_light", "dq_bush_a_r2", "dq_bush_b", "dq_rock_big_a", "dq_rock_big_b_r2", "dq_rock_small_a_r2", "dq_rock_small_b_r2" };
    [Serializable] private sealed class MeshData { public float[] vertices, normals, uv; public int[] triangles; }
    private static void EditOnly()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("E1 requires Edit mode.");
        if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().path != DQLookdevBuilder.Scene) throw new InvalidOperationException("Open Lookdev_DQ without saving the previous scene first.");
    }
    private static T Save<T>(T value, string path) where T : Object
    {
        var old = AssetDatabase.LoadAssetAtPath<T>(path);
        if (old == null) { AssetDatabase.CreateAsset(value, path); return value; }
        EditorUtility.CopySerialized(value, old); Object.DestroyImmediate(value); EditorUtility.SetDirty(old); AssetDatabase.SaveAssetIfDirty(old); return old;
    }
    public static void Import()
    {
        EditOnly(); Directory.CreateDirectory(Art + "/Prefabs"); Directory.CreateDirectory(Art + "/Materials"); Directory.CreateDirectory(Art + "/Meshes"); AssetDatabase.Refresh();
        foreach (var id in Ids)
        {
            var folder = Art + "/Converted/" + id;
            var d = JsonUtility.FromJson<MeshData>(File.ReadAllText(folder + "/mesh.json"));
            var vertices = Enumerable.Range(0, d.vertices.Length / 3).Select(j => new Vector3(d.vertices[j * 3], d.vertices[j * 3 + 1], d.vertices[j * 3 + 2])).ToArray();
            if (vertices.Any(v => !float.IsFinite(v.x) || !float.IsFinite(v.y) || !float.IsFinite(v.z))) throw new InvalidDataException(id + " nonfinite geometry");
            var normals = Enumerable.Range(0, d.normals.Length / 3).Select(j => new Vector3(d.normals[j * 3], d.normals[j * 3 + 1], d.normals[j * 3 + 2])).ToArray();
            var uv = Enumerable.Range(0, d.uv.Length / 2).Select(j => new Vector2(d.uv[j * 2], d.uv[j * 2 + 1])).ToArray();
            var mesh = new Mesh { name = id, vertices = vertices, normals = normals, uv = uv, triangles = d.triangles }; mesh.RecalculateBounds(); mesh.RecalculateTangents();
            mesh = Save(mesh, Art + "/Meshes/" + id + ".asset");
            var ti = (TextureImporter)AssetImporter.GetAtPath(folder + "/basecolor.png");
            ti.textureType = TextureImporterType.Default; ti.filterMode = FilterMode.Trilinear; ti.mipmapEnabled = true; ti.wrapMode = TextureWrapMode.Clamp; ti.textureCompression = TextureImporterCompression.Uncompressed; ti.maxTextureSize = id.Contains("tree") ? 1024 : 512; ti.SaveAndReimport();
            var mat = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = id, enableInstancing = true };
            mat.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(folder + "/basecolor.png")); mat.SetColor("_BaseColor", Color.white); mat.SetFloat("_Smoothness", 0); mat.SetFloat("_Metallic", 0); mat.SetFloat("_SpecularHighlights", 0); mat.SetFloat("_EnvironmentReflections", 0);
            mat = Save(mat, Art + "/Materials/" + id + ".mat");
            for (var v = 0; v < 3; v++)
            {
                var variant = new Material(mat); variant.SetColor("_BaseColor", new Color(.91f + v * .045f, .94f + v * .03f, .96f + v * .025f));
                Save(variant, Art + "/Materials/" + id + "-v" + v + ".mat");
            }
            var root = new GameObject(id, typeof(MeshFilter), typeof(MeshRenderer)); root.GetComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = root.GetComponent<MeshRenderer>(); renderer.sharedMaterial = mat; renderer.shadowCastingMode = ShadowCastingMode.On; renderer.receiveShadows = true;
            try { PrefabUtility.SaveAsPrefabAsset(root, Art + "/Prefabs/" + id + ".prefab"); } finally { Object.DestroyImmediate(root); }
            var saved = AssetDatabase.LoadAssetAtPath<GameObject>(Art + "/Prefabs/" + id + ".prefab");
            if (saved.GetComponent<MeshFilter>().sharedMesh != mesh || saved.GetComponent<MeshRenderer>().sharedMaterial != mat) throw new InvalidDataException(id + " prefab not saved");
        }
    }
    private static GameObject Place(string id, Vector3 position, float yaw, float scale, Transform parent)
    {
        var go = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Art + "/Prefabs/" + id + ".prefab"));
        go.transform.SetParent(parent); go.transform.position = position; go.transform.rotation = Quaternion.Euler(0, yaw, 0); go.transform.localScale = Vector3.one * scale; return go;
    }
    public static void Build()
    {
        EditOnly();
        var existing = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include).SingleOrDefault(x => x.name == "E1 Tripo environment"); if (existing != null) Object.DestroyImmediate(existing.gameObject);
        var root = new GameObject("E1 Tripo environment").transform;
        var oldForest = GameObject.Find("Clustered forest; center radius12 clear");
        var trees = oldForest.transform.Cast<Transform>().ToArray();
        var oldStones = Object.FindObjectsByType<MeshRenderer>().Where(r => r.name == "Faceted stone cluster").ToArray();
        foreach (var r in oldForest.GetComponentsInChildren<MeshRenderer>(true)) r.enabled = r.name == "Sun projected canopy shade";
        UnityEngine.Random.InitState(112203);
        for (var j = 0; j < trees.Length; j++)
        {
            var id = j % 19 == 0 ? "dq_tree_light" : j % 3 == 0 ? "dq_tree_round_c" : "dq_tree_round_a";
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Art + "/Prefabs/" + id + ".prefab");
            var height = prefab.GetComponent<MeshFilter>().sharedMesh.bounds.size.y;
            var h = UnityEngine.Random.Range(1.16f, 1.6f);
            var go = Place(id, trees[j].position, id.Contains("round_b") ? 110 + UnityEngine.Random.Range(-20f, 20f) : UnityEngine.Random.Range(0, 360), h / height, root);
            go.GetComponent<Renderer>().sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(Art + "/Materials/" + id + "-v" + j % 3 + ".mat");
            if (j % 11 == 0) Place("dq_bush_b", trees[j].position + new Vector3(.4f, 0, -.25f), j * 73, 1, root);
        }
        for (var j = 0; j < oldStones.Length; j++)
        {
            oldStones[j].enabled = false; var pos = oldStones[j].transform.position; pos.y = DQLookdevBuilder.Height(pos.x, pos.z) - .025f;
            var id = j % 4 == 0 ? "dq_rock_big_a" : j % 4 == 1 ? "dq_rock_big_b_r2" : j % 4 == 2 ? "dq_rock_small_a_r2" : "dq_rock_small_b_r2";
            Place(id, pos, j * 71 + 17, 1, root);
        }
        Camera.main.aspect = 1920f / 1080; SceneView.RepaintAll(); EditorApplication.QueuePlayerLoopUpdate();
    }
    public static void Select(bool e1)
    {
        EditOnly(); var root = GameObject.Find("E1 Tripo environment");
        if (root == null) root = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include).Single(x => x.name == "E1 Tripo environment").gameObject;
        root.SetActive(e1);
        foreach (var r in GameObject.Find("Clustered forest; center radius12 clear").GetComponentsInChildren<MeshRenderer>(true)) r.enabled = !e1 || r.name == "Sun projected canopy shade";
        foreach (var r in Object.FindObjectsByType<MeshRenderer>().Where(r => r.name == "Faceted stone cluster")) r.enabled = !e1;
        EditorApplication.QueuePlayerLoopUpdate();
    }
    public static void Capture()
    {
        EditOnly(); Directory.CreateDirectory(Output); Select(true);
        // Call after the reopened scene has advanced a frame (separate CLI request).
        DQLookdevCapture.Capture(Output + "/E1-main-1920x1080.png", 1920, 1080);
        DQLookdevCapture.Capture(Output + "/E1-2340x1080.png", 2340, 1080);
        DQLookdevCapture.Capture(Output + "/E1-2048x1536.png", 2048, 1536);
        Diagnostics();
    }
    private static void Diagnostics()
    {
        var all = Object.FindObjectsByType<Renderer>(); var states = all.Select(r => r.enabled).ToArray(); var materials = all.Select(r => r.sharedMaterials).ToArray();
        var data = Camera.main.GetUniversalAdditionalCameraData(); var post = data.renderPostProcessing; var bg = Camera.main.backgroundColor;
        var features = Camera.main.GetComponent<DeadSurvivor.DQLookdev.DQCameraScope>().RendererData.rendererFeatures; var featureStates = features.Select(f => f.isActive).ToArray();
        var white = new Material(Shader.Find("Universal Render Pipeline/Unlit")); white.SetColor("_BaseColor", Color.white);
        try
        {
            // True cast-shade control includes removal of actual shadow casting and projected canopy shade.
            var casters = all.Select(r => r.shadowCastingMode).ToArray();
            try
            {
                foreach (var r in all) { if (r.name == "Sun projected canopy shade") r.enabled = false; if (r.transform.IsChildOf(GameObject.Find("E1 Tripo environment").transform)) r.shadowCastingMode = ShadowCastingMode.Off; }
                DQLookdevCapture.Capture(Output + "/E1-no-canopy-shade-control.png", 1920, 1080);
            }
            finally { for (var j = 0; j < all.Length; j++) { all[j].enabled = states[j]; all[j].shadowCastingMode = casters[j]; } }
            data.renderPostProcessing = false; Camera.main.backgroundColor = Color.black; foreach (var f in features) f.SetActive(false);
            for (var j = 0; j < all.Length; j++) { var r = all[j]; r.enabled = states[j] && r.transform.IsChildOf(GameObject.Find("E1 Tripo environment").transform) && (r.name.Contains("tree") || r.name.Contains("bush")); r.sharedMaterial = white; }
            DQLookdevCapture.Capture(Output + "/E1-forest-mask.png", 1920, 1080);
            for (var j = 0; j < all.Length; j++) { all[j].enabled = states[j] && all[j] is SpriteRenderer && all[j].name != "Blob shadow"; all[j].sharedMaterials = materials[j]; }
            // Sprite shader mask preserves actual sprite alpha; meshes stay disabled.
            var mask = new Material(Shader.Find("DeadSurvivor/DQ/Sprite")); mask.SetFloat("_Mask", 1);
            try { foreach (var r in all.Where(r => r.enabled)) r.sharedMaterial = mask; DQLookdevCapture.Capture(Output + "/E1-actors-mask.png", 1920, 1080); } finally { Object.DestroyImmediate(mask); }
        }
        finally
        {
            for (var j = 0; j < all.Length; j++) { all[j].enabled = states[j]; all[j].sharedMaterials = materials[j]; }
            for (var j = 0; j < features.Count; j++) features[j].SetActive(featureStates[j]); data.renderPostProcessing = post; Camera.main.backgroundColor = bg; Object.DestroyImmediate(white);
        }
    }
    public static void CaptureAssets()
    {
        EditOnly(); var all = Object.FindObjectsByType<Renderer>(); var states = all.Select(r => r.enabled).ToArray();
        var camera = Camera.main; var position = camera.transform.position; var rotation = camera.transform.rotation; var bg = camera.backgroundColor;
        try
        {
            foreach (var r in all) r.enabled = false;
            camera.backgroundColor = new Color(.24f,.27f,.27f); camera.transform.rotation = Quaternion.Euler(18,0,0); camera.transform.position = new Vector3(0,.65f,0) - camera.transform.forward * 6.5f;
            foreach (var id in Ids)
            {
                var go = Place(id, Vector3.zero, id.Contains("round_b") ? 110 : 25, 1, null);
                try { DQLookdevCapture.Capture(Output + "/asset-" + id + ".png",640,640); } finally { Object.DestroyImmediate(go); }
            }
        }
        finally { for (var j=0;j<all.Length;j++) all[j].enabled=states[j]; camera.transform.position=position; camera.transform.rotation=rotation; camera.backgroundColor=bg; }
    }
    public static void SaveScene()
    {
        EditOnly(); Select(true); var guid = AssetDatabase.AssetPathToGUID(DQLookdevBuilder.Scene);
        EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene(), DQLookdevBuilder.Scene);
        EditorSceneManager.OpenScene(DQLookdevBuilder.Scene, OpenSceneMode.Single);
        if (GameObject.Find("E1 Tripo environment") == null || Mathf.Abs(Mathf.DeltaAngle(Camera.main.transform.eulerAngles.x, 18)) > .01f || AssetDatabase.AssetPathToGUID(DQLookdevBuilder.Scene) != guid) throw new InvalidDataException("E1 scene reopen failed");
    }
}
