using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using DeadSurvivor.DQLookdev;
using Object = UnityEngine.Object;

// Isolated still-image trial. The original DQ builder/capture remain the r2 entry points.
public static class DQCharacterTrial
{
    public const string Art = "Assets/Game/03.Resources/Art/HD2D/RoundDQ2";
    public const string Output = "output/hd2d-dq/char-r1";
    private static readonly string[] HeroList = { "bruno", "lena", "mira", "bolt" };

    [Serializable]
    private sealed class Actor
    {
        public string name;
        public string sprite;
        public float[] rectangle;
        public float[] foot;
        public float[] world;
    }

    [Serializable]
    private sealed class Plate
    {
        public string id;
        public string utc;
        public float[] camera;
        public Actor[] actors;
    }

    public static void Prepare()
    {
        RequireEditMode();
        AssetDatabase.Refresh();
        var path = Art + "/Characters/bruno_dq_front.png";
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spritePixelsPerUnit = 48;
        var spriteSettings = new TextureImporterSettings();
        importer.ReadTextureSettings(spriteSettings);
        spriteSettings.spriteAlignment = (int)SpriteAlignment.Custom;
        importer.SetTextureSettings(spriteSettings);
        importer.spritePivot = new Vector2(.5f, 8f / 72f);
        importer.filterMode = FilterMode.Point;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.mipmapEnabled = false;
        importer.alphaIsTransparency = true;
        importer.wrapMode = TextureWrapMode.Clamp;
        foreach (var platform in new[] { "Android", "iOS" })
        {
            var settings = importer.GetPlatformTextureSettings(platform);
            settings.overridden = true;
            settings.format = TextureImporterFormat.RGBA32;
            settings.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SetPlatformTextureSettings(settings);
        }
        importer.SaveAndReimport();
        EditorSceneManager.OpenScene(DQLookdevBuilder.Scene, OpenSceneMode.Single);
        BakeCanopy();
        var shader = Shader.Find("DeadSurvivor/DQ2/Character Lit");
        var material = AssetDatabase.LoadAssetAtPath<Material>(Art + "/CharactersLit.mat");
        if (material == null)
        {
            material = new Material(shader);
            AssetDatabase.CreateAsset(material, Art + "/CharactersLit.mat");
        }
        material.SetTexture("_CanopyMap", AssetDatabase.LoadAssetAtPath<Texture2D>(Art + "/CanopyTransmission.png"));
        material.SetFloat("_Exposure", 1f);
        material.SetFloat("_EdgeWidth", .10f);
        EditorUtility.SetDirty(material);
        AssetDatabase.SaveAssets();
    }

