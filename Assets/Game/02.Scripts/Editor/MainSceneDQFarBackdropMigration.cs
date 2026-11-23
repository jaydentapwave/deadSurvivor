using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

// Step 11: far mountains and sea as a camera-attached strip at the top of the Main battle view.
// Re-runnable: creates what is missing, rewrites the same values, saves and re-opens to verify.
public static class MainSceneDQFarBackdropMigration
{
    public const string TexturePath = "Assets/Game/03.Resources/Art/HD2D/DQMainBackground/Far/FarBackdrop.png";
    public const string MaterialPath = "Assets/Game/03.Resources/Materials/HD2D/DQ_FarBackdrop.mat";
    public const string ObjectName = "DQ Far Backdrop";
    // Camera space (m): 52 m ahead covers the old haze band (top ~20% of the frame at pitch 18, fov 20).
    // The card's lower edge (2.7 m up) sits below the ground line so nearer ground always covers it.
    public static readonly Vector3 LocalPosition = new(0f, 6.4f, 52f);
    public static readonly Vector3 LocalScale = new(44f, 7.4f, 1f);

    [MenuItem("DeadSurvivor/DQ/Apply Main far backdrop")]
    public static void Apply()
    {
        if (Application.isPlaying || EditorApplication.isCompiling || EditorSceneManager.GetActiveScene().isDirty)
        {
            throw new InvalidOperationException("[MainSceneDQFarBackdropMigration] Requires saved Edit mode.");
        }
        var importer = (TextureImporter)AssetImporter.GetAtPath(TexturePath);
        importer.textureType = TextureImporterType.Default;
        importer.sRGBTexture = true;
        importer.wrapModeU = TextureWrapMode.Repeat;
        importer.wrapModeV = TextureWrapMode.Clamp;
        importer.mipmapEnabled = true;
        importer.maxTextureSize = 2048;
        importer.textureCompression = TextureImporterCompression.CompressedHQ;
        importer.SaveAndReimport();

        var shader = Shader.Find("DeadSurvivor/DQ/Far backdrop");
        var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        if (material == null)
        {
            material = new Material(shader);
            AssetDatabase.CreateAsset(material, MaterialPath);
        }
        material.shader = shader;
        material.SetTexture("_MainTex", AssetDatabase.LoadAssetAtPath<Texture2D>(TexturePath));
        // Image v .0 (bottom: distant coast) to .824 (row 180 from the top: just under the clouds).
        material.SetVector("_Rows", new Vector4(0f, .824f, 0f, 0f));
        material.SetFloat("_Tile", 24f);
        material.SetFloat("_Parallax", .1f);
        material.SetColor("_Tint", Color.white);
        EditorUtility.SetDirty(material);
        AssetDatabase.SaveAssetIfDirty(material);

        var scene = EditorSceneManager.OpenScene(MainSceneDQScreenMigration.ScenePath);
        var camera = Camera.main;
        var card = camera.transform.Find(ObjectName);
        if (card == null)
        {
            card = new GameObject(ObjectName, typeof(MeshFilter), typeof(MeshRenderer)).transform;
            card.SetParent(camera.transform, false);
        }
        card.gameObject.layer = 0;
        card.localPosition = LocalPosition;
        card.localRotation = Quaternion.identity;
        card.localScale = LocalScale;
        card.GetComponent<MeshFilter>().sharedMesh = Resources.GetBuiltinResource<Mesh>("Quad.fbx");
        var renderer = card.GetComponent<MeshRenderer>();
        renderer.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        renderer.lightProbeUsage = LightProbeUsage.Off;
        renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene))
        {
            throw new InvalidOperationException("[MainSceneDQFarBackdropMigration] Could not save Main.");
        }
        // Same canonical form as the light migration (Unity re-adds a trailing space to this empty name).
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var sceneText = System.IO.File.ReadAllText(MainSceneDQScreenMigration.ScenePath);
        System.IO.File.WriteAllText(MainSceneDQScreenMigration.ScenePath, sceneText.Replace(
            "  m_Name: \n  m_EditorClassIdentifier: HD2D.View::DQSceneLighting", "  m_Name:\n  m_EditorClassIdentifier: HD2D.View::DQSceneLighting"));
        AssetDatabase.ImportAsset(MainSceneDQScreenMigration.ScenePath, ImportAssetOptions.ForceUpdate);
        EditorSceneManager.OpenScene(MainSceneDQScreenMigration.ScenePath);
        var saved = Camera.main.transform.Find(ObjectName);
        if (saved == null || saved.GetComponent<MeshRenderer>().sharedMaterial != AssetDatabase.LoadAssetAtPath<Material>(MaterialPath)
            || saved.localPosition != LocalPosition)
        {
            throw new InvalidOperationException("[MainSceneDQFarBackdropMigration] Far backdrop did not survive save/reopen.");
        }
    }
}
