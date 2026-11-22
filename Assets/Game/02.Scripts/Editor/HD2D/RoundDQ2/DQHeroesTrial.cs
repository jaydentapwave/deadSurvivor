using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

// H1 is an additional still-image plate. Existing P2/P3 entry points remain unchanged.
public static class DQHeroesTrial
{
    public const string Output = "output/hd2d-dq/heroes-r1";
    private const string Art = DQCharacterTrial.Art;
    private static readonly string[] HeroList = { "bruno", "lena", "mira", "bolt" };

    [Serializable]
    private sealed class Actor
    {
        public string name;
        public string sprite;
        public float[] world;
        public float[] rectangle;
        public float[] foot;
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
        foreach (var name in HeroList.Where(n => n != "bruno"))
        {
            var path = Art + "/Characters/" + name + "_dq_front.png";
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 48;
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteAlignment = (int)SpriteAlignment.Custom;
            settings.spritePivot = new Vector2(.5f, 8f / 72f);
            importer.SetTextureSettings(settings);
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.wrapMode = TextureWrapMode.Clamp;
            foreach (var platform in new[] { "Android", "iOS" })
            {
                var platformSettings = importer.GetPlatformTextureSettings(platform);
                platformSettings.overridden = true;
                platformSettings.format = TextureImporterFormat.RGBA32;
                platformSettings.textureCompression = TextureImporterCompression.Uncompressed;
                importer.SetPlatformTextureSettings(platformSettings);
            }
            importer.SaveAndReimport();
        }
        // Discard current in-memory scene edits; never save Main or shared art/material assets.
        EditorSceneManager.OpenScene(DQLookdevBuilder.Scene, OpenSceneMode.Single);
    }