    public static void Select(string id)
    {
        RequireEditMode();
        if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().path != DQLookdevBuilder.Scene)
        {
            throw new InvalidOperationException("Open the DQ trial scene first.");
        }
        if (!new[] { "P0", "P1", "P2", "P2-shade" }.Contains(id))
        {
            throw new ArgumentException("Unknown character trial plate: " + id);
        }
        for (var j = 0; j < HeroList.Length; j++)
        {
            var sr = GameObject.Find("Hero_" + HeroList[j] + "_" + j).GetComponent<SpriteRenderer>();
            sr.sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Game/03.Resources/Art/AIGenerated/Characters/" + HeroList[j] + "/front/" + HeroList[j] + "_front.png");
            sr.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(DQLookdevBuilder.Art + "/Actors.mat");
            sr.transform.localScale = Vector3.one;
            // Four distinct screen lanes: no overlap even at the fixed 18-degree view.
            var x = new[] { -1.65f, 1.65f, -.55f, .55f }[j];
            var z = new[] { -.95f, -.95f, 2.65f, 2.65f }[j];
            sr.transform.position = new Vector3(x, DQLookdevBuilder.Height(x, z) + .025f, z);
        }
        // Configure using OLD Bruno only, before swapping art: all four captures have the same camera.
        DQLookdevBuilder.Configure(18);
        var bruno = GameObject.Find("Hero_bruno_0").GetComponent<SpriteRenderer>();
        if (id != "P0")
        {
            bruno.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(Art + "/Characters/bruno_dq_front.png");
        }
        if (id.StartsWith("P2", StringComparison.Ordinal))
        {
            foreach (var sr in GameObject.Find("Actors").GetComponentsInChildren<SpriteRenderer>().Where(s => s.name.StartsWith("Hero_", StringComparison.Ordinal)))
            {
                sr.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(Art + "/CharactersLit.mat");
            }
        }
        if (id == "P2-shade")
        {
            // Visible near-side edge of the existing forest. Chosen from the actual canopy transmission map.
            var x = -8.65f;
            var z = 8.4f;
            bruno.transform.position = new Vector3(x, DQLookdevBuilder.Height(x, z) + .025f, z);
        }
        MoveContactShadows();
    }

    public static void All()
    {
        RequireEditMode();
        Directory.CreateDirectory(Output);
        foreach (var id in new[] { "P0", "P1", "P2", "P2-shade" })
        {
            Select(id);
            var filename = id == "P0" ? "P0-current" : id == "P1" ? "P1-newart" : id == "P2" ? "P2-newart-lit" : id;
            DQLookdevCapture.Capture(Output + "/" + filename + ".png", 1920, 1080);
            Describe(id, filename);
            Diagnostics(filename);
            if (id == "P2-shade")
            {
                var bruno = GameObject.Find("Hero_bruno_0").GetComponent<SpriteRenderer>();
                var original = bruno.sharedMaterial;
                var sunlight = new Material(original);
                sunlight.SetTexture("_CanopyMap", Texture2D.whiteTexture);
                try
                {
                    bruno.sharedMaterial = sunlight;
                    DQLookdevCapture.Capture(Output + "/P2-shade-sun-control.png", 1920, 1080);
                }
                finally
                {
                    bruno.sharedMaterial = original;
                    Object.DestroyImmediate(sunlight);
                }
            }
        }
        Select("P2");
        EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene(), DQLookdevBuilder.Scene);
        EditorSceneManager.OpenScene(DQLookdevBuilder.Scene, OpenSceneMode.Single);
        var saved = GameObject.Find("Hero_bruno_0").GetComponent<SpriteRenderer>();
        if (AssetDatabase.GetAssetPath(saved.sprite) != Art + "/Characters/bruno_dq_front.png" || AssetDatabase.GetAssetPath(saved.sharedMaterial) != Art + "/CharactersLit.mat")
        {
            throw new InvalidOperationException("Character trial references did not survive save/reopen.");
        }
    }

    private static void RequireEditMode()
    {
        if (Application.isPlaying)
        {
            throw new InvalidOperationException("Do not alter a playing scene.");
        }
    }

    private static void MoveContactShadows()
    {
        var shadowList = Object.FindObjectsByType<MeshFilter>().Where(m => m.name == "Foot contact shade").ToArray();
        for (var j = 0; j < HeroList.Length; j++)
        {
            var original = DQLookdevBuilder.Art + "/FootShade" + j + ".asset";
            var replacement = Art + "/FootShade" + j + ".asset";
            var filter = shadowList.Single(m => new[] { original, replacement }.Contains(AssetDatabase.GetAssetPath(m.sharedMesh)));
            var source = AssetDatabase.LoadAssetAtPath<Mesh>(original);
            var vertices = source.vertices;
            var center = source.bounds.center;
            var foot = GameObject.Find("Hero_" + HeroList[j] + "_" + j).transform.position;
            for (var k = 0; k < vertices.Length; k++)
            {
                var x = vertices[k].x - center.x + foot.x;
                var z = vertices[k].z - center.z + foot.z;
                vertices[k] = new Vector3(x, DQLookdevBuilder.Height(x, z) + .018f, z);
            }
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(replacement);
            if (mesh == null)
            {
                mesh = Object.Instantiate(source);
                AssetDatabase.CreateAsset(mesh, replacement);
            }
            mesh.vertices = vertices;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            mesh.UploadMeshData(false);
            EditorUtility.SetDirty(mesh);
            filter.sharedMesh = mesh;
        }
        AssetDatabase.SaveAssets();
    }

    private static void BakeCanopy()
    {
        const int size = 2048;
        var transmission = Enumerable.Repeat(Color.white, size * size).ToArray();
        var shadeList = Object.FindObjectsByType<MeshRenderer>().Where(r => r.name == "Sun projected canopy shade").ToArray();
        var texture = new Texture2D(2, 2);
        texture.LoadImage(File.ReadAllBytes(AssetDatabase.GetAssetPath(shadeList[0].sharedMaterial.GetTexture("_BaseMap"))));
        foreach (var shade in shadeList)
        {
            var bounds = shade.bounds;
            var x0 = Mathf.Clamp(Mathf.FloorToInt((bounds.min.x + 70) / 140 * size), 0, size - 1);
            var x1 = Mathf.Clamp(Mathf.CeilToInt((bounds.max.x + 70) / 140 * size), 0, size - 1);
            var y0 = Mathf.Clamp(Mathf.FloorToInt((bounds.min.z + 35) / 140 * size), 0, size - 1);
            var y1 = Mathf.Clamp(Mathf.CeilToInt((bounds.max.z + 35) / 140 * size), 0, size - 1);
            for (var y = y0; y <= y1; y++)
            {
                for (var x = x0; x <= x1; x++)
                {
                    var wx = (x + .5f) / size * 140 - 70;
                    var wz = (y + .5f) / size * 140 - 35;
                    var u = Mathf.InverseLerp(bounds.min.x, bounds.max.x, wx);
                    var v = Mathf.InverseLerp(bounds.min.z, bounds.max.z, wz);
                    transmission[y * size + x] *= 1 - texture.GetPixelBilinear(u, v).a * .94f;
                }
            }
        }
        Object.DestroyImmediate(texture);
        var map = new Texture2D(size, size, TextureFormat.RGBA32, false);
        map.SetPixels(transmission);
        map.Apply();
        var path = Art + "/CanopyTransmission.png";
        File.WriteAllBytes(path, map.EncodeToPNG());
        Object.DestroyImmediate(map);
        AssetDatabase.ImportAsset(path);
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType = TextureImporterType.Default;
        importer.sRGBTexture = false;
        importer.mipmapEnabled = false;
        importer.filterMode = FilterMode.Bilinear;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.SaveAndReimport();
    }

    private static void Diagnostics(string filename)
    {
        var camera = Camera.main;
        var bruno = GameObject.Find("Hero_bruno_0").GetComponent<SpriteRenderer>();
        bruno.enabled = false;
        try
        {
            DQLookdevCapture.Capture(Output + "/" + filename + "-background.png", 1920, 1080);
        }
        finally
        {
            bruno.enabled = true;
        }
        var rendererList = Object.FindObjectsByType<Renderer>();
        var stateList = rendererList.Select(r => r.enabled).ToArray();
        var previousMaterial = bruno.sharedMaterial;
        var mask = new Material(Shader.Find("DeadSurvivor/DQ/Sprite"));
        mask.SetFloat("_Mask", 1);
        var data = camera.GetUniversalAdditionalCameraData();
        var previousPost = data.renderPostProcessing;
        var previousBackground = camera.backgroundColor;
        var previousFlags = camera.clearFlags;
        var featureList = camera.GetComponent<DQCameraScope>().RendererData.rendererFeatures;
        var featureStateList = featureList.Select(f => f.isActive).ToArray();
        try
        {
            foreach (var renderer in rendererList)
            {
                renderer.enabled = renderer == bruno;
            }
            foreach (var feature in featureList)
            {
                feature.SetActive(false);
            }
            data.renderPostProcessing = false;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.black;
            bruno.sharedMaterial = mask;
            DQLookdevCapture.Capture(Output + "/" + filename + "-mask.png", 1920, 1080);
        }
        finally
        {
            for (var j = 0; j < rendererList.Length; j++)
            {
                rendererList[j].enabled = stateList[j];
            }
            for (var j = 0; j < featureList.Count; j++)
            {
                featureList[j].SetActive(featureStateList[j]);
            }
            data.renderPostProcessing = previousPost;
            camera.backgroundColor = previousBackground;
            camera.clearFlags = previousFlags;
            bruno.sharedMaterial = previousMaterial;
            Object.DestroyImmediate(mask);
        }
    }

    private static void Describe(string id, string filename)
    {
        var camera = Camera.main;
        var plate = new Plate
        {
            id = id,
            utc = DateTime.UtcNow.ToString("O"),
            camera = new[] { camera.transform.position.x, camera.transform.position.y, camera.transform.position.z, camera.transform.eulerAngles.x, camera.fieldOfView },
            actors = HeroList.Select((name, j) =>
            {
                var sr = GameObject.Find("Hero_" + name + "_" + j).GetComponent<SpriteRenderer>();
                var min = sr.bounds.min;
                var max = sr.bounds.max;
                var corners = new[] { new Vector3(min.x, min.y, min.z), new Vector3(max.x, max.y, max.z), new Vector3(min.x, max.y, min.z), new Vector3(max.x, min.y, max.z) }.Select(camera.WorldToViewportPoint).ToArray();
                var foot = camera.WorldToViewportPoint(sr.transform.position);
                var world = sr.transform.position;
                return new Actor { name = name, sprite = AssetDatabase.GetAssetPath(sr.sprite), rectangle = new[] { corners.Min(p => p.x) * 1920, (1 - corners.Max(p => p.y)) * 1080, corners.Max(p => p.x) * 1920, (1 - corners.Min(p => p.y)) * 1080 }, foot = new[] { foot.x * 1920, (1 - foot.y) * 1080 }, world = new[] { world.x, world.y, world.z } };
            }).ToArray()
        };
        File.WriteAllText(Output + "/" + filename + ".json", JsonUtility.ToJson(plate, true));
    }
}
