using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

// Candidate placement only: original art/material/scene assets are read, never rebuilt or saved.
public static class DQCandidateComparison
{
    private const string RootName = "DQ candidate comparison";
    private const string Current = "Assets/Game/03.Resources/Art/AIGenerated/Characters/bruno/front/bruno_front.png";

    [Serializable]
    private sealed class Actor
    {
        public string id;
        public string sprite;
        public float[] rectangle;
        public float[] foot;
        public float[] world;
        public int sourceHeight;
    }

    [Serializable]
    private sealed class Plate
    {
        public string id;
        public string utc;
        public float[] camera;
        public Actor[] actors;
        public string monsters;
    }

    public static void Prepare()
    {
        if (Application.isPlaying)
        {
            throw new InvalidOperationException("Do not change a playing scene.");
        }
        AssetDatabase.Refresh();
        foreach (var id in new[] { "04", "08" })
        {
            var importer = (TextureImporter)AssetImporter.GetAtPath(DQCharacterTrial.Art + "/Characters/bruno_dq_c" + id + ".png");
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
        // Deliberately discard unsaved editor scene state instead of saving Main or a previous trial.
        EditorSceneManager.OpenScene(DQLookdevBuilder.Scene, OpenSceneMode.Single);
        AssertCamera();
        Camera.main.aspect = 1920f / 1080;
    }

    public static void CaptureAll()
    {
        RequireEditMode();
        AssertCamera();
        var baseline = MonsterState();
        foreach (var id in new[] { "c04", "c08", "lineup" })
        {
            Select(id);
            var actorList = id == "lineup"
                ? GameObject.Find(RootName).GetComponentsInChildren<SpriteRenderer>()
                : new[] { GameObject.Find("Hero_bruno_0").GetComponent<SpriteRenderer>() };
            if (MonsterState() != baseline)
            {
                throw new InvalidOperationException("Candidate comparison changed monster positions.");
            }
            var path = DQCharacterTrial.Output + "/P3-" + id;
            DQLookdevCapture.Capture(path + ".png", 1920, 1080);
            var camera = Camera.main;
            var plate = new Plate
            {
                id = id,
                utc = DateTime.UtcNow.ToString("O"),
                camera = new[] { camera.transform.position.x, camera.transform.position.y, camera.transform.position.z, camera.transform.eulerAngles.x, camera.fieldOfView },
                actors = actorList.Select(Describe).ToArray(),
                monsters = baseline
            };
            File.WriteAllText(path + ".json", JsonUtility.ToJson(plate, true));
        }
        // Keep the lineup visible in memory. Never write the existing Lookdev_DQ scene/meshes/materials.
        EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
    }

    public static void Select(string id)
    {
        RequireEditMode();
        DQHeroesTrial.HideMonsters();
        AssertCamera();
        if (!new[] { "c04", "c08", "lineup" }.Contains(id))
        {
            throw new ArgumentException("Unknown comparison plate: " + id);
        }
        ClearLineup();
        var bruno = GameObject.Find("Hero_bruno_0").GetComponent<SpriteRenderer>();
        bruno.enabled = true;
        bruno.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(DQCharacterTrial.Art + "/Characters/bruno_dq_front.png");
        bruno.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(DQCharacterTrial.Art + "/CharactersLit.mat");
        var contact = BrunoShadow();
        contact.enabled = true;
        if (id != "lineup")
        {
            bruno.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(DQCharacterTrial.Art + "/Characters/bruno_dq_" + id + ".png");
            return;
        }
        var root = new GameObject(RootName).transform;
        var sourceList = new[] { Current, DQCharacterTrial.Art + "/Characters/bruno_dq_c04.png", DQCharacterTrial.Art + "/Characters/bruno_dq_c08.png", DQCharacterTrial.Art + "/Characters/bruno_dq_front.png" };
        var nameList = new[] { "current", "c04", "c08", "refined06" };
        // One shared depth on open grass, left of Lena at her unchanged normal position.
        var xList = new[] { -4.4f, -2.7f, -1f, .65f };
        for (var j = 0; j < sourceList.Length; j++)
        {
            var go = Object.Instantiate(bruno.gameObject, root);
            go.name = nameList[j];
            var sr = go.GetComponent<SpriteRenderer>();
            sr.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(sourceList[j]);
            var x = xList[j];
            const float z = -1.3f;
            go.transform.position = new Vector3(x, DQLookdevBuilder.Height(x, z) + .025f, z);
            PlaceShadow(contact, go.transform.position, root);
        }
        bruno.enabled = false;
        contact.enabled = false;
    }

    private static void RequireEditMode()
    {
        if (Application.isPlaying)
        {
            throw new InvalidOperationException("Do not change a playing scene.");
        }
        if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().path != DQLookdevBuilder.Scene)
        {
            throw new InvalidOperationException("Open the DQ trial scene before candidate comparison.");
        }
    }