    public static void Select(bool shade = false)
    {
        RequireEditMode();
        HideMonsters();
        if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().path != DQLookdevBuilder.Scene)
        {
            throw new InvalidOperationException("Open Lookdev_DQ before selecting H1.");
        }
        Camera.main.aspect = 1920f / 1080f;
        var filterList = Object.FindObjectsByType<MeshFilter>()
            .Where(m => m.name == "Foot contact shade").ToArray();
        for (var j = 0; j < HeroList.Length; j++)
        {
            var sr = GameObject.Find("Hero_" + HeroList[j] + "_" + j).GetComponent<SpriteRenderer>();
            sr.enabled = true;
            sr.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(Art + "/Characters/" + HeroList[j] + "_dq_front.png");
            sr.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(Art + "/CharactersLit.mat");
            sr.transform.localScale = Vector3.one;
            var x = shade ? new[] { -9.6f, -8.7f, -5.8f, -3.1f }[j] : new[] { -1.7f, 1.7f, -.57f, .57f }[j];
            var z = shade ? new[] { 6.5f, 8.7f, 11.2f, 14.7f }[j] : new[] { -.95f, -.95f, 2.65f, 2.65f }[j];
            var foot = new Vector3(x, DQLookdevBuilder.Height(x, z) + .025f, z);
            sr.transform.position = foot;
            var index = j;
            var filter = filterList.Single(m => AssetDatabase.GetAssetPath(m.sharedMesh).EndsWith("FootShade" + index + ".asset", StringComparison.Ordinal)
                || AssetDatabase.GetAssetPath(m.sharedMesh).EndsWith("H1-" + index + ".asset", StringComparison.Ordinal)
                || AssetDatabase.GetAssetPath(m.sharedMesh).EndsWith("shade-" + index + ".asset", StringComparison.Ordinal));
            var source = AssetDatabase.LoadAssetAtPath<Mesh>(DQLookdevBuilder.Art + "/FootShade" + j + ".asset");
            var path = Art + "/HeroesR1/" + (shade ? "shade-" : "H1-") + j + ".asset";
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (mesh == null)
            {
                mesh = Object.Instantiate(source);
                AssetDatabase.CreateAsset(mesh, path);
            }
            var vertices = source.vertices;
            var center = source.bounds.center;
            for (var k = 0; k < vertices.Length; k++)
            {
                var wx = vertices[k].x - center.x + foot.x;
                var wz = vertices[k].z - center.z + foot.z;
                vertices[k] = new Vector3(wx, DQLookdevBuilder.Height(wx, wz) + .018f, wz);
            }
            mesh.vertices = vertices;
            mesh.RecalculateBounds();
            mesh.RecalculateNormals();
            EditorUtility.SetDirty(mesh);
            AssetDatabase.SaveAssetIfDirty(mesh);
            filter.sharedMesh = mesh;
        }
    }

    // M1 owns only its new root and task-local assets; H1/P2/P3 keep their existing actors.
    public static void HideMonsters()
    {
        var root = UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects()
            .SingleOrDefault(go => go.name == "M1 Party Monsters");
        if (root == null)
        {
            return;
        }
        root.SetActive(false);
        foreach (var sr in Object.FindObjectsByType<SpriteRenderer>().Where(sr => sr.name.StartsWith("Hero_", StringComparison.Ordinal) || sr.name.StartsWith("Monster_", StringComparison.Ordinal)))
        {
            sr.enabled = true;
        }
        foreach (var filter in Object.FindObjectsByType<MeshFilter>().Where(filter => filter.name == "Foot contact shade"))
        {
            filter.GetComponent<MeshRenderer>().enabled = true;
            // P2/P3 resolve the four hero shadows by their established asset paths.
            var path = AssetDatabase.GetAssetPath(filter.sharedMesh);
            for (var j = 0; j < HeroList.Length; j++)
            {
                if (path.EndsWith("/H1-" + j + ".asset", StringComparison.Ordinal) || path.EndsWith("/shade-" + j + ".asset", StringComparison.Ordinal))
                {
                    filter.sharedMesh = AssetDatabase.LoadAssetAtPath<Mesh>(Art + "/FootShade" + j + ".asset");
                }
            }
        }
    }

    public static void PrepareMonsters()
    {
        RequireEditMode();
        AssetDatabase.Refresh();
        foreach (var name in new[] { "slime", "mushroom", "rock", "boar", "puffer" })
        {
            var importer = (TextureImporter)AssetImporter.GetAtPath(Art + "/Monsters/" + name + "_dq_front.png");
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 48;
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteAlignment = (int)SpriteAlignment.Custom;
            settings.spritePivot = new Vector2(.5f, 0);
            importer.SetTextureSettings(settings);
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.wrapMode = TextureWrapMode.Clamp;
            foreach (var platform in new[] { "Android", "iOS" })
            {
                var platformSettings = importer.GetPlatformTextureSettings(platform);
                platformSettings.overridden = true;
                platformSettings.format = TextureImporterFormat.RGBA32;
                platformSettings.textureCompression = TextureImporterCompression.Uncompressed;
                importer.SetPlatformTextureSettings(platformSettings);
            }
            importer.SaveAndReimport();
        }
        EditorSceneManager.OpenScene(DQLookdevBuilder.Scene, OpenSceneMode.Single);
    }

    public static void SelectMonsters()
    {
        RequireEditMode();
        if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().path != DQLookdevBuilder.Scene)
        {
            throw new InvalidOperationException("Open Lookdev_DQ before selecting M1.");
        }
        HideMonsters();
        var previous = UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects()
            .SingleOrDefault(go => go.name == "M1 Party Monsters");
        if (previous != null)
        {
            Object.DestroyImmediate(previous);
        }
        var template = GameObject.Find("Hero_bruno_0").GetComponent<SpriteRenderer>();
        var originalShadeList = Object.FindObjectsByType<MeshFilter>().Where(filter => filter.name == "Foot contact shade").ToArray();
        var shadeMaterial = originalShadeList[0].GetComponent<MeshRenderer>().sharedMaterial;
        var source = AssetDatabase.LoadAssetAtPath<Mesh>(DQLookdevBuilder.Art + "/FootShade0.asset");
        var root = new GameObject("M1 Party Monsters").transform;
        var nameList = HeroList.Concat(new[] { "slime", "mushroom", "rock", "boar", "puffer" }).ToArray();
        var xList = new[] { -1.7f, 1.7f, -.57f, .57f, -3.4f, -2.8f, 0f, 3.2f, 3.6f };
        var zList = new[] { -.95f, -.95f, 2.65f, 2.65f, -2.8f, 4.4f, 6.5f, 4.3f, -2.8f };
        for (var j = 0; j < nameList.Length; j++)
        {
            var sr = Object.Instantiate(template, root);
            sr.name = "M1_" + nameList[j];
            sr.enabled = true;
            sr.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(Art + (j < 4 ? "/Characters/" : "/Monsters/") + nameList[j] + "_dq_front.png");
            sr.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(Art + "/CharactersLit.mat");
            sr.transform.localScale = Vector3.one;
            var foot = new Vector3(xList[j], DQLookdevBuilder.Height(xList[j], zList[j]) + .025f, zList[j]);
            sr.transform.position = foot;
            var path = Art + "/Monsters/M1-shade-" + j + ".asset";
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (mesh == null)
            {
                mesh = Object.Instantiate(source);
                AssetDatabase.CreateAsset(mesh, path);
            }
            var vertices = source.vertices;
            var center = source.bounds.center;
            var scale = j == 6 ? 1.4f : j < 4 ? 1f : .8f;
            for (var k = 0; k < vertices.Length; k++)
            {
                var x = (vertices[k].x - center.x) * scale + foot.x;
                var z = (vertices[k].z - center.z) * scale + foot.z;
                vertices[k] = new Vector3(x, DQLookdevBuilder.Height(x, z) + .018f, z);
            }
            mesh.vertices = vertices;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            EditorUtility.SetDirty(mesh);
            AssetDatabase.SaveAssetIfDirty(mesh);
            var shadow = new GameObject("M1 round foot shade");
            shadow.transform.SetParent(root);
            shadow.AddComponent<MeshFilter>().sharedMesh = mesh;
            shadow.AddComponent<MeshRenderer>().sharedMaterial = shadeMaterial;
        }
        foreach (var sr in Object.FindObjectsByType<SpriteRenderer>().Where(sr => sr.name.StartsWith("Hero_", StringComparison.Ordinal) || sr.name.StartsWith("Monster_", StringComparison.Ordinal)))
        {
            sr.enabled = false;
        }
        foreach (var filter in originalShadeList)
        {
            filter.GetComponent<MeshRenderer>().enabled = false;
        }
        Camera.main.aspect = 1920f / 1080f;
    }

    public static void CaptureMonsters()
    {
        RequireEditMode();
        SelectMonsters();
        const string output = "output/hd2d-dq/heroes-r2/M1-party-monsters";
        DQLookdevCapture.Capture(output + ".png", 1920, 1080);
        var plate = new Plate
        {
            id = "M1",
            utc = DateTime.UtcNow.ToString("O"),
            camera = new[] { Camera.main.transform.eulerAngles.x, Camera.main.fieldOfView },
            actors = GameObject.Find("M1 Party Monsters").GetComponentsInChildren<SpriteRenderer>().Select(sr => Describe(sr, 1920, 1080)).ToArray()
        };
        File.WriteAllText(output + ".json", JsonUtility.ToJson(plate, true));
        EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene(), DQLookdevBuilder.Scene);
        EditorSceneManager.OpenScene(DQLookdevBuilder.Scene, OpenSceneMode.Single);
        if (GameObject.Find("M1 Party Monsters").GetComponentsInChildren<SpriteRenderer>().Length != 9)
        {
            throw new InvalidOperationException("M1 actors were not persisted.");
        }
    }

    public static void CaptureAll()
    {
        RequireEditMode();
        Directory.CreateDirectory(Output);
        Select();
        Capture("H1-party", 1920, 1080);
        Capture("H1-2340x1080", 2340, 1080);
        Capture("H1-2048x1536", 2048, 1536);
        Select(true);
        Capture("H1-shade", 1920, 1080);
        Select();
        EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene(), DQLookdevBuilder.Scene);
    }

    private static void Capture(string id, int width, int height)
    {
        DQLookdevCapture.Capture(Output + "/" + id + ".png", width, height);
        var camera = Camera.main;
        var aspect = camera.aspect;
        camera.aspect = (float)width / height;
        try
        {
            var plate = new Plate
            {
                id = id,
                utc = DateTime.UtcNow.ToString("O"),
                camera = new[] { camera.transform.position.x, camera.transform.position.y, camera.transform.position.z, camera.transform.eulerAngles.x, camera.fieldOfView },
                actors = HeroList.Select((name, j) => Describe(GameObject.Find("Hero_" + name + "_" + j).GetComponent<SpriteRenderer>(), width, height)).ToArray()
            };
            File.WriteAllText(Output + "/" + id + ".json", JsonUtility.ToJson(plate, true));
        }
        finally
        {
            camera.aspect = aspect;
        }
    }

    private static Actor Describe(SpriteRenderer sr, int width, int height)
    {
        var min = sr.bounds.min;
        var max = sr.bounds.max;
        var corners = new[] { new Vector3(min.x, min.y, min.z), new Vector3(max.x, min.y, min.z), new Vector3(min.x, max.y, max.z), new Vector3(max.x, max.y, max.z) }
            .Select(Camera.main.WorldToViewportPoint).ToArray();
        var foot = Camera.main.WorldToViewportPoint(sr.transform.position);
        var world = sr.transform.position;
        return new Actor
        {
            name = sr.name,
            sprite = AssetDatabase.GetAssetPath(sr.sprite),
            rectangle = new[] { corners.Min(v => v.x) * width, (1 - corners.Max(v => v.y)) * height, corners.Max(v => v.x) * width, (1 - corners.Min(v => v.y)) * height },
            foot = new[] { foot.x * width, (1 - foot.y) * height },
            world = new[] { world.x, world.y, world.z }
        };
    }

    private static void RequireEditMode()
    {
        if (Application.isPlaying)
        {
            throw new InvalidOperationException("Do not alter a playing scene.");
        }
    }
}
