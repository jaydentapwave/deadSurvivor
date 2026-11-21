using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using DeadSurvivor.DQLookdev;
using Object = UnityEngine.Object;

public static class DQLookdevBuilder
{
    public const string Art = "Assets/Game/03.Resources/Art/HD2D/RoundDQ1";
    public const string Scene = "Assets/Game/01.Scene/Lookdev_DQ.unity";
    public const string Output = "output/hd2d-dq/r1";
    private const string Sprites = "Assets/Game/03.Resources/Art/AIGenerated";

    private static T Save<T>(T asset, string name) where T : Object
    {
        var path = Art + "/" + name;
        var previous = AssetDatabase.LoadAssetAtPath<T>(path);
        if (previous != null)
        {
            EditorUtility.CopySerialized(asset, previous);
            Object.DestroyImmediate(asset);
            EditorUtility.SetDirty(previous);
            return previous;
        }
        AssetDatabase.CreateAsset(asset, path);
        return asset;
    }

    private static Texture2D Texture(string name, bool repeat = false)
    {
        var path = Art + "/" + name + ".png";
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType = TextureImporterType.Default;
        importer.filterMode = FilterMode.Trilinear;
        importer.mipmapEnabled = true;
        importer.wrapMode = repeat ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.alphaIsTransparency = !repeat;
        importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    private static Material Material(string name, string shader, Color color, Texture2D texture = null)
    {
        var material = new Material(Shader.Find(shader)) { name = name };
        material.SetColor("_BaseColor", color);
        if (texture != null)
        {
            material.SetTexture("_BaseMap", texture);
        }
        material.SetFloat("_Smoothness", 0);
        return Save(material, name + ".mat");
    }

    public static float Height(float x, float z)
    {
        var radius = Mathf.Sqrt(x * x + z * z);
        var edge = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(11, 24, radius));
        var h = .23f * Mathf.Sin(x * .46f) * Mathf.Cos(z * .38f);
        h += edge * (1.35f + 1.05f * Mathf.Sin(x * .17f + z * .13f) * Mathf.Cos(z * .17f));
        // The pond is a shallow basin; no ledges.
        h -= .38f * Mathf.Exp(-((x + 8) * (x + 8) / 7 + (z - 1) * (z - 1) / 25));
        return h;
    }

    private static Mesh Ground()
    {
        const int count = 280;
        var vertices = new Vector3[(count + 1) * (count + 1)];
        var triangles = new int[count * count * 6];
        for (var z = 0; z <= count; z++)
        {
            for (var x = 0; x <= count; x++)
            {
                var wx = -70 + x * .5f;
                var wz = -35 + z * .5f;
                vertices[z * (count + 1) + x] = new Vector3(wx, Height(wx, wz), wz);
            }
        }
        var t = 0;
        for (var z = 0; z < count; z++)
        {
            for (var x = 0; x < count; x++)
            {
                var a = z * (count + 1) + x;
                triangles[t++] = a; triangles[t++] = a + count + 1; triangles[t++] = a + 1;
                triangles[t++] = a + 1; triangles[t++] = a + count + 1; triangles[t++] = a + count + 2;
            }
        }
        var mesh = new Mesh { name = "Smooth rolling meadow", indexFormat = IndexFormat.UInt32, vertices = vertices, triangles = triangles };
        mesh.RecalculateNormals(); mesh.RecalculateBounds();
        return Save(mesh, "Meadow.asset");
    }

