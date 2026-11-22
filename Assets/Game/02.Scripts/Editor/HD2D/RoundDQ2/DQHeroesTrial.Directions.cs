using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

// D1 is a still-image extension of the existing trial, with no runtime connections.
public static partial class DQHeroesTrial
{
    public const string DirectionsOutput = "output/hd2d-dq/dirs-scene";
    private const string DirectionsRoot = "D1 Directions";
    private static readonly string[] MonsterList = { "slime", "mushroom", "rock", "boar", "puffer" };
    private static readonly string[] DirectionList = { "down", "downright", "right", "upright", "up", "upleft", "left", "downleft" };

    [Serializable]
    private sealed class ImportRecord
    {
        public string path;
        public string reference;
        public string type;
        public string mode;
        public float ppu;
        public string filter;
        public string compression;
        public bool mipmaps;
        public Vector2 pivot;
        public Vector2 loadedPivot;
        public bool changed;
        public bool pass;
    }

    [Serializable]
    private sealed class ImportReport
    {
        public string utc;
        public ImportRecord[] records;
    }

    // Only the 39 new PNG importers may be corrected; front art remains untouched.
    public static void AuditDirectionImports()
    {
        RequireEditMode();
        Directory.CreateDirectory(DirectionsOutput);
        var recordList = new List<ImportRecord>();
        foreach (var name in HeroList.Concat(MonsterList).Concat(new[] { "boar_telegraph", "puffer_inflate" }))
        {
            var hero = HeroList.Contains(name);
            var folder = Art + (hero ? "/Characters/" : "/Monsters/");
            var reference = folder + name.Split('_')[0] + "_dq_front.png";
            var front = (TextureImporter)AssetImporter.GetAtPath(reference);
            var directionList = hero || name.Contains("_") ? new[] { "down", "right", "upright", "up" } : new[] { "right", "upright", "up" };
            foreach (var direction in directionList)
            {
                var path = folder + name + "_dq_" + direction + ".png";
                var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                var changed = importer.textureType != TextureImporterType.Sprite || importer.spriteImportMode != SpriteImportMode.Single
                    || importer.spritePixelsPerUnit != 48 || importer.filterMode != FilterMode.Point
                    || importer.textureCompression != TextureImporterCompression.Uncompressed || importer.mipmapEnabled
                    || Vector2.Distance(importer.spritePivot, front.spritePivot) > .00001f;
                if (changed)
                {
                    importer.textureType = TextureImporterType.Sprite;
                    importer.spriteImportMode = SpriteImportMode.Single;
                    importer.spritePixelsPerUnit = 48;
                    importer.filterMode = FilterMode.Point;
                    importer.textureCompression = TextureImporterCompression.Uncompressed;
                    importer.mipmapEnabled = false;
                    var settings = new TextureImporterSettings();
                    importer.ReadTextureSettings(settings);
                    settings.spriteAlignment = (int)SpriteAlignment.Custom;
                    settings.spritePivot = front.spritePivot;
                    importer.SetTextureSettings(settings);
                    importer.SaveAndReimport();
                }
                var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                var pivot = sprite.pivot / sprite.rect.size;
                recordList.Add(new ImportRecord
                {
                    path = path, reference = reference, type = importer.textureType.ToString(), mode = importer.spriteImportMode.ToString(),
                    ppu = sprite.pixelsPerUnit, filter = importer.filterMode.ToString(), compression = importer.textureCompression.ToString(),
                    mipmaps = importer.mipmapEnabled, pivot = importer.spritePivot, loadedPivot = pivot, changed = changed,
                    pass = sprite.pixelsPerUnit == 48 && Vector2.Distance(pivot, front.spritePivot) < .00001f
                });
            }
        }
        File.WriteAllText(DirectionsOutput + "/imports.json", JsonUtility.ToJson(new ImportReport { utc = DateTime.UtcNow.ToString("O"), records = recordList.ToArray() }, true));
        if (recordList.Count != 39 || recordList.Any(r => !r.pass))
        {
            throw new InvalidOperationException("D1_IMPORT_MISMATCH");
        }
    }

    public static void HideDirections()
    {
        var root = UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects().SingleOrDefault(go => go.name == DirectionsRoot);
        if (root == null || !root.activeSelf)
        {
            return;
        }
        var restore = root.GetComponentInChildren<Camera>(true);
        var camera = Camera.main;
        camera.transform.SetPositionAndRotation(restore.transform.position, restore.transform.rotation);
        camera.fieldOfView = restore.fieldOfView;
        camera.aspect = restore.aspect;
        camera.orthographic = restore.orthographic;
        camera.orthographicSize = restore.orthographicSize;
        root.SetActive(false);
    }

