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
    public const string Output = "output/hd2d-dq/r2";
    private const string Sprites = "Assets/Game/03.Resources/Art/AIGenerated";

    private static T Save<T>(T asset, string name) where T : Object
    {
        var path = Art + "/" + name;
        var previous = AssetDatabase.LoadAssetAtPath<T>(path);
        if (previous != null)
        {
            if (asset is Mesh source && previous is Mesh target)
            {
                // CopySerialized does not invalidate live GPU mesh buffers reliably.
                // Explicit setters update both the native render mesh and serialized source.
                target.Clear();
                target.indexFormat = source.indexFormat;
                target.vertices = source.vertices;
                target.normals = source.normals;
                target.colors = source.colors;
                target.uv = source.uv;
                target.subMeshCount = source.subMeshCount;
                for (var i = 0; i < source.subMeshCount; i++)
                {
                    target.SetTriangles(source.GetTriangles(i), i);
                }
                target.RecalculateBounds();
                target.UploadMeshData(false);
            }
            else
            {
                EditorUtility.CopySerialized(asset, previous);
            }
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
        var edge = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(11.5f, 19, radius));
        var h = .16f * Mathf.Sin(x * .32f + z * .19f) + .08f * Mathf.Cos(z * .43f - x * .13f);
        h += edge * (1.8f + .9f * Mathf.Sin(x * .22f + z * .18f) * Mathf.Cos(z * .19f));
        var far = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(16, 25, z));
        h += far * (2.7f * Mathf.Exp(-((x + 8) * (x + 8) / 100 + (z - 29) * (z - 29) / 100))
            + 3.8f * Mathf.Exp(-((x - 15) * (x - 15) / 150 + (z - 43) * (z - 43) / 180))
            + 4.2f * Mathf.Exp(-((x + 20) * (x + 20) / 300 + (z - 65) * (z - 65) / 230)));
        var pondRadius = Mathf.Sqrt((x + 8) * (x + 8) / 6.25f + (z - 1) * (z - 1) / 20.25f);
        var basin = 1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.58f, 1.2f, pondRadius));
        h = Mathf.Lerp(h, -.52f, basin);
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
        var lobeList = new[]
        {
            new Vector3(-.32f, .68f, -.10f), new Vector3(.31f, .66f, -.07f),
            new Vector3(0, .96f, .04f), new Vector3(-.18f, .79f, .30f),
            new Vector3(.25f, .83f, .24f), new Vector3(-.02f, .61f, -.32f)
        };
        for (var j = 0; j < 264; j++)
        {
            var normal = UnityEngine.Random.onUnitSphere;
            var lobe = lobeList[j % lobeList.Length];
            if (variant == 1)
            {
                lobe = Vector3.Scale(lobe, new Vector3(1.28f, .78f, .95f));
            }
            if (variant == 2)
            {
                lobe = Vector3.Scale(lobe, new Vector3(.85f, 1.10f, 1.16f));
            }
            var center = lobe + Vector3.Scale(normal, new Vector3(.27f, .23f, .25f));
            var right = Vector3.Cross(normal, Vector3.up).normalized;
            if (right.sqrMagnitude < .1f)
            {
                right = Vector3.right;
            }
            var up = Vector3.Cross(right, normal);
            var size = UnityEngine.Random.Range(.09f, .17f);
            var first = vertices.Count;
            vertices.Add(center - right * size - up * size);
            vertices.Add(center + right * size - up * size);
            vertices.Add(center + right * size + up * size);
            vertices.Add(center - right * size + up * size);
            // Lobe surface normals produce continuous form lighting across leaf cards.
            var tint = Color.white * UnityEngine.Random.Range(.94f, 1.06f);
            var crownCenter = variant == 1 ? .60f : variant == 2 ? .80f : .72f;
            var crownNormal = new Vector3(center.x / .6f, (center.y - crownCenter) / .45f, center.z / .6f).normalized;
            var lightingNormal = (crownNormal * .8f + normal * .2f).normalized;
            for (var k = 0; k < 4; k++)
            {
                normals.Add(lightingNormal); colors.Add(tint);
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
        grassMaterial.SetFloat("_Grass", 1);
        var bark = Material("Bark", "Universal Render Pipeline/Lit", new Color(.16f, .12f, .067f));
        var rock = Material("Rock", "DeadSurvivor/DQ/Faceted rock", new Color(.31f, .39f, .33f));
        var card = Card();
        var shadeMaterial = Material("CanopyShade", "DeadSurvivor/DQ/Projected shade", Color.white, Texture("CanopyShadow"));
        MeshObject("DQ meadow 60m + distant apron", Ground(), groundMaterial, Vector3.zero);
        var canopyList = Enumerable.Range(0, 3).Select(Canopy).ToArray();
        var forest = new GameObject("Clustered forest; center radius12 clear").transform;
        UnityEngine.Random.InitState(21121);
        var groveCenterList = new[]
        {
            new Vector2(-12.8f, -2), new Vector2(-12.2f, 7), new Vector2(-10.2f, 13.5f),
            new Vector2(12.9f, -1), new Vector2(12.2f, 8.5f), new Vector2(10.5f, 15.5f),
            new Vector2(-3.7f, 20), new Vector2(-15, 26), new Vector2(16, 28),
            new Vector2(-24, 42), new Vector2(2, 43), new Vector2(25, 51),
            new Vector2(-6.5f, 15.5f), new Vector2(-1.5f, 19.5f), new Vector2(8, 16)
        };
        for (var j = 0; j < 545; j++)
        {
            var grove = j < 525 ? groveCenterList[j / 35] : new[] { new Vector2(-10.4f, 7.5f), new Vector2(10.5f, 7.4f), new Vector2(-7.3f, 10.5f), new Vector2(7.5f, 11) }[(j - 525) / 5];
            var offset = UnityEngine.Random.insideUnitCircle;
            offset = Vector2.Scale(offset, j < 525 ? new Vector2(3.3f, 2.6f) : new Vector2(.8f, .7f));
            var x = grove.x + offset.x;
            var z = grove.y + offset.y;
            if (x * x + z * z < 12.1f * 12.1f || Mathf.Abs(x - (3.4f + 1.7f * Mathf.Sin(z * .13f))) < 1.25f)
            {
                continue;
            }
            var mesh = canopyList[j % 3];
            var size = UnityEngine.Random.Range(1.05f, 1.6f) / mesh.bounds.max.y;
            var root = new GameObject("Tree " + j + " variant " + j % 3).transform;
            root.SetParent(forest); root.position = new Vector3(x, Height(x, z), z);
            MeshObject("Leaves", mesh, leafMaterial, root.position, root).transform.localScale = Vector3.one * size;
            Primitive("Short trunk", PrimitiveType.Cylinder, bark, root.position + Vector3.up * .10f * size, new Vector3(.10f, .10f, .10f) * size, root);
            if (j < 545)
            {
                var shadowMesh = Shade(x + .45f * size, z + .55f * size, .80f * size);
                MeshObject("Sun projected canopy shade", Save(shadowMesh, "Shade" + j + ".asset"), shadeMaterial, Vector3.zero, root);
            }
        }
        var edgeRoot = new GameObject("Meadow vegetation clusters").transform;
        var flowerColors = new[] { new Color(.96f, .81f, .31f), new Color(.91f, .52f, .65f), new Color(.94f, .97f, .89f) };
        var flowerMaterialList = flowerColors.Select((c, i) => Material("Flower" + i, "Universal Render Pipeline/Unlit", c)).ToArray();
        for (var j = 0; j < 2200; j++)
        {
            var x = UnityEngine.Random.Range(-17f, 17f);
            var z = UnityEngine.Random.Range(-10f, 28f);
            var radius = Mathf.Sqrt(x * x + z * z);
            var pondRadius = Mathf.Sqrt((x + 8) * (x + 8) / 6.25f + (z - 1) * (z - 1) / 20.25f);
            var roadEdge = Mathf.Abs(x - (3.4f + 1.7f * Mathf.Sin(z * .13f)));
            var isBorder = (radius > 10.5f && radius < 17) || (pondRadius > 1.08f && pondRadius < 1.48f) || (roadEdge > .9f && roadEdge < 1.6f);
            if (pondRadius < 1.06f || roadEdge < .85f || (!isBorder && j % 32 != 0))
            {
                continue;
            }
            var size = isBorder ? UnityEngine.Random.Range(.14f, .29f) : UnityEngine.Random.Range(.025f, .065f);
            var pos = new Vector3(x, Height(x, z) + .01f, z);
            for (var k = 0; k < 2; k++)
            {
                var go = MeshObject("Grass tuft", card, grassMaterial, pos, edgeRoot);
                go.transform.localScale = new Vector3(size * 1.6f, size, size);
                go.transform.rotation = Quaternion.Euler(0, 30 + 75 * k + j * 17, 0);
            }
            if (isBorder && j % 4 == 0)
            {
                for (var k = 0; k < 3; k++)
                {
                    var flower = pos + new Vector3((k - 1) * .075f, size * .8f + (k % 2) * .035f, .04f * k);
                    Primitive("Flower cluster", PrimitiveType.Sphere, flowerMaterialList[(j / 4) % 3], flower, new Vector3(.055f, .028f, .055f), edgeRoot);
                }
            }
        }
        var rockCenterList = new[] { new Vector2(-6.6f, 7.5f), new Vector2(7.2f, -1.7f), new Vector2(-9, 14) };
        for (var j = 0; j < 8; j++)
        {
            var center = rockCenterList[Mathf.Min(j / 3, 2)];
            var x = center.x + new[] { 0f, .72f, -.52f }[j % 3];
            var z = center.y + new[] { 0f, .48f, .66f }[j % 3];
            var go = MeshObject("Faceted stone cluster", Stone(j), rock, new Vector3(x, Height(x, z) - .06f, z));
            go.transform.localScale = new Vector3(.85f, .9f, .78f) * (j % 3 == 0 ? 1 : .55f);
            var shadowMesh = Shade(x + .15f, z + .1f, .65f);
            MeshObject("Stone ground shade", Save(shadowMesh, "StoneShade" + j + ".asset"), shadeMaterial, Vector3.zero);
        }
        BuildWater();
        BuildActors();
        var moteMaterial = Material("Sun motes", "Universal Render Pipeline/Unlit", new Color(2.4f, 2.05f, 1.1f));
        for (var j = 0; j < 24; j++)
        {
            var x = UnityEngine.Random.Range(-13f, 13f);
            var z = UnityEngine.Random.Range(-8f, 22f);
            if (x * x + z * z < 4)
            {
                continue;
            }
            Primitive("Warm drifting light", PrimitiveType.Sphere, moteMaterial, new Vector3(x, Height(x, z) + UnityEngine.Random.Range(.25f, .85f), z), Vector3.one * UnityEngine.Random.Range(.018f, .035f));
        }
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
        sun.type = LightType.Directional; sun.transform.rotation = Quaternion.Euler(48, 35, 0); sun.intensity = 1.05f; sun.color = new Color(1f, .96f, .86f); sun.shadows = LightShadows.Soft; sun.shadowStrength = 1;
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
        var water = Save(new Material(Shader.Find("DeadSurvivor/DQ/Shallow water")), "Water.mat");
        var mesh = new Mesh
        {
            vertices = new[] { new Vector3(-10.8f, -.13f, -4), new Vector3(-5.2f, -.13f, -4), new Vector3(-5.2f, -.13f, 6), new Vector3(-10.8f, -.13f, 6) },
            triangles = new[] { 0, 2, 1, 0, 3, 2 }
        };
        mesh.RecalculateNormals(); mesh.RecalculateBounds();
        MeshObject("Translucent pond and continuous foam", Save(mesh, "Pond.asset"), water, Vector3.zero).GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
    }

    private static Mesh Stone(int variant)
    {
        var phi = (1 + Mathf.Sqrt(5)) / 2;
        var points = new[]
        {
            new Vector3(-1, phi, 0), new Vector3(1, phi, 0), new Vector3(-1, -phi, 0), new Vector3(1, -phi, 0),
            new Vector3(0, -1, phi), new Vector3(0, 1, phi), new Vector3(0, -1, -phi), new Vector3(0, 1, -phi),
            new Vector3(phi, 0, -1), new Vector3(phi, 0, 1), new Vector3(-phi, 0, -1), new Vector3(-phi, 0, 1)
        };
        var indices = new[] {0,11,5, 0,5,1, 0,1,7, 0,7,10, 0,10,11, 1,5,9, 5,11,4, 11,10,2, 10,7,6, 7,1,8, 3,9,4, 3,4,2, 3,2,6, 3,6,8, 3,8,9, 4,9,5, 2,4,11, 6,2,10, 8,6,7, 9,8,1};
        var vertices = new Vector3[indices.Length];
        var triangles = new int[indices.Length];
        for (var j = 0; j < points.Length; j++)
        {
            points[j] = Vector3.Scale(points[j].normalized, new Vector3(.62f, .61f, .55f)) * (1 + .15f * Mathf.Sin(j * 12.3f + variant * 3.7f));
            points[j].y += .56f;
        }
        for (var j = 0; j < indices.Length; j++)
        {
            vertices[j] = points[indices[j]]; triangles[j] = j;
        }
        var mesh = new Mesh { vertices = vertices, triangles = triangles };
        mesh.RecalculateNormals(); mesh.RecalculateBounds();
        return Save(mesh, "Rock" + variant + ".asset");
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
                var radius = Mathf.Sqrt(x * x + z * z);
                if (radius > 11.3f)
                {
                    x *= 11.3f / radius;
                    z *= 11.3f / radius;
                }
            }
            go.transform.position = new Vector3(x, Height(x, z) + .025f, z);
            var sr = go.GetComponent<SpriteRenderer>(); sr.sprite = sprite; sr.sharedMaterial = material; sr.shadowCastingMode = ShadowCastingMode.Off;
            var footShade = Material("FootShade", "DeadSurvivor/DQ/Projected shade", Color.white, Texture("FootShadow"));
            var width = id == "rock" || id == "boar" ? .78f : isHero ? .55f : .46f;
            var shadowMesh = Shade(x, z, width * .78f);
            MeshObject("Foot contact shade", Save(shadowMesh, "FootShade" + j + ".asset"), footShade, Vector3.zero);

        }
    }

    public static void Configure(float angle)
    {
        var camera = Camera.main; camera.fieldOfView = 20; camera.orthographic = false; camera.aspect = 1920f / 1080;
        camera.transform.rotation = Quaternion.Euler(angle, 0, 0);
        var focus = new Vector3(0, .95f, 0);
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
