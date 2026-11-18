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
public static class FieldLookdevCapture4
{
    public const string Output = "output/hd2d-시안";
    public const string GateOutput = "output/hd2d-관문/r4";
    private static int _shot;
    private static int _lastFrame;
    private static double _next;
    private static Camera _camera;
    private static Vector3 _origin;
    private static GameObject _closeup;
    private static Renderer[] _hidden;
    private static bool[] _states;
    private static RenderTexture _target;
    private static RenderTexture _previousTarget;
    private static double _start;
    private static Measurement _measurement;
    private static readonly List<Shot> ShotList = new List<Shot>();
    [Serializable] public sealed class Context { public string runId; public string planHash; }
    [Serializable] public sealed class Settings { public string quality; public float renderScale; public bool srpBatcher; public bool postProcessing; public string antialiasing; }
    [Serializable] public sealed class Shot { public string file; public int frame; public Vector3 cameraPosition; public float orthoSize; public int width; public int height; public Settings settings; public bool banksInside; }
    [Serializable] public sealed class Collection { public Context context; public List<Shot> shots; public float ledgeHeight; public float shadowLength; public Contact[] contacts; }
    [Serializable] public sealed class Contact { public string name; public float footGap; public float length; }
    [Serializable] public sealed class Sample { public int frame; public int draws; public int setPass; public int triangles; public int shadowCasters; public int srpDraws; public float frameMs; public float renderMs; }
    [Serializable] public sealed class Measurement { public string quality; public float renderScale; public bool srpBatcher; public double seconds; public int monsters; public int activeMonsters; public int activeRenderers; public int monsterRenderers; public int activeMonstersAfter; public string qualityAfter; public string source; public List<Sample> samples = new List<Sample>(); }

    [MenuItem("DeadSurvivor/HD2D/Round4/Capture And Measure")]
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
        return new Settings { quality = QualitySettings.names[QualitySettings.GetQualityLevel()],renderScale = asset.renderScale,srpBatcher = asset.useSRPBatcher && GraphicsSettings.useScriptableRenderPipelineBatching,postProcessing = data.renderPostProcessing,antialiasing = data.antialiasing.ToString() };
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
                    Capture("F4-main.png",1920,1080);
                    File.WriteAllText(GateOutput + "/settings.json",JsonUtility.ToJson(ActualSettings(),true));
                    File.WriteAllText(GateOutput + "/camera.json","{\"mode\":\"ortho\",\"angle\":" + _camera.transform.eulerAngles.x + ",\"res\":[1920,1080],\"ortho_size\":" + _camera.orthographicSize + ",\"char_px\":48}");
                    break;
                case 1:
                    CaptureHeroMask();
                    break;
                case 2:
                    SetEffects(false);
                    Capture("F4-repeat.png",1920,1080);
                    SetEffects(true);
                    CaptureGroundCut();
                    break;
                case 11:
                    _camera.transform.position = _origin;
                    Capture("F4-2520x1080.png",2520,1080);
                    break;
                case 12:
                    _camera.orthographicSize = 1536f/288;
                    Capture("F4-2048x1536.png",2048,1536);
                    _camera.orthographicSize = 5.625f;
                    break;
                case 13:
                    BeginCloseup();
                    break;
                case 14:
                    Capture("closeup-F4.png",1920,1080);
                    CaptureHeroMask("F4-close-hero-mask.png");
                    break;
                case 15:
                    CaptureLighting();
                    CaptureLedgeMask();
                    EndCloseup();
                    break;
                case 16:
                    WriteCollection();
                    BeginMeasurement();
                    break;
                default:
                    if (_shot >= 3 && _shot <= 10)
                    {
                        var index = _shot-3;
                        _camera.transform.position = _origin+Vector3.right*(index/48f);
                        Capture("F4-move-" + index + ".png",1920,1080);
                    }
                    else
                    {
                        MeasureFrame();
                        return;
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
    }

    private static void Capture(string file,int width,int height)
    {
        LookdevCaptureUtils.Capture(Output + "/" + file,width,height);
        ShotList.Add(new Shot { file = file,frame = Time.frameCount,cameraPosition = _camera.transform.position,orthoSize = _camera.orthographicSize,width = width,height = height,settings = ActualSettings(),banksInside = BanksInside(width,height) });
    }