    public static void SelectDirections(string plate = "battle")
    {
        RequireEditMode();
        if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().path != DQLookdevBuilder.Scene)
        {
            throw new InvalidOperationException("Open Lookdev_DQ before selecting D1.");
        }
        if (plate != "heroes" && plate != "monsters" && plate != "battle")
        {
            throw new ArgumentException("Unknown D1 plate: " + plate);
        }
        HideMonsters();
        var previous = UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects().SingleOrDefault(go => go.name == DirectionsRoot);
        if (previous != null)
        {
            Object.DestroyImmediate(previous);
        }
        var root = new GameObject(DirectionsRoot).transform;
        var camera = Camera.main;
        var restore = new GameObject("D1 previous camera", typeof(Camera)).GetComponent<Camera>();
        restore.transform.SetParent(root);
        restore.CopyFrom(camera);
        restore.enabled = false;
        restore.transform.SetPositionAndRotation(camera.transform.position, camera.transform.rotation);
        camera.aspect = 1920f / 1080;
        var template = GameObject.Find("Hero_bruno_0").GetComponent<SpriteRenderer>();
        var shadeList = Object.FindObjectsByType<MeshFilter>().Where(f => f.name == "Foot contact shade").ToArray();
        var shadeMaterial = shadeList[0].GetComponent<MeshRenderer>().sharedMaterial;
        if (plate == "heroes")
        {
            for (var row = 0; row < 4; row++)
            {
                AddDirectionRow(root, template, shadeMaterial, HeroList[row], true, row, 7.5f - row * 4.2f);
            }
        }
        else if (plate == "monsters")
        {
            for (var row = 0; row < MonsterList.Length; row++)
            {
                var name = MonsterList[row];
                var z = 9 - row * 4;
                if (row < 3)
                {
                    AddDirectionRow(root, template, shadeMaterial, name, false, row, z);
                    continue;
                }
                var state = name == "boar" ? "boar_telegraph" : "puffer_inflate";
                for (var col = 0; col < 8; col++)
                {
                    // Within each direction pair: base on the left, state directly on the right.
                    AddDirectionActor(root, template, shadeMaterial, name, false, DirectionList[col], (col * 2 - 7.5f) * .84f, z, "row-" + row + "-base-" + col);
                    AddDirectionActor(root, template, shadeMaterial, state, false, DirectionList[col], (col * 2 + 1 - 7.5f) * .84f, z, "row-" + row + "-state-" + col);
                }
            }
        }
        else
        {
            var heroXList = new[] { -1.2f, 1.2f, -.8f, .8f };
            var heroZList = new[] { -1.3f, -1.3f, 1.9f, 1.9f };
            var heroDirectionList = new[] { "downleft", "right", "upleft", "up" };
            for (var j = 0; j < 4; j++)
            {
                AddDirectionActor(root, template, shadeMaterial, HeroList[j], true, heroDirectionList[j], heroXList[j], heroZList[j], "hero-" + j);
            }
            var nameList = new[] { "slime", "mushroom", "rock", "boar", "puffer", "slime", "mushroom", "boar_telegraph", "puffer_inflate" };
            var xList = new[] { -4.5f, 4.5f, 0f, -4.5f, 4.7f, -3.4f, 3.2f, -5.4f, 4.8f };
            var zList = new[] { 1.4f, 2f, 8.2f, -2.5f, -2.4f, 6.7f, 6.7f, 4.8f, 5f };
            for (var j = 0; j < nameList.Length; j++)
            {
                var nearest = Enumerable.Range(0, 4).OrderBy(k => Vector2.Distance(new Vector2(xList[j], zList[j]), new Vector2(heroXList[k], heroZList[k]))).First();
                var delta = new Vector2(heroXList[nearest] - xList[j], heroZList[nearest] - zList[j]);
                var angle = Mathf.Atan2(delta.x, -delta.y) * Mathf.Rad2Deg;
                var index = (Mathf.RoundToInt(angle / 45) + 8) % 8;
                AddDirectionActor(root, template, shadeMaterial, nameList[j], false, DirectionList[index], xList[j], zList[j], "monster-" + j);
            }
        }
        foreach (var sr in Object.FindObjectsByType<SpriteRenderer>().Where(sr => sr.name.StartsWith("Hero_", StringComparison.Ordinal) || sr.name.StartsWith("Monster_", StringComparison.Ordinal)))
        {
            sr.enabled = false;
        }
        foreach (var filter in shadeList)
        {
            filter.GetComponent<MeshRenderer>().enabled = false;
        }
    }

    private static void AddDirectionRow(Transform root, SpriteRenderer template, Material shadeMaterial, string name, bool hero, int row, float z)
    {
        for (var col = 0; col < 8; col++)
        {
            AddDirectionActor(root, template, shadeMaterial, name, hero, DirectionList[col], (col - 3.5f) * 1.5f, z, "row-" + row + "-col-" + col);
        }
    }

    private static void AddDirectionActor(Transform root, SpriteRenderer template, Material shadeMaterial, string name, bool hero, string direction, float x, float z, string id)
    {
        var flip = direction == "upleft" || direction == "left" || direction == "downleft";
        var sourceDirection = direction.Replace("left", "right");
        // Monster front diagonals reuse their already accepted front; no diagonal art was supplied.
        if (sourceDirection == "downright" || (!hero && !name.Contains("_") && sourceDirection == "down"))
        {
            sourceDirection = "front";
        }
        if (!hero && name.Contains("_") && sourceDirection == "front")
        {
            sourceDirection = "down";
        }
        var path = Art + (hero ? "/Characters/" : "/Monsters/") + name + "_dq_" + sourceDirection + ".png";
        var sr = Object.Instantiate(template, root);
        sr.name = "D1_" + id + "_" + name + "_" + direction;
        sr.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if (sr.sprite == null)
        {
            throw new InvalidOperationException("Missing D1 sprite: " + path);
        }
        sr.enabled = true;
        sr.flipX = flip;
        sr.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(Art + "/CharactersLit.mat");
        sr.transform.localScale = Vector3.one;
        sr.transform.rotation = Camera.main.transform.rotation;
        var foot = new Vector3(x, DQLookdevBuilder.Height(x, z) + .025f, z);
        sr.transform.position = foot;
        var source = AssetDatabase.LoadAssetAtPath<Mesh>(DQLookdevBuilder.Art + "/FootShade0.asset");
        var mesh = Object.Instantiate(source);
        var vertices = source.vertices;
        var center = source.bounds.center;
        var scale = hero ? 1f : name == "rock" ? 1.4f : .8f;
        for (var k = 0; k < vertices.Length; k++)
        {
            var wx = (vertices[k].x - center.x) * scale + foot.x;
            var wz = (vertices[k].z - center.z) * scale + foot.z;
            vertices[k] = new Vector3(wx, DQLookdevBuilder.Height(wx, wz) + .018f, wz);
        }
        mesh.vertices = vertices;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        var meshPath = Art + (hero ? "/Characters/" : "/Monsters/") + "D1-shade-" + id + ".asset";
        var saved = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
        if (saved == null)
        {
            AssetDatabase.CreateAsset(mesh, meshPath);
            saved = mesh;
        }
        else
        {
            saved.vertices = vertices;
            saved.RecalculateNormals();
            saved.RecalculateBounds();
            Object.DestroyImmediate(mesh);
            EditorUtility.SetDirty(saved);
            AssetDatabase.SaveAssetIfDirty(saved);
        }
        var shadow = new GameObject("D1 round foot shade", typeof(MeshFilter), typeof(MeshRenderer));
        shadow.transform.SetParent(root);
        shadow.GetComponent<MeshFilter>().sharedMesh = saved;
        shadow.GetComponent<MeshRenderer>().sharedMaterial = shadeMaterial;
    }

    public static void CaptureDirections()
    {
        RequireEditMode();
        Directory.CreateDirectory(DirectionsOutput);
        foreach (var plate in new[] { "heroes", "monsters", "battle" })
        {
            SelectDirections(plate);
            DQLookdevCapture.Capture(DirectionsOutput + "/D1-" + plate + ".png", 1920, 1080);
            var record = new Plate
            {
                id = "D1-" + plate, utc = DateTime.UtcNow.ToString("O"),
                camera = new[] { Camera.main.transform.position.x, Camera.main.transform.position.y, Camera.main.transform.position.z, Camera.main.transform.eulerAngles.x, Camera.main.fieldOfView },
                actors = GameObject.Find(DirectionsRoot).GetComponentsInChildren<SpriteRenderer>().Select(sr => Describe(sr, 1920, 1080)).ToArray()
            };
            File.WriteAllText(DirectionsOutput + "/D1-" + plate + ".json", JsonUtility.ToJson(record, true));
        }
        EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene(), DQLookdevBuilder.Scene);
        EditorSceneManager.OpenScene(DQLookdevBuilder.Scene, OpenSceneMode.Single);
        var actors = GameObject.Find(DirectionsRoot).GetComponentsInChildren<SpriteRenderer>();
        if (actors.Length != 13 || actors.Any(sr => sr.sprite == null || sr.sharedMaterial == null))
        {
            throw new InvalidOperationException("D1_ACTORS_NOT_SAVED");
        }
    }
}
