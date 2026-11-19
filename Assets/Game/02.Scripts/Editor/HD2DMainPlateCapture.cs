// Capture request adapted from Assets/Game/02.Scripts/Editor/HD2D/LookdevCaptureUtils.cs SHA256 0484349b2a7061cd636ebebaffded74825c7b5c457cef2cf50dd60a25a24f663
// Adapted from Assets/Game/02.Scripts/Editor/HD2D/Round8/HD2DMainPlateCapture.cs SHA256 2c3ba2e76cedb1fea37e934ef75e3f50421fbcc14c51c55f685ebbe018a0891b
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using VContainer;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static class HD2DMainPlateCapture
{
    public static string Output { get; private set; } = "output/hd2d-background/plates";
    public static string GateOutput { get; private set; } = "output/hd2d-관문/main1";

    public static void Configure(string output, string gateOutput)
    {
        if (_camera != null && _shot > 0 && _shot < 15)
        {
            throw new InvalidOperationException("Do not change a running collection.");
        }
        Output = output;
        GateOutput = gateOutput;
    }

    public static void CaptureStill(string path, int width = 1920, int height = 1080)
    {
        if (!Application.isPlaying)
        {
            throw new InvalidOperationException("Owned Main Play required.");
        }
        _camera = Camera.main;
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        CaptureCamera(path, width, height);
    }
    private static int _shot;
    private static int _lastFrame;
    private static double _next;
    private static Camera _camera;
    private static Vector3 _origin;
    private static int _pondCropX, _pondCropY;
    public const int CropX = 420, CropY = 96, CropWidth = 1224, CropHeight = 708, CropScale = 4;
    private static readonly List<Shot> ShotList = new List<Shot>();
    [Serializable] public sealed class Context { public string runId; public string planHash; }
    [Serializable] public sealed class Settings { public string quality; public float renderScale; public bool srpBatcher; public bool postProcessing; public string antialiasing; public int backgroundWidth; public int backgroundHeight; public int integerScale; public string backgroundFilter; public string shadows; public int outlineTexels; public float vignetteIntensity; public float vignetteSmoothness; }
    [Serializable] public sealed class Shot { public string file; public int frame; public Vector3 cameraPosition; public float orthoSize; public int width; public int height; public Settings settings; public bool banksInside; }
    [Serializable] public sealed class Collection { public Context context; public List<Shot> shots; public float ledgeHeight; public float shadowLength; public int[] pondCrop; }

    [MenuItem("DeadSurvivor/HD2D/Main/Capture All")]
    public static void Run()
    {
        if (!Application.isPlaying || !File.Exists(GateOutput + "/capture-context.json"))
        {
            throw new InvalidOperationException("Prepare a pre-capture declaration and enter owned Lookdev Play first.");
        }
        _camera = Camera.main;
        Directory.CreateDirectory(Output);
        Time.timeScale = 0;
        PlayableProbe.SetMove(0,0);
        _origin = _camera.transform.position;
        _shot = 0;
        _lastFrame = Time.frameCount;
        _next = EditorApplication.timeSinceStartup + 1;
        ShotList.Clear();
        EditorApplication.update += OnEditorUpdate;
    }

    public static Settings ActualSettings()
    {
        if (QualitySettings.names[QualitySettings.GetQualityLevel()] != "Mobile") throw new InvalidOperationException("Mobile quality required");
        var asset = (UniversalRenderPipelineAsset)QualitySettings.renderPipeline;
        var data = _camera.GetUniversalAdditionalCameraData();
        var vignette = VolumeManager.instance.stack.GetComponent<Vignette>();
        return new Settings { vignetteIntensity=vignette == null ? 0 : vignette.intensity.value,vignetteSmoothness=vignette == null ? 0 : vignette.smoothness.value, quality = QualitySettings.names[QualitySettings.GetQualityLevel()],renderScale = asset.renderScale,srpBatcher = asset.useSRPBatcher && GraphicsSettings.useScriptableRenderPipelineBatching,postProcessing = data.renderPostProcessing,antialiasing = data.antialiasing.ToString(),backgroundWidth = _camera.GetComponent<HD2DBackgroundRig>().LowWidth,backgroundHeight = _camera.GetComponent<HD2DBackgroundRig>().LowHeight,integerScale = _camera.GetComponent<HD2DBackgroundRig>().Scale,backgroundFilter = _camera.GetComponent<HD2DBackgroundRig>().Color.rt.filterMode.ToString(),shadows = RenderSettings.sun.shadows.ToString(),outlineTexels = 1 };
    }

    private static void OnEditorUpdate()
    {
        if (!Application.isPlaying)
        {
            Cleanup();
            return;
        }
        if (EditorApplication.timeSinceStartup < _next || Time.frameCount == _lastFrame)
        {
            return;
        }
        try
        {
            ActualSettings();
            _camera.transform.position = _origin;
            _lastFrame = Time.frameCount;
            switch (_shot)
            {
                case 0:
                    Capture("Main1-main.png",1920,1080);
                    File.WriteAllText(GateOutput + "/settings.json",JsonUtility.ToJson(ActualSettings(),true));
                    File.WriteAllText(GateOutput + "/camera.json","{\"mode\":\"ortho\",\"angle\":" + _camera.transform.eulerAngles.x + ",\"res\":[1920,1080],\"ortho_size\":" + _camera.orthographicSize + ",\"char_px\":48}");
                    break;
                case 1:
                    CaptureHeroMask();
                    break;
                case 2:
                    SetEffects(false);
                    Capture("Main1-repeat.png",1920,1080);
                    SetEffects(true);
                    CaptureGroundCut();
                    break;
                case 11:
                    _camera.transform.position = _origin;
                    Capture("Main1-2520x1080.png",2520,1080);
                    break;
                case 12:
                    _camera.orthographicSize = PixelScaleMath.OrthographicSize(1536,48,PixelScaleMath.Scale(1536,.1f,48,1));
                    Capture("Main1-2048x1536.png",2048,1536);
                    _camera.orthographicSize = 5.625f;
                    break;
                case 13:
                    CropMain("Main1-main.png","closeup-Main1.png",CropX,CropY,CropWidth,CropHeight,CropScale);
                    CropMain("Main1-hero-mask.png","Main1-close-hero-mask.png",CropX,CropY,CropWidth,CropHeight,CropScale);
                    var pondPosition = _camera.transform.position;
                    try
                    {
                        _camera.transform.position = PondFocus() - _camera.transform.forward * _camera.GetComponent<TiltedCameraRig>().Distance;
                        Capture("Main1-pond-view.png",1920,1080);
                        CropMain("Main1-pond-view.png","Main1-pond-corner.png",_pondCropX,_pondCropY,600,240,4);
                    }
                    finally { _camera.transform.position = pondPosition; }
                    break;
                case 14:
                    _camera.transform.position = _origin;
                    CaptureLighting();
                    CaptureBankDiagnostic();
                    break;
                case 15:
                    WriteCollection();
                    File.WriteAllText(GateOutput + "/runtime-state-after-capture.json",JsonUtility.ToJson(ActualSettings(),true));
                    File.WriteAllText(GateOutput + "/capture-complete.txt",DateTime.UtcNow.ToString("O"));
                    Cleanup();
                    return;
                default:
                    if (_shot >= 3 && _shot <= 10)
                    {
                        var index = _shot-3;
                        _camera.transform.position = _origin+Vector3.right*(index/48f);
                        Object.FindFirstObjectByType<GameSceneLifetimeScope>().Container.Resolve<FieldOcclusionPresenter>().Tick();
                        Capture("Main1-move-" + index + ".png",1920,1080);
                        SetEffects(false);
                        Capture("Main1-move-raw-" + index + ".png",1920,1080);
                        SetEffects(true);
                    }
                    else
                    {
                        throw new InvalidOperationException("Unexpected capture step");
                    }
                    break;
            }
            _shot++;
            _lastFrame = Time.frameCount;
            _next = EditorApplication.timeSinceStartup + .6;
        }
        catch (Exception exception)
        {
            File.WriteAllText(GateOutput + "/capture-error.txt",exception.ToString());
            Cleanup();

            throw;
        }
    }

    private static readonly Dictionary<ScriptableRendererFeature,bool> EffectState = new();
    private static void SetEffects(bool enabled)
    {
        _camera.GetUniversalAdditionalCameraData().renderPostProcessing = enabled;
        if (!enabled)
        {
            foreach (var feature in AssetDatabase.LoadAllAssetsAtPath("Assets/Settings/Mobile_Renderer.asset").OfType<ScriptableRendererFeature>().Where(f=>f.name=="HD2D focus band"))
            {
                EffectState[feature]=feature.isActive;feature.SetActive(false);
            }
        }
        else
        {
            foreach (var pair in EffectState) pair.Key.SetActive(pair.Value);
            EffectState.Clear();
        }


    }

    private static void Capture(string file,int width,int height)
    {
        var aspect = _camera.aspect;
        _camera.aspect = (float)width/height;
        var position = _camera.transform.position;
        CaptureCamera(Output + "/" + file,width,height);
        _camera.aspect = aspect;
        ShotList.Add(new Shot { file = file,frame = Time.frameCount,cameraPosition = _camera.transform.position,orthoSize = _camera.orthographicSize,width = width,height = height,settings = ActualSettings(),banksInside = BanksInside(width,height) });
        _camera.transform.position = position;
    }

    private static bool BanksInside(int width,int height)
    {
        var oldAspect = _camera.aspect;
        _camera.aspect = (float)width/height;
        try
        {
            var bank = Object.FindFirstObjectByType<FieldView>().Landscape.Find("bank");
            return GeometryUtility.TestPlanesAABB(GeometryUtility.CalculateFrustumPlanes(_camera),bank.GetComponent<Renderer>().bounds);
        }
        finally { _camera.aspect = oldAspect; }
    }

    private static void CaptureHeroMask(string file = "Main1-hero-mask.png")
    {
        var renderers = Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None);
        var states = renderers.Select(r => r.enabled).ToArray();
        var hero = Hero();
        var material = hero.sharedMaterial;
        var mask = new Material(Shader.Find("HD2D/HD2D_SpriteFlash"));
        var background = _camera.backgroundColor;
        try
        {
            foreach (var renderer in renderers)
            {
                renderer.enabled = renderer == hero;
            }
            hero.sharedMaterial = mask;
            _camera.backgroundColor = Color.black;
            SetEffects(false);
            Capture(file,1920,1080);
        }
        finally
        {
            hero.sharedMaterial = material;
            _camera.backgroundColor = background;
            for (var i = 0; i < renderers.Length; i++)
            {
                renderers[i].enabled = states[i];
            }
            SetEffects(true);
            Object.DestroyImmediate(mask);
        }
    }

    private static void CaptureGroundCut()
    {
        var renderers = Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None);
        var states = renderers.Select(r => r.enabled).ToArray();
        var floor = Object.FindFirstObjectByType<FieldView>().Landscape.Find("ground").GetComponent<MeshRenderer>();
        var original = floor.sharedMaterial;
        var material = new Material(original);
        material.SetTexture("_Ground",AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Game/03.Resources/Textures/HD2D/FieldGrassOnly.png"));
        try
        {
            foreach (var renderer in renderers)
            {
                renderer.enabled = renderer == floor;
            }
            floor.sharedMaterial = material;
            Capture("Main1-ground-crop.png",1920,1080);
        }
        finally
        {
            floor.sharedMaterial = original;
            for (var i = 0; i < renderers.Length; i++)
            {
                renderers[i].enabled = states[i];
            }
            Object.DestroyImmediate(material);
        }
    }

    private static void CropMain(string source,string output,int left,int top,int width,int height,int scale)
    {
        var image = new Texture2D(2,2,TextureFormat.RGBA32,false);
        image.LoadImage(File.ReadAllBytes(Output + "/" + source));
        var pixels = image.GetPixels32();
        var result = new Texture2D(width*scale,height*scale,TextureFormat.RGBA32,false);
        var enlarged = new Color32[width*height*scale*scale];
        for (var y = 0; y < height*scale; y++)
        {
            for (var x = 0; x < width*scale; x++)
            {
                enlarged[y*width*scale+x] = pixels[(image.height-top-height+y/scale)*image.width+left+x/scale];
            }
        }
        result.SetPixels32(enlarged);
        result.Apply();
        File.WriteAllBytes(Output + "/" + output,result.EncodeToPNG());
        Object.DestroyImmediate(image);
        Object.DestroyImmediate(result);
    }

    private static void CaptureLighting()
    {
        var renderers = Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None);
        var states = renderers.Select(r => r.enabled).ToArray();
        var originals = renderers.Select(r => r.sharedMaterials).ToArray();
        var clones = new List<Material>();
        try
        {
            SetEffects(false);
            _camera.GetComponent<HD2DBackgroundRig>().EdgesEnabled = false;
            for (var i = 0; i < renderers.Length; i++)
            {
                var diagnostic = renderers[i] is MeshRenderer && originals[i].All(m => m != null && m.HasProperty("_Diagnostic"));
                renderers[i].enabled = states[i] && diagnostic;
                if (!diagnostic) continue;
                renderers[i].sharedMaterials = originals[i].Select(original =>
                {
                    var material = new Material(original);
                    material.SetFloat("_Diagnostic",1);
                    clones.Add(material);
                    return material;
                }).ToArray();
            }
            Capture("Main1-lighting.png",1920,1080);
        }
        finally
        {
            for (var i = 0; i < renderers.Length; i++)
            {
                renderers[i].sharedMaterials = originals[i];
                renderers[i].enabled = states[i];
            }
            foreach (var material in clones) Object.DestroyImmediate(material);
            SetEffects(true);
            _camera.GetComponent<HD2DBackgroundRig>().EdgesEnabled = true;
        }
    }

    private static Vector3 PondFocus()
    {
        var layout = JsonUtility.FromJson<FieldLayoutData>(File.ReadAllText("Assets/Game/03.Resources/Resources/Data/Field/field_layout.json"));
        var outline = layout.water.First(w => w.name == "pond").points;
        var centre = outline.Aggregate(Vector2.zero, (sum, point) => sum + point) / outline.Length * .5f;
        return new Vector3(centre.x, 0, centre.y);
    }

    private static void CaptureBankDiagnostic()
    {
        var renderers = Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None);
        var originals = renderers.Select(r=>r.sharedMaterials).ToArray();
        var states = renderers.Select(r=>r.enabled).ToArray();
        var clones = new List<Material>();
        var cameraPosition = _camera.transform.position;
        try
        {
            // A separate material diagnostic uses the actual pond bank, even when the battle view has moved away.
            _camera.transform.position = PondFocus() - _camera.transform.forward * _camera.GetComponent<TiltedCameraRig>().Distance;
            SetEffects(false);_camera.GetComponent<HD2DBackgroundRig>().EdgesEnabled=false;
            for (var i=0;i<renderers.Length;i++)
            {
                var bank=renderers[i].name=="bank";
                renderers[i].enabled=bank;
                if (!bank) continue;
                renderers[i].sharedMaterials=originals[i].Select(m=>{var c=new Material(m);clones.Add(c);return c;}).ToArray();
            }
            Capture("Main1-bank-color.png",1920,1080);
            foreach (var material in clones) material.SetFloat("_Diagnostic",2);
            Capture("Main1-bank-mask.png",1920,1080);
        }
        finally
        {
            for (var i=0;i<renderers.Length;i++) {renderers[i].sharedMaterials=originals[i];renderers[i].enabled=states[i];}
            foreach (var material in clones) Object.DestroyImmediate(material);
            _camera.transform.position = cameraPosition;
            SetEffects(true);_camera.GetComponent<HD2DBackgroundRig>().EdgesEnabled=true;
        }
    }

    private static void WriteCollection()
    {
        var length = MeasureShadowLength();
        var collection = new Collection { context = JsonUtility.FromJson<Context>(File.ReadAllText(GateOutput + "/capture-context.json")),shots = ShotList,ledgeHeight = Object.FindFirstObjectByType<FieldView>().Landscape.Find("bank").GetComponent<MeshFilter>().sharedMesh.bounds.size.y,shadowLength = length,pondCrop = new[] { _pondCropX, _pondCropY, _pondCropX + 600, _pondCropY + 240 } };
        File.WriteAllText(GateOutput + "/collection.json",JsonUtility.ToJson(collection,true));
    }

    public static float MeasureShadowLength()
    {
        var hero = Hero();
        var direction = RenderSettings.sun.transform.forward;
        var origin = hero.transform.position;
        // Atlas clones have no source PNG path. Read exactly their packed rectangle on the GPU.
        var rect=hero.sprite.textureRect;var atlas=hero.sprite.texture;
        var target=RenderTexture.GetTemporary((int)rect.width,(int)rect.height,0,RenderTextureFormat.ARGB32);
        var active=RenderTexture.active;
        var pixels=new Texture2D(target.width,target.height,TextureFormat.RGBA32,false);
        try
        {
            Graphics.Blit(atlas,target,new Vector2(rect.width/atlas.width,rect.height/atlas.height),new Vector2(rect.x/atlas.width,rect.y/atlas.height));
            RenderTexture.active=target;pixels.ReadPixels(new Rect(0,0,target.width,target.height),0,0);pixels.Apply();
        }
        finally { RenderTexture.active=active;RenderTexture.ReleaseTemporary(target); }
        var colors = pixels.GetPixels32();
        var length = 0f;
        for (var y = 0; y < pixels.height; y++)
        {
            for (var x = 0; x < pixels.width; x++)
            {
                if (colors[y*pixels.width+x].a<128) continue;
                foreach (var offset in new[] { Vector2.zero,Vector2.right,Vector2.up,Vector2.one })
                {
                    var local = (new Vector2(x,y)+hero.sprite.textureRectOffset+offset-hero.sprite.pivot)/hero.sprite.pixelsPerUnit;
                    var point = hero.transform.TransformPoint(new Vector3(local.x*(hero.flipX?-1:1),local.y,0));
                    var projected = point-direction*(point.y/direction.y);
                    length = Mathf.Max(length,Vector2.Distance(new Vector2(projected.x,projected.z),new Vector2(origin.x,origin.z)));
                }
            }
        }
        Object.DestroyImmediate(pixels);
        return length;
    }

    public static void CaptureLightUnit(string directory, string mode)
    {
        if (!Application.isPlaying || Time.timeScale != 0)
        {
            throw new InvalidOperationException("Freeze the owned frame before paired light captures.");
        }
        _camera = Camera.main;
        var oldOutput = Output;
        Output = directory;
        Directory.CreateDirectory(directory);
        try
        {
            Capture("main.png", 1920, 1080);
            if (mode == "vignette")
            {
                var volume = Object.FindObjectsByType<Volume>(FindObjectsSortMode.None)
                    .Single(v => v.sharedProfile != null && v.sharedProfile.Has<Vignette>());
                volume.profile.TryGet<Vignette>(out var vignette);
                var intensity = vignette.intensity.value;
                try
                {
                    vignette.intensity.value = 0;
                    Capture("vignette-off.png", 1920, 1080);
                }
                finally
                {
                    vignette.intensity.value = intensity;
                    UpdateAndSelectVolumeStack();
                }
                File.WriteAllText(directory + "/settings.json", JsonUtility.ToJson(ActualSettings(), true));
            }
            if (mode == "baseline" || mode == "sun")
            {
                var sun = RenderSettings.sun;
                var wasEnabled = sun.enabled;
                try
                {
                    sun.enabled = false;
                    Capture("sun-off.png", 1920, 1080);
                }
                finally { sun.enabled = wasEnabled; }
                File.WriteAllText(directory + "/shadow.json", "{\"space\":\"world\",\"hero_height\":1,\"length\":" + MeasureShadowLength().ToString(System.Globalization.CultureInfo.InvariantCulture) + "}");
            }
            if (mode == "baseline" || mode == "toon")
            {
                CaptureLighting();
                CropMain("main.png", "closeup.png", CropX, CropY, CropWidth, CropHeight, CropScale);
            }
            if (mode == "baseline" || mode == "bloom")
            {
                var profile = Object.FindObjectsByType<Volume>(FindObjectsSortMode.None).First(v => v.sharedProfile != null && v.sharedProfile.Has<Bloom>()).profile;
                profile.TryGet<Bloom>(out var bloom);
                var intensity = bloom.intensity.value;
                try
                {
                    bloom.intensity.value = 0;
                    Capture("bloom-off.png", 1920, 1080);
                }
                finally { bloom.intensity.value = intensity; }
                File.WriteAllText(directory + "/settings.json", JsonUtility.ToJson(ActualSettings(), true));
            }
            if (mode == "baseline" || mode == "bloom")
            {
                var feature = AssetDatabase.LoadAllAssetsAtPath("Assets/Settings/Mobile_Renderer.asset")
                    .OfType<ScriptableRendererFeature>().Single(f => f.name == "HD2D focus band");
                var wasActive = feature.isActive;
                try
                {
                    feature.SetActive(false);
                    Capture("blur-off.png", 1920, 1080);
                }
                finally { feature.SetActive(wasActive); }
            }
            if (mode == "baseline" || mode == "bank")
            {
                CaptureBankDiagnostic();
            }
            File.WriteAllText(directory + "/frame.json", "{\"wave\":" + PlayableProbe.Wave + ",\"waveFrame\":" + HD2DLightCapture.WaveFrame + ",\"numbers\":" + PlayableProbe.DamageNumberCount + ",\"hpBars\":" + PlayableProbe.MonsterHpBarCount + "}");
        }
        finally { _camera.UpdateVolumeStack(); Output = oldOutput; }
    }

    // Transient view-only patches: identical point stripes in the two blur bands,
    // and an actual pooled HP bar. No scene or gameplay asset is saved.
    public static void CaptureBlurReview(string directory)
    {
        if (!Application.isPlaying || Time.timeScale != 0)
            throw new InvalidOperationException("Freeze the owned light frame first.");
        _camera = Camera.main;
        Directory.CreateDirectory(directory);
        var feature = AssetDatabase.LoadAllAssetsAtPath("Assets/Settings/Mobile_Renderer.asset")
            .OfType<ScriptableRendererFeature>().Single(f => f.name == "HD2D focus band");
        var wasActive = feature.isActive;
        var texture = new Texture2D(128, 1, TextureFormat.RGBA32, false, true);
        texture.filterMode = FilterMode.Point;
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.SetPixels(Enumerable.Range(0, 128).Select(x => (x / 4) % 2 == 0 ? new Color(.2f,.2f,.2f) : new Color(.6f,.6f,.6f)).ToArray());
        texture.Apply();
        var material = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
        material.SetTexture("_BaseMap", texture);
        material.SetFloat("_Cull", 0);
        var patches = new List<GameObject>();
        var container = Object.FindAnyObjectByType<GameSceneLifetimeScope>().Container;
        var factory = container.Resolve<MonsterHpBarViewFactory>();
        const int barId = 19061119;
        MonsterHpBarView bar = null;
        try
        {
            var pixel = 2 * _camera.orthographicSize / 1080;
            // Same normalized band depth: bottom .08/.16, top (.88-.76)/.24.
            foreach (var v in new[] { .08f, .88f })
            {
                var patch = GameObject.CreatePrimitive(PrimitiveType.Quad);
                patches.Add(patch);
                Object.DestroyImmediate(patch.GetComponent<Collider>());
                patch.name = "Transient identical blur texture";
                patch.transform.SetPositionAndRotation(_camera.ViewportToWorldPoint(new Vector3(.5f,v,5)), _camera.transform.rotation);
                patch.transform.localScale = new Vector3(128 * pixel,64 * pixel,1);
                patch.GetComponent<MeshRenderer>().sharedMaterial = material;
                patch.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
            }
            CaptureCamera(directory + "/texture-on.png", 1920, 1080);
            feature.SetActive(false);
            CaptureCamera(directory + "/texture-off.png", 1920, 1080);
            feature.SetActive(wasActive);
            foreach (var patch in patches) patch.SetActive(false);
            texture.SetPixels(Enumerable.Repeat(new Color(.4f,.4f,.4f),128).ToArray());
            texture.Apply();
            patches[0].transform.position = _camera.ViewportToWorldPoint(new Vector3(.5f,.08f,6));
            patches[0].SetActive(true);
            bar = factory.Create(barId);
            bar.UpdateView(Vector3.zero, .625f);
            bar.transform.position = _camera.ViewportToWorldPoint(new Vector3(.5f,.08f,5));
            CaptureCamera(directory + "/hp-on.png", 1920, 1080);
            feature.SetActive(false);
            CaptureCamera(directory + "/hp-off.png", 1920, 1080);
            var barRenderers = bar.GetComponentsInChildren<SpriteRenderer>();
            File.WriteAllText(directory + "/hp-renderers.txt", string.Join("\n",barRenderers.Select(r => r.name + " layer=" + r.gameObject.layer + " enabled=" + r.enabled + " sprite=" + r.sprite.name + " viewport=" + _camera.WorldToViewportPoint(r.bounds.center))));
        }
        finally
        {
            if (bar != null) factory.Release(barId);
            feature.SetActive(wasActive);
            foreach (var patch in patches) Object.DestroyImmediate(patch);
            Object.DestroyImmediate(material);
            Object.DestroyImmediate(texture);
        }
    }

    private static SpriteRenderer Hero() => Object.FindObjectsByType<MemberView>(FindObjectsSortMode.None)
        .First(v => v.BodySpriteName.StartsWith("bruno")).BodyRenderer;

    private static void UpdateAndSelectVolumeStack()
    {
        _camera.UpdateVolumeStack();
        var data = _camera.GetUniversalAdditionalCameraData();
        if (data.volumeStack != null) VolumeManager.instance.stack = data.volumeStack;
        else VolumeManager.instance.Update(data.volumeTrigger != null ? data.volumeTrigger : _camera.transform, data.volumeLayerMask);
    }

    private static void CaptureCamera(string path, int width, int height)
    {
        var oldTarget = _camera.targetTexture;
        var active = RenderTexture.active;
        var oldStack = VolumeManager.instance.stack;
        // Keep HDR precision until standard linear-to-sRGB PNG encoding; do not filter or redraw pixels.
        var target = RenderTexture.GetTemporary(width,height,24,RenderTextureFormat.ARGBHalf,RenderTextureReadWrite.sRGB);
        target.antiAliasing=1;
        var texture = new Texture2D(width,height,TextureFormat.RGBAFloat,false);
        var canvases = Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None);
        var enabled = canvases.Select(c=>c.enabled).ToArray();
        try
        {
            foreach (var canvas in canvases) canvas.enabled=false;
            _camera.targetTexture=target;
            _camera.GetComponent<HD2DBackgroundRig>().RenderBackgroundForCapture();
            // SingleCameraRequest does not run RenderCameraStack's volume selection.
            // UpdateVolumeStack fills the camera-local stack but does not select it
            // globally. Select that stack explicitly after the background request.
            UpdateAndSelectVolumeStack();
            RenderPipeline.SubmitRenderRequest(_camera,new UniversalRenderPipeline.SingleCameraRequest { destination=target });
            File.WriteAllText(path + ".settings.json", JsonUtility.ToJson(ActualSettings(), true));
            RenderTexture.active=target;
            texture.ReadPixels(new Rect(0,0,width,height),0,0);texture.Apply();
            var png=new Texture2D(width,height,TextureFormat.RGBA32,false);
            try { png.SetPixels(texture.GetPixels().Select(c=>c.gamma).ToArray());png.Apply();File.WriteAllBytes(path,png.EncodeToPNG()); }
            finally { Object.DestroyImmediate(png); }
        }
        finally
        {
            VolumeManager.instance.stack = oldStack;
            _camera.targetTexture=oldTarget;
            RenderTexture.active=active;
            for (var i=0;i<canvases.Length;i++) canvases[i].enabled=enabled[i];
            RenderTexture.ReleaseTemporary(target);Object.DestroyImmediate(texture);
        }
    }

    [Serializable] public sealed class Region { public string name; public int[] box; public int[] ground_box; }
    [Serializable] public sealed class Regions { public string source; public int[] hero; public int[][] ground; public Region[] props; public Region[] monsters; }
    private static bool IsVisibleSlime(SpriteRenderer renderer)
    {
        var p=Camera.main.WorldToViewportPoint(renderer.bounds.center);
        return renderer.sprite.name.StartsWith("slime") && p.z>0 && p.x>.01f && p.x<.99f && p.y>.05f && p.y<.95f;
    }
    public static bool CanPrepare => Object.FindObjectsByType<MonsterView>(FindObjectsSortMode.None)
        .Select(v=>(SpriteRenderer)new SerializedObject(v).FindProperty("bodyRenderer").objectReferenceValue).Any(IsVisibleSlime);

    public static void Prepare()
    {
        if (!Application.isPlaying) throw new InvalidOperationException("Main Play required");
        Time.timeScale=0;PlayableProbe.SetMove(0,0);
        _camera=Camera.main;
        _camera.aspect=1920f/1080;
        Directory.CreateDirectory(GateOutput);
        var layout = JsonUtility.FromJson<FieldLayoutData>(File.ReadAllText("Assets/Game/03.Resources/Resources/Data/Field/field_layout.json"));
        var pondPoint = layout.water.First(w => w.name == "pond").points.OrderBy(p => p.sqrMagnitude).First();
        var pondPosition = _camera.transform.position;
        try
        {
            _camera.transform.position = PondFocus() - _camera.transform.forward * _camera.GetComponent<TiltedCameraRig>().Distance;
            var pondPixel = _camera.WorldToViewportPoint(new Vector3(pondPoint.x, -.015f, pondPoint.y));
            _pondCropX = Mathf.Clamp(Mathf.RoundToInt(pondPixel.x * 1920) - 300, 0, 1320);
            _pondCropY = Mathf.Clamp(Mathf.RoundToInt((1 - pondPixel.y) * 1080) - 120, 0, 840);
        }
        finally { _camera.transform.position = pondPosition; }
        File.WriteAllText(GateOutput + "/pond-crop-before.json", "{\"box\":[" + _pondCropX + "," + _pondCropY + "," + (_pondCropX + 600) + "," + (_pondCropY + 240) + "],\"source\":\"nearest PCG pond contour in same-zoom pond view, projected before declaration\"}");
        int[] Box(Renderer renderer, bool interior)
        {
            var min=Vector2.one*float.PositiveInfinity;var max=Vector2.one*float.NegativeInfinity;
            var sprite = renderer as SpriteRenderer;
            var vertices = sprite != null
                ? sprite.sprite.vertices.Select(v => new Vector3(v.x * (sprite.flipX ? -1 : 1), v.y, 0)).ToArray()
                : renderer.GetComponent<MeshFilter>().sharedMesh.vertices;
            foreach (var vertex in vertices)
            {
                var point=_camera.WorldToViewportPoint(renderer.transform.TransformPoint(vertex));
                min=Vector2.Min(min,point);max=Vector2.Max(max,point);
            }
            var rect=Rect.MinMaxRect(min.x,min.y,max.x,max.y);
            var left=rect.xMin*1920;var top=(1-rect.yMax)*1080;
            if (interior)
            {
                var rock=renderer.GetComponentsInParent<Transform>().Any(t=>t.name.StartsWith("3D:boulder:")) || renderer.name.Contains("boulder_01");
                var xInset=rock?.2f:.2f;var topInset=rock?.25f:.15f;var bottom=rock?.85f:.55f;
                return new[] { Mathf.RoundToInt(left+rect.width*1920*xInset),Mathf.RoundToInt(top+rect.height*1080*topInset),Mathf.RoundToInt(left+rect.width*1920*(1-xInset)),Mathf.RoundToInt(top+rect.height*1080*bottom) };
            }
            return new[] { Mathf.FloorToInt(left),Mathf.FloorToInt(top),Mathf.CeilToInt(rect.xMax*1920),Mathf.CeilToInt((1-rect.yMin)*1080) };
        }
        bool Visible(Renderer renderer)
        {
            var rect = OcclusionMath.ProjectBounds(_camera, renderer.bounds);
            return _camera.WorldToViewportPoint(renderer.bounds.center).z > 0
                && rect.xMin >= 0 && rect.xMax <= 1 && rect.yMin >= 0 && rect.yMax <= 1;
        }
        var field=Object.FindFirstObjectByType<FieldView>();
        var scenery=field.Landscape.GetComponentsInChildren<Renderer>().Where(r => r.enabled).ToArray();
        bool Kind(Renderer renderer, string kind) => field.UsesModelScenery
            ? renderer.GetComponentsInParent<Transform>().Any(t => t.name.StartsWith("3D:" + kind + ":"))
            : renderer.name.StartsWith(kind + ":");
        // Select actual mesh geometry after the card-to-model migration; criteria remain unchanged.
        bool ClearSurface(Renderer renderer)
        {
            var box = Box(renderer,true);
            var sample = Rect.MinMaxRect(box[0]/1920f,1-box[3]/1080f,box[2]/1920f,1-box[1]/1080f);
            var block = new MaterialPropertyBlock();
            renderer.GetPropertyBlock(block);
            if (renderer.sharedMaterial.GetFloat("_Dither") < .001f) return true;
            var windows=block.GetVectorArray("_OcclusionRects");
            for (var index=0;index<block.GetInt("_OcclusionRectCount");index++)
            {
                var window=windows[index];
                if (sample.Overlaps(Rect.MinMaxRect(window.x,window.y,window.z,window.w))) return false;
            }
            return true;
        }
        var tree=scenery.Where(v=>Kind(v,"tree") && Visible(v) && ClearSurface(v))
            .OrderBy(v=>{var box=Box(v,true); return new Vector2((box[0]+box[2])/3840f-.5f,(box[1]+box[3])/2160f-.5f).sqrMagnitude;}).First();
        var rock=scenery.Where(v=>Kind(v,"boulder") && Visible(v))
            .First(v=>{var box=Box(v,false); return box[0]>=CropX && box[1]>=CropY && box[2]<=CropX+CropWidth && box[3]<=CropY+CropHeight;});
        var monsters=Object.FindObjectsByType<MonsterView>(FindObjectsSortMode.InstanceID);
        var monster=monsters.Select(v=>v.BodyRenderer).Where(IsVisibleSlime)
            .OrderBy(v=>{var p=_camera.WorldToViewportPoint(v.bounds.center);return new Vector2(p.x-.5f,p.y-.5f).sqrMagnitude;}).First();
        var groundBox = EmptyGrassPanel(field);
        var groundSmall = new[] { groundBox[0], groundBox[1], groundBox[0] + 80, groundBox[1] + 45 };
        var regions=new Regions { source="pre-capture: actual fully visible Main sprite or mesh vertices; clear prop surfaces nearest focus; empty grass panel excludes scenery, actors, cast shadows and authored path/water/soil masks before capture",hero=Box(Hero(),false),ground=new[] { groundBox },props=new[] { new Region { name="tree",box=Box(tree,true),ground_box=groundSmall },new Region {name="stone",box=Box(rock,true),ground_box=groundSmall} },monsters=new[]{new Region {name=monster.sprite.name,box=Box(monster,true),ground_box=groundSmall}} };
        // JsonUtility cannot serialize jagged arrays. Write the simple region declaration explicitly.
        string Array(int[] a)=>"["+string.Join(",",a)+"]";
        string Entry(Region e)=>"{\"name\":\""+e.name+"\",\"box\":"+Array(e.box)+",\"ground_box\":"+Array(e.ground_box)+"}";
        File.WriteAllText(GateOutput+"/regions-before.json","{\"source\":\""+regions.source+"\",\"hero\":"+Array(regions.hero)+",\"ground\":["+Array(regions.ground[0])+"],\"props\":["+string.Join(",",regions.props.Select(Entry))+"],\"monsters\":["+string.Join(",",regions.monsters.Select(Entry))+"]}");
        var dependencies=AssetDatabase.GetDependencies("Assets/Game/01.Scene/Main.unity",true).Where(File.Exists).ToArray();
        File.WriteAllText(GateOutput+"/dependencies.json","{\"result\":["+string.Join(",",dependencies.Select(p=>"\""+p+"\""))+"]}");
    }

    private static int[] EmptyGrassPanel(FieldView field)
    {
        var obstacles = new List<Rect>();
        Rect PixelRect(Bounds bounds)
        {
            var rect = OcclusionMath.ProjectBounds(_camera,bounds);
            return Rect.MinMaxRect(rect.xMin*1920,(1-rect.yMax)*1080,rect.xMax*1920,(1-rect.yMin)*1080);
        }
        var direction = RenderSettings.sun.transform.forward;
        foreach (var renderer in field.Landscape.GetComponentsInChildren<Renderer>(true).Where(r=>r.enabled && !r.name.StartsWith("3D plant batch ")))
        {
            if (new[] { "ground","bank","pond","stream","stream-upper","waterfall" }.Contains(renderer.name)) continue;
            var bounds=renderer.bounds;
            if (!renderer.gameObject.activeInHierarchy && renderer is MeshRenderer)
            {
                var local=renderer.GetComponent<MeshFilter>().sharedMesh.bounds;
                var first=true;
                for (var i=0;i<8;i++)
                {
                    var corner=local.center+Vector3.Scale(local.extents,new Vector3((i&1)==0?-1:1,(i&2)==0?-1:1,(i&4)==0?-1:1));
                    var point=renderer.transform.TransformPoint(corner);
                    if (first) { bounds=new Bounds(point,Vector3.zero);first=false; }
                    bounds.Encapsulate(point);
                }
            }
            obstacles.Add(PixelRect(bounds));
            if (renderer.shadowCastingMode == ShadowCastingMode.Off) continue;
            var projected = new Bounds();
            var initialized = false;
            for (var i=0;i<8;i++)
            {
                var point=bounds.center+Vector3.Scale(bounds.extents,new Vector3((i&1)==0?-1:1,(i&2)==0?-1:1,(i&4)==0?-1:1));
                point-=direction*(point.y/direction.y);
                if (!initialized) { projected=new Bounds(point,Vector3.zero);initialized=true; }
                projected.Encapsulate(point);
            }
            obstacles.Add(PixelRect(projected));
        }
        foreach (var member in Object.FindObjectsByType<MemberView>(FindObjectsSortMode.None)) obstacles.Add(PixelRect(member.BodyRenderer.bounds));
        foreach (var monster in Object.FindObjectsByType<MonsterView>(FindObjectsSortMode.None)) obstacles.Add(PixelRect(monster.BodyRenderer.bounds));
        var masks = new List<Texture2D>();
        try
        {
            foreach (var name in new[] { "Path","Water","Zone" })
            {
                var texture = new Texture2D(2,2,TextureFormat.RGBA32,false,true);
                texture.LoadImage(File.ReadAllBytes("Assets/Game/03.Resources/Textures/HD2D/Field"+name+"Mask.png"));
                masks.Add(texture);
            }
            var plane = new Plane(Vector3.up,new Vector3(0,.36f,0));
            // Same 61,440-pixel area as the previous 640x96 panel, with aspect choices
            // for the new groves; do not mistake a sparse plant batch's AABB for filled ground.
            foreach (var size in new[] { new Vector2Int(640,96),new Vector2Int(480,128),new Vector2Int(384,160),new Vector2Int(320,192) })
            for (var top=400;top<=920;top+=24)
            {
                for (var left=64;left<=1536;left+=24)
                {
                    if (left+size.x>1856 || top+size.y>1040) continue;
                    var rect = new Rect(left,top,size.x,size.y);
                    if (obstacles.Any(o=>o.Overlaps(rect))) continue;
                    var isGrass=true;
                    for (var y=top;y<=top+size.y && isGrass;y+=16)
                    {
                        for (var x=left;x<=left+size.x;x+=16)
                        {
                            var ray=_camera.ViewportPointToRay(new Vector3(x/1920f,1-y/1080f,0));
                            plane.Raycast(ray,out var distance);
                            var point=ray.GetPoint(distance);
                            var u=(point.x+36)/72;var v=(point.z+36)/72;
                            for (var k=0;k<masks.Count;k++)
                            {
                                var mask=masks[k];
                                var color=mask.GetPixel(Mathf.Clamp((int)(u*mask.width),0,mask.width-1),Mathf.Clamp((int)(v*mask.height),0,mask.height-1));
                                if (color.r>.1f || k==2 && color.g>.1f) { isGrass=false;break; }
                            }
                            if (!isGrass) break;
                        }
                    }
                    if (isGrass) return new[] {left,top,left+size.x,top+size.y};
                }
            }
            throw new InvalidOperationException("No unobstructed 61,440-pixel grass panel at this normal-movement focus.");
        }
        finally
        {
            foreach (var texture in masks) Object.DestroyImmediate(texture);
        }
    }

    private static void Cleanup()
    {
        EditorApplication.update -= OnEditorUpdate;
        if (_camera != null) _camera.transform.position = _origin;

    }
}