    private static Mesh Canopy(int variant)
    {
        var vertices = new List<Vector3>();
        var normals = new List<Vector3>();
        var colors = new List<Color>();
        var uv = new List<Vector2>();
        var triangles = new List<int>();
        UnityEngine.Random.InitState(780 + variant);
        for (var j = 0; j < 160; j++)
        {
            var normal = UnityEngine.Random.onUnitSphere;
            var center = Vector3.Scale(normal, new Vector3(.48f + variant * .035f, .37f, .43f)) + Vector3.up * .85f;
            center += UnityEngine.Random.insideUnitSphere * .09f;
            var right = Vector3.Cross(normal, Vector3.up).normalized;
            if (right.sqrMagnitude < .1f)
            {
                right = Vector3.right;
            }
            var up = Vector3.Cross(right, normal);
            var size = UnityEngine.Random.Range(.19f, .32f);
            var first = vertices.Count;
            vertices.Add(center - right * size - up * size);
            vertices.Add(center + right * size - up * size);
            vertices.Add(center + right * size + up * size);
            vertices.Add(center - right * size + up * size);
            var tint = Color.white * UnityEngine.Random.Range(.8f, 1.15f);
            for (var k = 0; k < 4; k++)
            {
                normals.Add(normal); colors.Add(tint);
            }
            uv.Add(Vector2.zero); uv.Add(Vector2.right); uv.Add(Vector2.one); uv.Add(Vector2.up);
            triangles.AddRange(new[] { first, first + 1, first + 2, first, first + 2, first + 3 });
        }
        var mesh = new Mesh { name = "Round leaf bouquet " + variant };
        mesh.SetVertices(vertices); mesh.SetNormals(normals); mesh.SetColors(colors); mesh.SetUVs(0, uv); mesh.SetTriangles(triangles, 0); mesh.RecalculateBounds();
        return Save(mesh, "Canopy" + variant + ".asset");
    }

    private static GameObject MeshObject(string name, Mesh mesh, Material material, Vector3 position, Transform parent = null)
    {
        var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
        go.transform.SetParent(parent); go.transform.position = position;
        go.GetComponent<MeshFilter>().sharedMesh = mesh;
        go.GetComponent<MeshRenderer>().sharedMaterial = material;
        return go;
    }

    private static GameObject Primitive(string name, PrimitiveType type, Material material, Vector3 position, Vector3 scale, Transform parent = null)
    {
        var go = GameObject.CreatePrimitive(type); go.name = name;
        Object.DestroyImmediate(go.GetComponent<Collider>());
        go.transform.SetParent(parent); go.transform.position = position; go.transform.localScale = scale;
        go.GetComponent<Renderer>().sharedMaterial = material;
        return go;
    }

    private static Mesh Card()
    {
        var mesh = new Mesh { vertices = new[] { new Vector3(-.5f, 0, 0), new Vector3(.5f, 0, 0), new Vector3(.5f, 1, 0), new Vector3(-.5f, 1, 0) }, uv = new[] { Vector2.zero, Vector2.right, Vector2.one, Vector2.up }, triangles = new[] { 0, 2, 1, 0, 3, 2 } };
        mesh.RecalculateNormals(); mesh.RecalculateBounds(); return Save(mesh, "Card.asset");
    }