    private static bool BanksInside(int width,int height)
    {
        var oldAspect = _camera.aspect;
        _camera.aspect = (float)width/height;
        try
        {
            return FieldLookdevScene4.Ledge.GetComponent<MeshFilter>().sharedMesh.vertices.All(v =>
            {
                var point = _camera.WorldToViewportPoint(v);
                return point.x>.005f && point.x<.995f && point.y>.005f && point.y<.995f;
            });
        }
        finally { _camera.aspect = oldAspect; }
    }

    private static void CaptureHeroMask(string file = "F4-hero-mask.png")
    {
        var renderers = Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None);
        var states = renderers.Select(r => r.enabled).ToArray();
        var hero = GameObject.Find("Member_bruno").GetComponent<SpriteRenderer>();
        var material = hero.sharedMaterial;
        var mask = new Material(Shader.Find("HD2D/Round4/HeroMask"));
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
        material.SetTexture("_Ground",FieldLookdevAssets4.Load<Texture2D>("FieldGrassOnly.png"));
        try
        {
            foreach (var renderer in renderers)
            {
                renderer.enabled = renderer == floor;
            }
            floor.sharedMaterial = material;
            Capture("F4-ground-crop.png",1920,1080);
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

    private static void BeginCloseup()
    {
        _camera.transform.position = _origin;
        _camera.orthographicSize = 1.40625f;
        _hidden = Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None);
        _states = _hidden.Select(r => r.enabled).ToArray();
        var hero = GameObject.Find("Member_bruno").transform;
        foreach (var renderer in _hidden)
        {
            renderer.enabled = renderer.name.Contains("texels per unit grass") || renderer.transform.IsChildOf(hero);
        }
        _closeup = FieldLookdevScene4.Closeup();
    }

    private static void EndCloseup()
    {
        if (_hidden == null)
        {
            return;
        }
        for (var i = 0; i < _hidden.Length; i++)
        {
            _hidden[i].enabled = _states[i];
        }
        _hidden = null;
        Object.DestroyImmediate(_closeup);
        _camera.orthographicSize = 5.625f;
    }

    private static void CaptureLighting()
    {
        var renderers = _closeup.GetComponentsInChildren<MeshRenderer>();
        var originals = renderers.Select(r => r.sharedMaterial).ToArray();
        var clones = new List<Material>();
        try
        {
            SetEffects(false);
            foreach (var renderer in renderers.Where(r => r.sharedMaterial.HasProperty("_Diagnostic")))
            {
                var material = new Material(renderer.sharedMaterial);
                material.SetFloat("_Diagnostic",1);
                renderer.sharedMaterial = material;
                clones.Add(material);
            }
            Capture("F4-lighting.png",1920,1080);
        }
        finally
        {
            for (var i = 0; i < renderers.Length; i++)
            {
                renderers[i].sharedMaterial = originals[i];
            }
            foreach (var material in clones)
            {
                Object.DestroyImmediate(material);
            }
            SetEffects(true);
        }
    }

