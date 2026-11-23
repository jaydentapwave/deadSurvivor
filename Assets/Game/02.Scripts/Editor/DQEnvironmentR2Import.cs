using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

// Step 13: turns the step-12 models (Converted/<id>/mesh.json from Tools/dq_glb_to_mesh.py) into RoundDQ3
// mesh, material and prefab assets. Re-runnable: it overwrites only these ids. Run before "Apply Main background".
public static class DQEnvironmentR2Import
{
    public const string Art = "Assets/Game/03.Resources/Art/HD2D/RoundDQ3";
    public static readonly string[] ModelIds = { "dq_tree_round_a_r2", "dq_tree_round_c_r2", "dq_tree_light_r2", "dq_bush_b_r2", "flower_white", "flower_yellow", "flower_purple" };
    public static readonly string[] FlagParts = { "flag_pole", "flag_cloth" };

    [Serializable]
    private sealed class MeshData
    {
        public float[] vertices;
        public float[] normals;
        public float[] uv;
        public int[] triangles;
    }

    [MenuItem("DeadSurvivor/DQ/Import environment r2 models")]
    public static void Import()
    {
        if (Application.isPlaying || EditorApplication.isCompiling)
        {
            throw new InvalidOperationException("Model import requires ready Edit mode.");
        }
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        foreach (var id in ModelIds)
        {
            var mesh = SaveMesh(id);
            var texturePath = Art + "/Converted/" + id + "/basecolor.png";
            var importer = (TextureImporter)AssetImporter.GetAtPath(texturePath);
            importer.textureType = TextureImporterType.Default;
            importer.filterMode = FilterMode.Trilinear;
            importer.mipmapEnabled = true;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.maxTextureSize = id.Contains("tree") ? 1024 : 512;
            importer.SaveAndReimport();
            var material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = id, enableInstancing = true };
            material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath));
            material.SetColor("_BaseColor", Color.white);
            material.SetFloat("_Smoothness", 0);
            material.SetFloat("_Metallic", 0);
            material = Save(material, Art + "/Materials/" + id + ".mat");
            var root = new GameObject(id, typeof(MeshFilter), typeof(MeshRenderer));
            try
            {
                root.GetComponent<MeshFilter>().sharedMesh = mesh;
                var renderer = root.GetComponent<MeshRenderer>();
                renderer.sharedMaterial = material;
                renderer.shadowCastingMode = ShadowCastingMode.On;
                renderer.receiveShadows = true;
                PrefabUtility.SaveAsPrefabAsset(root, Art + "/Prefabs/" + id + ".prefab");
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
            var saved = AssetDatabase.LoadAssetAtPath<GameObject>(Art + "/Prefabs/" + id + ".prefab");
            if (saved.GetComponent<MeshFilter>().sharedMesh != mesh || saved.GetComponent<MeshRenderer>().sharedMaterial != material)
            {
                throw new InvalidDataException(id + " prefab not saved");
            }
        }
        foreach (var part in FlagParts)
        {
            SaveMesh(part);
        }
        AssetDatabase.SaveAssets();
    }

    private static Mesh SaveMesh(string id)
    {
        var data = JsonUtility.FromJson<MeshData>(File.ReadAllText(Art + "/Converted/" + id + "/mesh.json"));
        var vertices = Enumerable.Range(0, data.vertices.Length / 3).Select(j => new Vector3(data.vertices[j * 3], data.vertices[j * 3 + 1], data.vertices[j * 3 + 2])).ToArray();
        if (vertices.Any(v => !float.IsFinite(v.x) || !float.IsFinite(v.y) || !float.IsFinite(v.z)))
        {
            throw new InvalidDataException(id + " nonfinite geometry");
        }
        var normals = Enumerable.Range(0, data.normals.Length / 3).Select(j => new Vector3(data.normals[j * 3], data.normals[j * 3 + 1], data.normals[j * 3 + 2])).ToArray();
        var uv = Enumerable.Range(0, data.uv.Length / 2).Select(j => new Vector2(data.uv[j * 2], data.uv[j * 2 + 1])).ToArray();
        var mesh = new Mesh { name = id, vertices = vertices, normals = normals, uv = uv, triangles = data.triangles };
        mesh.RecalculateBounds();
        mesh.RecalculateTangents();
        return Save(mesh, Art + "/Meshes/" + id + ".asset");
    }

    private static T Save<T>(T value, string path) where T : Object
    {
        var previous = AssetDatabase.LoadAssetAtPath<T>(path);
        if (previous == null)
        {
            AssetDatabase.CreateAsset(value, path);
            return value;
        }
        EditorUtility.CopySerialized(value, previous);
        Object.DestroyImmediate(value);
        EditorUtility.SetDirty(previous);
        AssetDatabase.SaveAssetIfDirty(previous);
        return previous;
    }
}