    [MenuItem("DeadSurvivor/DQ/Build trial")]
    public static void Build()
    {
        if (Application.isPlaying)
        {
            throw new InvalidOperationException("Do not replace a playing scene.");
        }
        var oldGuid = AssetDatabase.AssetPathToGUID(Scene);
        // The user's unsaved Main is deliberately neither saved nor prompted.
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var grassTexture = Texture("Velvet", true);
        var leafTexture = Texture("Leaves");
        var grassCardTexture = Texture("Grass");
        var groundMaterial = Material("Meadow", "DeadSurvivor/DQ/Meadow", new Color(.13f, .71f, .29f), grassTexture);
        var leafMaterial = Material("Leaves", "DeadSurvivor/DQ/Leaves", new Color(.035f, .55f, .32f), leafTexture);
        var grassMaterial = Material("Grass", "DeadSurvivor/DQ/Leaves", new Color(.18f, .55f, .13f), grassCardTexture);
        var bark = Material("Bark", "Universal Render Pipeline/Lit", new Color(.16f, .12f, .067f));
        var rock = Material("Rock", "Universal Render Pipeline/Lit", new Color(.31f, .39f, .33f));
        var card = Card();
        var shadeMaterial = Material("CanopyShade", "DeadSurvivor/DQ/Projected shade", Color.white, Texture("CanopyShadow"));
        MeshObject("DQ meadow 60m + distant apron", Ground(), groundMaterial, Vector3.zero);
        var canopyList = Enumerable.Range(0, 3).Select(Canopy).ToArray();
        var forest = new GameObject("Clustered forest; center radius12 clear").transform;
        UnityEngine.Random.InitState(21121);
        // Place contiguous crescents along the clear area's boundary, then distant groves.
        for (var j = 0; j < 460; j++)
        {
            float x, z;
            if (j < 150)
            {
                var a = Mathf.Lerp(-100, 100, (j % 50) / 49f) * Mathf.Deg2Rad;
                var radius = 13.1f + (j / 50) * .75f + UnityEngine.Random.Range(-.2f, .2f);
                x = Mathf.Sin(a) * radius;
                z = Mathf.Cos(a) * radius;
            }
            else
            {
                x = UnityEngine.Random.Range(-35f, 35f);
                z = UnityEngine.Random.Range(22f, 66f);
            }
            if (Mathf.Abs(x - (3.4f + 1.7f * Mathf.Sin(z * .13f))) < 1.4f)
            {
                continue;
            }
            var mesh = canopyList[j % 3];
            var size = UnityEngine.Random.Range(.9f, 1.6f) / mesh.bounds.max.y;
            var root = new GameObject("Tree " + j + " variant " + j % 3).transform;
            root.SetParent(forest); root.position = new Vector3(x, Height(x, z), z);
            MeshObject("Leaves", mesh, leafMaterial, root.position, root).transform.localScale = Vector3.one * size;
            Primitive("Trunk", PrimitiveType.Cylinder, bark, root.position + Vector3.up * .28f * size, new Vector3(.11f, .28f, .11f) * size, root);
            if (j < 150)
            {
                var shadowMesh = Shade(x + .6f * size, z - .7f * size, .83f * size);
                MeshObject("Sun projected canopy shade", Save(shadowMesh, "Shade" + j + ".asset"), shadeMaterial, Vector3.zero, root);
            }
        }
        var edgeRoot = new GameObject("Edge grasses and flowers").transform;
        var flowerColors = new[] { new Color(.7f, .68f, .25f), new Color(.63f, .32f, .64f), new Color(.82f, .85f, .76f) };
        var flowerMaterialList = flowerColors.Select((c, i) => Material("Flower" + i, "Universal Render Pipeline/Unlit", c)).ToArray();
        for (var j = 0; j < 500; j++)
        {
            var x = UnityEngine.Random.Range(-18f, 18f); var z = UnityEngine.Random.Range(-9f, 30f);
            if (x * x + z * z < 110 || Mathf.Abs(x) < 7)
            {
                continue;
            }
            var pos = new Vector3(x, Height(x, z), z);
            var go = MeshObject("Grass tuft", card, grassMaterial, pos, edgeRoot);
            go.transform.localScale = Vector3.one * UnityEngine.Random.Range(.18f, .38f);
            go.transform.rotation = Quaternion.Euler(0, UnityEngine.Random.Range(0, 180), 0);
            if (j % 5 == 0)
            {
                Primitive("Flower", PrimitiveType.Sphere, flowerMaterialList[j % 3], pos + Vector3.up * .18f, new Vector3(.065f, .035f, .065f), edgeRoot);
            }
        }
        for (var j = 0; j < 8; j++)
        {
            var x = j < 4 ? -6.5f + j * .32f : 7 + (j - 4) * .42f;
            var z = j < 4 ? 8f + j * .36f : -2.4f + (j - 4) * .3f;
            var go = Primitive("Angular grey green rock", PrimitiveType.Sphere, rock, new Vector3(x, Height(x, z) + .12f, z), new Vector3(.7f, .6f, .6f));
            var original = go.GetComponent<MeshFilter>().sharedMesh;
            var mesh = Object.Instantiate(original);
            // Quantized radial displacement makes irregular stone facets without a pixel texture.
            var vertices = mesh.vertices;
            for (var k = 0; k < vertices.Length; k++)
            {
                var v = vertices[k]; vertices[k] = new Vector3(Mathf.Round(v.x * 6) / 6, Mathf.Round(v.y * 6) / 6, Mathf.Round(v.z * 6) / 6);
            }
            mesh.vertices = vertices; mesh.RecalculateNormals(); mesh.RecalculateBounds();
            go.GetComponent<MeshFilter>().sharedMesh = Save(mesh, "Rock" + j + ".asset");
        }
        BuildWater();
        BuildActors();
        var camera = new GameObject("DQ Camera", typeof(Camera), typeof(UniversalAdditionalCameraData)).GetComponent<Camera>();
        camera.SetVolumeFrameworkUpdateMode(VolumeFrameworkUpdateMode.EveryFrame);
        camera.tag = "MainCamera"; camera.nearClipPlane = .1f; camera.farClipPlane = 180; camera.allowHDR = true;
        camera.backgroundColor = new Color(.45f, .69f, .8f); camera.clearFlags = CameraClearFlags.SolidColor;
        var data = camera.GetUniversalAdditionalCameraData(); data.renderPostProcessing = true; data.antialiasing = AntialiasingMode.None;
        var renderer = Object.Instantiate(AssetDatabase.LoadAssetAtPath<UniversalRendererData>("Assets/Settings/Mobile_Renderer.asset"));
        renderer.rendererFeatures.Clear();
        var focus = ScriptableObject.CreateInstance<FullScreenPassRendererFeature>();
        focus.name = "DQ optical focus band";
        focus.passMaterial = Save(new Material(Shader.Find("Hidden/DeadSurvivor/DQ/Focus")), "Focus.mat");
        focus.injectionPoint = FullScreenPassRendererFeature.InjectionPoint.AfterRenderingPostProcessing;
        focus.requirements = ScriptableRenderPassInput.Color;
        var rendererPath = Art + "/Renderer.asset";
        var savedRenderer = Save(renderer, "Renderer.asset");
        AssetDatabase.AddObjectToAsset(focus, rendererPath); savedRenderer.rendererFeatures.Add(focus); EditorUtility.SetDirty(savedRenderer);
        var scope = camera.gameObject.AddComponent<DQCameraScope>(); scope.RendererData = savedRenderer; scope.RefreshRenderer();
        var sun = new GameObject("Sun from upper left", typeof(Light)).GetComponent<Light>();
        sun.type = LightType.Directional; sun.transform.rotation = Quaternion.Euler(48, -35, 0); sun.intensity = 1.05f; sun.shadows = LightShadows.Soft; sun.shadowStrength = 1;
        RenderSettings.sun = sun; RenderSettings.ambientMode = AmbientMode.Flat; RenderSettings.ambientLight = new Color(.035f, .09f, .065f); RenderSettings.ambientIntensity = 1;
        RenderSettings.fog = false; RenderSettings.skybox = null;
        var profile = ScriptableObject.CreateInstance<VolumeProfile>();
        var bloom = profile.Add<Bloom>(true); bloom.threshold.value = 1.4f; bloom.intensity.value = .1f; bloom.scatter.value = .4f;
        var volume = new GameObject("DQ subtle water bloom", typeof(Volume)).GetComponent<Volume>();
        volume.isGlobal = true; volume.sharedProfile = Save(profile, "Volume.asset"); AssetDatabase.AddObjectToAsset(bloom, volume.sharedProfile);
        Configure(28);
        AssetDatabase.SaveAssets();
        EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene(), Scene);
        EditorSceneManager.OpenScene(Scene, OpenSceneMode.Single);
        if (GameObject.Find("Actors").transform.childCount != 44 || Camera.main.GetComponent<DQCameraScope>().RendererData == null || Object.FindAnyObjectByType<Volume>().sharedProfile == null)
        {
            throw new InvalidOperationException("DQ saved references missing.");
        }
        if (!string.IsNullOrEmpty(oldGuid) && oldGuid != AssetDatabase.AssetPathToGUID(Scene))
        {
            throw new InvalidOperationException("DQ scene GUID changed.");
        }
    }

    private static void BuildWater()
    {
        var water = Material("Water", "Universal Render Pipeline/Lit", new Color(.09f, .39f, .44f, .78f));
        water.SetFloat("_Surface", 1); water.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha); water.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha); water.SetFloat("_ZWrite", 0); water.SetFloat("_Smoothness", .9f); water.EnableKeyword("_SURFACE_TYPE_TRANSPARENT"); water.renderQueue = 3000;
        var plane = Primitive("Shallow turquoise pond", PrimitiveType.Cylinder, water, new Vector3(-8, -.13f, 1), new Vector3(4.5f, .005f, 9));
        plane.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
        var foam = Material("Foam", "Universal Render Pipeline/Unlit", new Color(.7f, .88f, .85f));
        for (var j = 0; j < 100; j++)
        {
            var a = j * Mathf.PI * 2 / 100;
            Primitive("Broken foam shoreline", PrimitiveType.Sphere, foam, new Vector3(-8 + 2.15f * Mathf.Cos(a), -.11f, 1 + 4.35f * Mathf.Sin(a)), new Vector3(.11f, .015f, .07f));
        }
        var glint = Material("Water sparkles", "Universal Render Pipeline/Unlit", new Color(3, 3, 3));
        for (var j = 0; j < 18; j++)
        {
            var pos = new Vector3(-8 + UnityEngine.Random.Range(-1.4f, 1.4f), -.1f, UnityEngine.Random.Range(-2.3f, 4.3f));
            Primitive("Sparkle", PrimitiveType.Sphere, glint, pos, new Vector3(.11f, .01f, .045f));
        }
    }

    private static void BuildActors()
    {
        var root = new GameObject("Actors").transform;
        var material = Save(new Material(Shader.Find("DeadSurvivor/DQ/Sprite")), "Actors.mat");
        material.SetFloat("_Surface", 1); material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha); material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha); material.SetFloat("_ZWrite", 1); material.SetFloat("_Cull", 0); material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT"); material.renderQueue = 3000;
        var blobMaterial = new Material(Shader.Find("DeadSurvivor/DQ/Sprite")); blobMaterial.mainTexture = Texture("Blob"); blobMaterial = Save(blobMaterial, "Blob.mat");
        var blobTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(Art + "/Blob.png");
        var importer = (TextureImporter)AssetImporter.GetAtPath(Art + "/Blob.png"); importer.textureType = TextureImporterType.Sprite; importer.spritePixelsPerUnit = 128; importer.SaveAndReimport();
        var blobSprite = AssetDatabase.LoadAssetAtPath<Sprite>(Art + "/Blob.png");
        var heroList = new[] { "bruno", "lena", "mira", "bolt" };
        for (var j = 0; j < 44; j++)
        {
            var isHero = j < 4;
            var id = isHero ? heroList[j] : new[] { "slime", "mushroom", "boar", "rock", "puffer" }[(j - 4) % 5];
            var path = isHero ? Sprites + "/Characters/" + id + "/front/" + id + "_front.png" : Sprites + "/Monsters/" + id + "/" + id + ".png";
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite == null)
            {
                throw new InvalidOperationException("Missing actor " + path);
            }
            var go = new GameObject((isHero ? "Hero_" : "Monster_") + id + "_" + j, typeof(SpriteRenderer)); go.transform.SetParent(root);
            float x, z;
            if (isHero)
            {
                x = (j % 2 - .5f) * 1.25f; z = (j / 2 - .5f) * 1.9f;
            }
            else
            {
                var n = j - 4; var row = n / 8; var col = n % 8;
                x = (col - 3.5f) * 2.05f + .2f * Mathf.Sin(n * 3.7f);
                z = new[] { -6.1f, -3.5f, 3.6f, 8f, 12.2f }[row];
            }
            if (!isHero)
            {
                if (x < -6 && z > -4 && z < 7)
                {
                    x += 1.8f;
                }
                z += .42f * Mathf.Sin(j * 2.1f);
                x += .25f * Mathf.Cos(j * 4.7f);
            }
            go.transform.position = new Vector3(x, Height(x, z) + .025f, z);
            var sr = go.GetComponent<SpriteRenderer>(); sr.sprite = sprite; sr.sharedMaterial = material; sr.shadowCastingMode = ShadowCastingMode.Off;
            var shadow = new GameObject("Blob shadow", typeof(SpriteRenderer)); shadow.transform.position = new Vector3(x, Height(x, z) + .012f, z); shadow.transform.rotation = Quaternion.Euler(90, 0, 0); shadow.transform.localScale = new Vector3(.52f, .4f, 1); shadow.transform.SetParent(go.transform, true);
            shadow.GetComponent<SpriteRenderer>().sprite = blobSprite; shadow.GetComponent<SpriteRenderer>().sharedMaterial = blobMaterial;
        }
    }

    public static void Configure(float angle)
    {
        var camera = Camera.main; camera.fieldOfView = 20; camera.orthographic = false; camera.aspect = 1920f / 1080;
        camera.transform.rotation = Quaternion.Euler(angle, 0, 0);
        var focus = new Vector3(0, .6f, 0);
        var hero = GameObject.Find("Hero_bruno_0").GetComponent<SpriteRenderer>();
        var source = new Texture2D(2, 2); source.LoadImage(File.ReadAllBytes(AssetDatabase.GetAssetPath(hero.sprite)));
        var pixelList = source.GetPixels32(); var min = source.height; var max = 0;
        for (var y = 0; y < source.height; y++)
        {
            for (var x = 0; x < source.width; x++)
            {
                if (pixelList[y * source.width + x].a >= 128)
                {
                    min = Mathf.Min(min, y); max = Mathf.Max(max, y + 1);
                }
            }
        }
        var actorHeight = (max - min) / hero.sprite.pixelsPerUnit;
        Object.DestroyImmediate(source);
        var distance = actorHeight / (2 * Mathf.Tan(10 * Mathf.Deg2Rad) * .0889f);
        camera.transform.position = focus - camera.transform.forward * distance;
        foreach (var sr in GameObject.Find("Actors").GetComponentsInChildren<SpriteRenderer>().Where(s => s.name != "Blob shadow"))
        {
            sr.transform.rotation = camera.transform.rotation;
        }
        foreach (var shadow in GameObject.Find("Actors").GetComponentsInChildren<SpriteRenderer>().Where(s => s.name == "Blob shadow"))
        {
            shadow.transform.rotation = Quaternion.Euler(90, 0, 0);
        }
    }

    private static Mesh Shade(float x, float z, float radius)
    {
        const int n = 8;
        var vertices = new Vector3[(n + 1) * (n + 1)];
        var uv = new Vector2[vertices.Length];
        var triangles = new List<int>();
        for (var row = 0; row <= n; row++)
        {
            for (var col = 0; col <= n; col++)
            {
                var wx = x + (col / (float)n - .5f) * radius * 2;
                var wz = z + (row / (float)n - .5f) * radius * 2;
                var index = row * (n + 1) + col;
                vertices[index] = new Vector3(wx, Height(wx, wz) + .018f, wz);
                uv[index] = new Vector2(col / (float)n, row / (float)n);
                if (row < n && col < n)
                {
                    triangles.AddRange(new[] { index, index + n + 1, index + 1, index + 1, index + n + 1, index + n + 2 });
                }
            }
        }
        var mesh = new Mesh { vertices = vertices, uv = uv, triangles = triangles.ToArray() };
        mesh.RecalculateNormals(); mesh.RecalculateBounds(); return mesh;
    }
}
