using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

public static class PixelLookdevSceneUtils
{
    public static PixelLookdevScope Scope { get; private set; }
    public static GameObject TreeSprites { get; private set; }
    public static GameObject RockSprites { get; private set; }
    public static GameObject TreeMeshes { get; private set; }
    public static GameObject RockMeshes { get; private set; }
    public static ParticleSystem Pollen { get; private set; }
    private static GameObject _root;
    private static Material _spriteMaterial;

    [MenuItem("DeadSurvivor/HD2D/Round2/Prepare Play Scene")]
    public static void Prepare()
    {
        if (!Application.isPlaying || UnityEngine.SceneManagement.SceneManager.GetActiveScene().path != LookdevSceneBuilder.ScenePath)
        {
            throw new InvalidOperationException("Enter Play in the existing Lookdev_HD2D scene first.");
        }
        if (_root != null)
        {
            throw new InvalidOperationException("Round2 is already prepared in this Play session.");
        }
        Object.FindAnyObjectByType<LookdevRenderScope>().enabled = false;
        _root = new GameObject("F2 transient lookdev");
        Scope = _root.AddComponent<PixelLookdevScope>();
        Scope.Initialize(AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>("Assets/Settings/Mobile_RPAsset.asset"), PixelLookdevAssets.Load<UniversalRendererData>("Renderer.asset"));
        Camera.main.GetUniversalAdditionalCameraData().SetRenderer(0);
        _spriteMaterial = new Material(AssetDatabase.LoadAssetAtPath<Material>(LookdevSceneBuilder.ArtPath + "/PixelSprite.mat")) { name = "F2 sprite shared", hideFlags = HideFlags.HideAndDontSave };
        TreeSprites = Group("Tree sprites");
        RockSprites = Group("Rock sprites");
        TreeMeshes = Group("Tree meshes");
        RockMeshes = Group("Rock meshes");
        var originalProps = GameObject.Find("Props palette left and world pixels right");
        foreach (Transform original in originalProps.transform)
        {
            var index = int.Parse(original.name.Split('_')[1]);
            var isTree = index % 3 == 0;
            var spriteFile = isTree ? "tree_01.png" : index % 3 == 1 ? "rock_02.png" : "rock_01.png";
            var position = new Vector3(original.position.x, 0, original.position.z);
            var sprite = Decor(spriteFile, position, isTree ? TreeSprites.transform : RockSprites.transform);
            var copy = Object.Instantiate(original.gameObject, isTree ? TreeMeshes.transform : RockMeshes.transform);
            copy.name = "Toon_" + original.name;
            var renderers = copy.GetComponentsInChildren<MeshRenderer>();
            var bounds = renderers[0].bounds;
            foreach (var renderer in renderers)
            {
                bounds.Encapsulate(renderer.bounds);
            }
            // Match the adopted sprite's physical silhouette height, not the FBX canvas.
            var targetHeight = AlphaHeight(sprite.sprite) / 48f;
            copy.transform.localScale *= targetHeight / bounds.size.y;
            bounds = renderers[0].bounds;
            foreach (var renderer in renderers)
            {
                bounds.Encapsulate(renderer.bounds);
                renderer.sharedMaterial = PixelLookdevAssets.Load<Material>(isTree ? "Surface_1.mat" : "Surface_2.mat");
                AddHull(renderer);
            }
            copy.transform.position += Vector3.up * -bounds.min.y;
        }
        originalProps.SetActive(false);
        GameObject.Find("48 texels per unit grass").GetComponent<MeshRenderer>().sharedMaterial = PixelLookdevAssets.Load<Material>("Surface_0.mat");
        GameObject.Find("046 main road study").SetActive(false);
        var shore = GameObject.Find("046_OB-006_Stones").GetComponent<MeshRenderer>();
        shore.sharedMaterial = PixelLookdevAssets.Load<Material>("Surface_2.mat");
        AddHull(shore);
        GameObject.Find("046_OB-006_Water").GetComponent<MeshRenderer>().sharedMaterial = PixelLookdevAssets.Load<Material>("Surface_3.mat");
        var extras = Group("Fence flowers and grass");
        for (var side = -1; side <= 1; side += 2)
        {
            Decor("fence_01.png", new Vector3(side * 4.8f,0,2.7f), extras.transform);
            Decor("fence_01.png", new Vector3(side * 5.3f,0,2.7f), extras.transform);
            for (var i = 0; i < 12; i++)
            {
                var file = i % 3 == 0 ? "grass_tuft_01.png" : i % 3 == 1 ? "flower_01.png" : "flower_02.png";
                Decor(file,new Vector3(side * (3.8f + (i % 4) * 0.72f),0,-4.5f + (i / 4) * 3.8f), extras.transform);
            }
        }
        RenderSettings.sun.transform.rotation = Quaternion.Euler(30f,135f,0f);
        RenderSettings.sun.color = new Color(1f,0.9f,0.76f);
        RenderSettings.sun.intensity = 1.15f;
        RenderSettings.sun.shadowBias = 0.01f;
        RenderSettings.sun.shadowNormalBias = 0.01f;
        var volume = Object.FindAnyObjectByType<Volume>();
        var profile = volume.profile;
        profile.TryGet<Bloom>(out var bloom);
        bloom.threshold.value = 1.8f;
        bloom.intensity.value = 0.4f;
        bloom.scatter.value = 0.25f;
        profile.TryGet<Vignette>(out var vignette);
        vignette.intensity.value = 0f;
        profile.TryGet<ColorAdjustments>(out var adjustments);
        adjustments.saturation.value = 0f;
        CreatePollen();
        Configure("F2c");
    }