    private static void CaptureLedgeMask()
    {
        var renderers = Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None);
        var states = renderers.Select(r => r.enabled).ToArray();
        var ledge = _closeup.transform.Find("Close grass lip").GetComponent<MeshRenderer>();
        var original = ledge.sharedMaterial;
        var diagnostic = new Material(original);
        diagnostic.SetFloat("_Diagnostic",2);
        var background = _camera.backgroundColor;
        try
        {
            foreach (var renderer in renderers)
            {
                renderer.enabled = renderer == ledge;
            }
            ledge.sharedMaterial = diagnostic;
            _camera.backgroundColor = Color.black;
            SetEffects(false);
            Capture("F4-ledge-mask.png",1920,1080);
        }
        finally
        {
            ledge.sharedMaterial = original;
            _camera.backgroundColor = background;
            for (var i = 0; i < renderers.Length; i++)
            {
                renderers[i].enabled = states[i];
            }
            SetEffects(true);
            Object.DestroyImmediate(diagnostic);
        }
    }

    private static void WriteCollection()
    {
        var hero = GameObject.Find("Member_bruno").GetComponent<SpriteRenderer>();
        var direction = RenderSettings.sun.transform.forward;
        var origin = hero.transform.position;
        var length = hero.sprite.vertices.Select(v => hero.transform.TransformPoint(new Vector3(v.x,v.y,0))).Select(v => v-direction*(v.y/direction.y)).Max(v => Vector2.Distance(new Vector2(v.x,v.z),new Vector2(origin.x,origin.z)));
        var collection = new Collection { context = JsonUtility.FromJson<Context>(File.ReadAllText(GateOutput + "/capture-context.json")),shots = ShotList,contacts = Object.FindObjectsByType<PixelProjectedShadow>(FindObjectsSortMode.None).Select(c => new Contact { name = c.name,footGap = c.FootGap,length = c.Length }).ToArray(),ledgeHeight = FieldLookdevScene4.Ledge.GetComponent<MeshFilter>().sharedMesh.bounds.size.y,shadowLength = length };
        File.WriteAllText(GateOutput + "/collection.json",JsonUtility.ToJson(collection,true));
    }

    private static void BeginMeasurement()
    {
        var parent = GameObject.Find("Monsters").transform;
        var originals = parent.GetComponentsInChildren<SpriteRenderer>().Where(r => r.name.StartsWith("Monster_")).ToArray();
        for (var i = originals.Length; i < 300; i++)
        {
            var copy = Object.Instantiate(originals[i%originals.Length].gameObject,parent);
            copy.name = "Monster_load_" + i;
        }
        var monsters = parent.GetComponentsInChildren<SpriteRenderer>().Where(r => r.name.StartsWith("Monster_")).ToArray();
        for (var i = 0; i < monsters.Length; i++)
        {
            monsters[i].transform.position = new Vector3((i%25-12)*.44f,0,(i/25-5.5f)*.62f);
            monsters[i].GetComponent<PixelShadowCaster>().SetCastingShadow(false);
            FieldLookdevScene4.SnapSprite(monsters[i]);
        }
        var settings = ActualSettings();
        _measurement = new Measurement { quality = settings.quality,renderScale = settings.renderScale,srpBatcher = settings.srpBatcher,monsters = monsters.Length,activeMonsters = monsters.Count(r => r.enabled && r.gameObject.activeInHierarchy),monsterRenderers = monsters.Count(r => r.enabled && r.gameObject.activeInHierarchy),activeRenderers = Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None).Count(r => r.enabled && r.gameObject.activeInHierarchy),source = "Editor UnityStats at 1920x1080; blob planes, no real monster shadows; not device GPU timing" };
        _previousTarget = _camera.targetTexture;
        _target = new RenderTexture(1920,1080,24);
        _camera.targetTexture = _target;
        _start = EditorApplication.timeSinceStartup+2;
    }

    private static void MeasureFrame()
    {
        ActualSettings();
        if (EditorApplication.timeSinceStartup < _start)
        {
            return;
        }
        if (_measurement.samples.Count == 0)
        {
            _measurement.activeRenderers = Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None).Count(r => r.enabled && r.gameObject.activeInHierarchy);
        }
        _measurement.samples.Add(new Sample { frame = Time.frameCount,draws = UnityStats.drawCalls,setPass = UnityStats.setPassCalls,triangles = UnityStats.triangles,shadowCasters = UnityStats.shadowCasters,srpDraws = UnityStats.srpBatcherDrawCalls,frameMs = UnityStats.frameTime*1000,renderMs = UnityStats.renderTime*1000 });
        _measurement.seconds = EditorApplication.timeSinceStartup-_start;
        _lastFrame = Time.frameCount;
        if (_measurement.seconds < 20)
        {
            return;
        }
        _measurement.activeMonstersAfter = GameObject.Find("Monsters").GetComponentsInChildren<SpriteRenderer>().Count(r => r.name.StartsWith("Monster_") && r.enabled && r.gameObject.activeInHierarchy);
        _measurement.qualityAfter = ActualSettings().quality;
        File.WriteAllText(Output + "/measure4-F4.json",JsonUtility.ToJson(_measurement,true));

        File.WriteAllText(GateOutput + "/capture-complete.txt",DateTime.UtcNow.ToString("O"));
        Cleanup();
    }

    private static void Cleanup()
    {
        EditorApplication.update -= OnEditorUpdate;
        EndCloseup();
        if (_target != null)
        {
            _camera.targetTexture = _previousTarget;
            _target.Release();
            Object.DestroyImmediate(_target);
            _target = null;
        }
    }
}
