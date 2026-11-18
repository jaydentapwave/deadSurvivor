using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static class FieldLookdevCapture8
{
    public const string Output = "output/hd2d-시안";
    public const string GateOutput = "output/hd2d-관문/r8";
    private static int _shot;
    private static int _lastFrame;
    private static double _next;
    private static Camera _camera;
    private static Vector3 _origin;
    public const int CropX = 420, CropY = 96, CropWidth = 1224, CropHeight = 708, CropScale = 4;
    private static readonly List<Shot> ShotList = new List<Shot>();
    [Serializable] public sealed class Context { public string runId; public string planHash; }
    [Serializable] public sealed class Settings { public string quality; public float renderScale; public bool srpBatcher; public bool postProcessing; public string antialiasing; public int backgroundWidth; public int backgroundHeight; public int integerScale; public string backgroundFilter; public string shadows; public int outlineTexels; }
    [Serializable] public sealed class Shot { public string file; public int frame; public Vector3 cameraPosition; public float orthoSize; public int width; public int height; public Settings settings; public bool banksInside; }
    [Serializable] public sealed class Collection { public Context context; public List<Shot> shots; public float ledgeHeight; public float shadowLength; }

    [MenuItem("DeadSurvivor/HD2D/Round8/Capture All")]
    public static void Run()
    {
        if (!Application.isPlaying || !File.Exists(GateOutput + "/capture-context.json"))
        {
            throw new InvalidOperationException("Prepare a pre-capture declaration and enter owned Lookdev Play first.");
        }
        _camera = Camera.main;
        _origin = _camera.transform.position;
        _shot = 0;
        _lastFrame = Time.frameCount;
        _next = EditorApplication.timeSinceStartup + 1;
        ShotList.Clear();
        EditorApplication.update += OnEditorUpdate;
    }

    public static Settings ActualSettings()
    {
        PixelLookdevSceneUtils.Scope.RequireMobile();
        var asset = (UniversalRenderPipelineAsset)QualitySettings.renderPipeline;
        var data = _camera.GetUniversalAdditionalCameraData();
        return new Settings { quality = QualitySettings.names[QualitySettings.GetQualityLevel()],renderScale = asset.renderScale,srpBatcher = asset.useSRPBatcher && GraphicsSettings.useScriptableRenderPipelineBatching,postProcessing = data.renderPostProcessing,antialiasing = data.antialiasing.ToString(),backgroundWidth = FieldLookdevScene8.Rig.LowWidth,backgroundHeight = FieldLookdevScene8.Rig.LowHeight,integerScale = FieldLookdevScene8.Rig.Scale,backgroundFilter = FieldLookdevScene8.Rig.Color.rt.filterMode.ToString(),shadows = RenderSettings.sun.shadows.ToString(),outlineTexels = 1 };
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
            _lastFrame = Time.frameCount;
            switch (_shot)
            {
                case 0:
                    Capture("F8-main.png",1920,1080);
                    File.WriteAllText(GateOutput + "/settings.json",JsonUtility.ToJson(ActualSettings(),true));
                    File.WriteAllText(GateOutput + "/camera.json","{\"mode\":\"ortho\",\"angle\":" + _camera.transform.eulerAngles.x + ",\"res\":[1920,1080],\"ortho_size\":" + _camera.orthographicSize + ",\"char_px\":48}");
                    break;
                case 1:
                    CaptureHeroMask();
                    break;
                case 2:
                    SetEffects(false);
                    Capture("F8-repeat.png",1920,1080);
                    SetEffects(true);
                    CaptureGroundCut();
                    break;
                case 11:
                    _camera.transform.position = _origin;
                    Capture("F8-2520x1080.png",2520,1080);
                    break;
                case 12:
                    _camera.orthographicSize = 1536f/288;
                    Capture("F8-2048x1536.png",2048,1536);
                    _camera.orthographicSize = 5.625f;
                    break;
                case 13:
                    CropMain("F8-main.png","closeup-F8.png",CropX,CropY,CropWidth,CropHeight,CropScale);
                    CropMain("F8-hero-mask.png","F8-close-hero-mask.png",CropX,CropY,CropWidth,CropHeight,CropScale);
                    CropMain("F8-main.png","F8-pond-corner.png",300,96,600,240,4);
                    break;
                case 14:
                    _camera.transform.position = _origin;
                    CaptureLighting();
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
                        Capture("F8-move-" + index + ".png",1920,1080);
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
            PixelLookdevSceneUtils.Scope.Restore();
            throw;
        }
    }

    private static void SetEffects(bool enabled)
    {
        _camera.GetUniversalAdditionalCameraData().renderPostProcessing = enabled;
        PixelLookdevSceneUtils.Scope.SetScreenEffects(enabled);
        FieldLookdevScene8.Rig.KeepCompositeEnabled();
    }

    private static void Capture(string file,int width,int height)
    {
        var aspect = _camera.aspect;
        _camera.aspect = (float)width/height;
        var position = _camera.transform.position;
        FieldLookdevScene8.SnapCamera(_camera,width,height);
        FieldLookdevScene8.Rig.RenderBackground(width,height);
        LookdevCaptureUtils.Capture(Output + "/" + file,width,height);
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
            return FieldLookdevScene8.Ledge.GetComponent<MeshFilter>().sharedMesh.vertices.All(v =>
            {
                var point = _camera.WorldToViewportPoint(v);
                return point.x>.005f && point.x<.995f && point.y>.005f && point.y<.995f;
            });
        }
        finally { _camera.aspect = oldAspect; }
    }

    private static void CaptureHeroMask(string file = "F8-hero-mask.png")
    {
        var renderers = Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None);
        var states = renderers.Select(r => r.enabled).ToArray();
        var hero = GameObject.Find("Member_bruno").GetComponent<SpriteRenderer>();
        var material = hero.sharedMaterial;
        var mask = new Material(Shader.Find("HD2D/Round8/HeroMask"));
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
        var floor = GameObject.Find("48 texels per unit grass").GetComponent<MeshRenderer>();
        var original = floor.sharedMaterial;
        var material = new Material(original);
        material.SetTexture("_Ground",FieldLookdevAssets8.Load<Texture2D>("FieldGrassOnly.png"));
        try
        {
            foreach (var renderer in renderers)
            {
                renderer.enabled = renderer == floor;
            }
            floor.sharedMaterial = material;
            Capture("F8-ground-crop.png",1920,1080);
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
            FieldLookdevScene8.Rig.EdgesEnabled = false;
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
            Capture("F8-lighting.png",1920,1080);
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
            FieldLookdevScene8.Rig.EdgesEnabled = true;
        }
    }

    private static void WriteCollection()
    {
        var hero = GameObject.Find("Member_bruno").GetComponent<SpriteRenderer>();
        var direction = RenderSettings.sun.transform.forward;
        var origin = hero.transform.position;
        var pixels = new Texture2D(2,2,TextureFormat.RGBA32,false);
        pixels.LoadImage(File.ReadAllBytes(AssetDatabase.GetAssetPath(hero.sprite.texture)));
        var colors = pixels.GetPixels32();
        var length = 0f;
        for (var y = 0; y < pixels.height; y++)
        {
            for (var x = 0; x < pixels.width; x++)
            {
                if (colors[y*pixels.width+x].a<128) continue;
                foreach (var offset in new[] { Vector2.zero,Vector2.right,Vector2.up,Vector2.one })
                {
                    var local = (new Vector2(x,y)+offset-hero.sprite.pivot)/hero.sprite.pixelsPerUnit;
                    var point = hero.transform.TransformPoint(new Vector3(local.x,local.y,0));
                    var projected = point-direction*(point.y/direction.y);
                    length = Mathf.Max(length,Vector2.Distance(new Vector2(projected.x,projected.z),new Vector2(origin.x,origin.z)));
                }
            }
        }
        Object.DestroyImmediate(pixels);
        var collection = new Collection { context = JsonUtility.FromJson<Context>(File.ReadAllText(GateOutput + "/capture-context.json")),shots = ShotList,ledgeHeight = FieldLookdevScene8.Ledge.GetComponent<MeshFilter>().sharedMesh.bounds.size.y,shadowLength = length };
        File.WriteAllText(GateOutput + "/collection.json",JsonUtility.ToJson(collection,true));
    }

    private static void Cleanup()
    {
        EditorApplication.update -= OnEditorUpdate;

    }
}