    private static void AssertCamera()
    {
        var expected = JsonUtility.FromJson<Plate>(File.ReadAllText(DQCharacterTrial.Output + "/P2-newart-lit.json")).camera;
        var camera = Camera.main;
        var p = camera.transform.position;
        if (Vector3.Distance(p, new Vector3(expected[0], expected[1], expected[2])) > .0001f
            || Mathf.Abs(camera.transform.eulerAngles.x - expected[3]) > .0001f
            || Mathf.Abs(camera.fieldOfView - expected[4]) > .0001f)
        {
            throw new InvalidOperationException("Candidate capture must keep the P2 camera.");
        }
    }

    private static MeshRenderer BrunoShadow()
    {
        return Object.FindObjectsByType<MeshFilter>().Single(m => AssetDatabase.GetAssetPath(m.sharedMesh) == DQCharacterTrial.Art + "/FootShade0.asset").GetComponent<MeshRenderer>();
    }

    private static void PlaceShadow(MeshRenderer source, Vector3 foot, Transform parent)
    {
        var original = source.GetComponent<MeshFilter>().sharedMesh;
        var mesh = Object.Instantiate(original);
        var vertices = original.vertices;
        var center = original.bounds.center;
        for (var j = 0; j < vertices.Length; j++)
        {
            var x = vertices[j].x - center.x + foot.x;
            var z = vertices[j].z - center.z + foot.z;
            vertices[j] = new Vector3(x, DQLookdevBuilder.Height(x, z) + .018f, z);
        }
        mesh.vertices = vertices;
        mesh.RecalculateBounds();
        mesh.RecalculateNormals();
        var go = new GameObject("Comparison contact shade", typeof(MeshFilter), typeof(MeshRenderer));
        go.transform.SetParent(parent);
        go.GetComponent<MeshFilter>().sharedMesh = mesh;
        go.GetComponent<MeshRenderer>().sharedMaterial = source.sharedMaterial;
    }

    private static void ClearLineup()
    {
        var root = GameObject.Find(RootName);
        if (root == null)
        {
            return;
        }
        foreach (var filter in root.GetComponentsInChildren<MeshFilter>())
        {
            Object.DestroyImmediate(filter.sharedMesh);
        }
        Object.DestroyImmediate(root);
    }

    private static string MonsterState()
    {
        return string.Join(";", GameObject.Find("Actors").GetComponentsInChildren<SpriteRenderer>()
            .Where(s => s.name.StartsWith("Monster_", StringComparison.Ordinal)).OrderBy(s => s.name)
            .Select(s => s.name + ":" + s.transform.position.ToString("F6") + ":" + AssetDatabase.GetAssetPath(s.sprite)));
    }

    private static Actor Describe(SpriteRenderer sr)
    {
        var path = AssetDatabase.GetAssetPath(sr.sprite);
        var image = new Texture2D(2, 2);
        image.LoadImage(File.ReadAllBytes(path));
        var pixels = image.GetPixels32();
        var minX = image.width;
        var minY = image.height;
        var maxX = 0;
        var maxY = 0;
        for (var y = 0; y < image.height; y++)
        {
            for (var x = 0; x < image.width; x++)
            {
                if (pixels[y * image.width + x].a >= 128)
                {
                    minX = Mathf.Min(minX, x);
                    minY = Mathf.Min(minY, y);
                    maxX = Mathf.Max(maxX, x + 1);
                    maxY = Mathf.Max(maxY, y + 1);
                }
            }
        }
        Object.DestroyImmediate(image);
        var pivot = sr.sprite.pivot;
        var ppu = sr.sprite.pixelsPerUnit;
        var pointList = new[] { new Vector3(minX - pivot.x, minY - pivot.y, 0), new Vector3(maxX - pivot.x, maxY - pivot.y, 0), new Vector3(minX - pivot.x, maxY - pivot.y, 0), new Vector3(maxX - pivot.x, minY - pivot.y, 0) }
            .Select(v => Camera.main.WorldToViewportPoint(sr.transform.TransformPoint(v / ppu))).ToArray();
        var foot = Camera.main.WorldToViewportPoint(sr.transform.position);
        var world = sr.transform.position;
        return new Actor
        {
            id = sr.name == "Hero_bruno_0" ? Path.GetFileNameWithoutExtension(path) : sr.name,
            sprite = path,
            rectangle = new[] { pointList.Min(v => v.x) * 1920, (1 - pointList.Max(v => v.y)) * 1080, pointList.Max(v => v.x) * 1920, (1 - pointList.Min(v => v.y)) * 1080 },
            foot = new[] { foot.x * 1920, (1 - foot.y) * 1080 },
            world = new[] { world.x, world.y, world.z },
            sourceHeight = maxY - minY
        };
    }
}