    public static void Configure(string plate)
    {
        TreeSprites.SetActive(plate != "F2b");
        RockSprites.SetActive(plate == "F2a");
        TreeMeshes.SetActive(plate == "F2b");
        RockMeshes.SetActive(plate != "F2a");
        var camera = Camera.main;
        var hero = GameObject.Find("Member_bruno");
        hero.GetComponent<PixelWalkPreview>().enabled = false;
        hero.GetComponent<SpriteRenderer>().sprite = AssetDatabase.LoadAssetAtPath<Sprite>(LookdevSceneBuilder.SpritePath + "/Characters/bruno/front/bruno_front.png");
        TiltedCameraUtils.Configure(camera,30f,true,12f,plate == "F-int" ? 96f / 1080f : 0.1f,hero.transform.position + Quaternion.Euler(30,0,0)*Vector3.up*0.62f);
        // Orthographic distance does not change the composition. Keep all props
        // inside the 35m Mobile shadow range instead of clipping at the focus.
        camera.transform.position += camera.transform.forward * 15f;
        camera.GetUniversalAdditionalCameraData().renderPostProcessing = true;
        camera.GetUniversalAdditionalCameraData().antialiasing = AntialiasingMode.None;
        camera.allowMSAA = false;
        Scope.SetScreenEffects(true);
        Scope.RequireMobile();
    }

    public static GameObject CloseupProps(string plate)
    {
        var group = new GameObject("Closeup props");
        var tree = plate == "F2b" ? TreeMeshes.transform.GetChild(0) : TreeSprites.transform.GetChild(0);
        var rock = plate == "F2a" ? RockSprites.transform.GetChild(0) : RockMeshes.transform.GetChild(0);
        var treeCopy = Object.Instantiate(tree.gameObject, group.transform);
        var rockCopy = Object.Instantiate(rock.gameObject, group.transform);
        foreach (var renderer in group.GetComponentsInChildren<Renderer>(true))
        {
            renderer.enabled = true;
        }
        treeCopy.transform.position = new Vector3(-1.25f,treeCopy.transform.position.y,-0.4f);
        rockCopy.transform.position = new Vector3(1.25f,rockCopy.transform.position.y,-0.4f);
        return group;
    }

    private static SpriteRenderer Decor(string file, Vector3 position, Transform parent)
    {
        var body = LookdevSceneBuilder.AddSprite("F2 Decor " + file, LookdevSceneBuilder.SpritePath + "/Decor/" + file, position, _spriteMaterial, false);
        body.transform.SetParent(parent,true);
        // Initialize serialized inputs before Awake builds the shadow renderer.
        body.gameObject.SetActive(false);
        body.gameObject.AddComponent<PixelShadowCaster>().Initialize(body, AssetDatabase.LoadAssetAtPath<Material>(LookdevSceneBuilder.ArtPath + "/PixelShadow.mat"), true);
        body.gameObject.SetActive(true);
        return body;
    }

    private static GameObject Group(string name)
    {
        var group = new GameObject(name);
        group.transform.SetParent(_root.transform,false);
        return group;
    }

    private static void AddHull(MeshRenderer source)
    {
        var hull = new GameObject("Brown outline hull");
        hull.transform.SetParent(source.transform,false);
        hull.AddComponent<MeshFilter>().sharedMesh = source.GetComponent<MeshFilter>().sharedMesh;
        var renderer = hull.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = PixelLookdevAssets.Load<Material>("BrownHull.mat");
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
    }

    private static int AlphaHeight(Sprite sprite)
    {
        var texture = new Texture2D(2,2);
        texture.LoadImage(System.IO.File.ReadAllBytes(AssetDatabase.GetAssetPath(sprite)));
        var pixels = texture.GetPixels32();
        var minY = texture.height;
        var maxY = 0;
        for (var y = 0; y < texture.height; y++)
        {
            if (pixels.Skip(y * texture.width).Take(texture.width).Any(p => p.a >= 128))
            {
                minY = Mathf.Min(minY,y);
                maxY = Mathf.Max(maxY,y);
            }
        }
        Object.DestroyImmediate(texture);
        return maxY - minY + 1;
    }

    private static void CreatePollen()
    {
        Pollen = Group("Quiet pollen").AddComponent<ParticleSystem>();
        var main = Pollen.main;
        main.startLifetime = 18f;
        main.startSpeed = 0.035f;
        main.startSize = 0.018f;
        main.startColor = new Color(1f,0.87f,0.58f,0.35f);
        main.maxParticles = 36;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        var emission = Pollen.emission;
        emission.rateOverTime = 1.4f;
        var shape = Pollen.shape;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(17,1.5f,13);
        Pollen.transform.position = new Vector3(0,0.7f,0);
        var material = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit")) { hideFlags = HideFlags.HideAndDontSave };
        Pollen.GetComponent<ParticleSystemRenderer>().sharedMaterial = material;
        Pollen.Simulate(10f,true,true,false);
        Pollen.Play();
    }
}
