using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static class PixelLookdevCaptureUtils
{
    public const string Output = "output/hd2d-시안";
    private static readonly string[] PlateNames = { "F2a", "F2b", "F2c", "F-int" };
    private static Collection _collection;
    private static int _plate;
    private static int _shot;
    private static int _lastFrame;
    private static double _next;
    private static Vector3 _origin;
    private static Camera _camera;
    private static Renderer[] _closeupHidden;
    private static bool[] _closeupStates;
    private static GameObject _closeup;
    private static bool _isMeasuring;
    private static double _measurementStart;
    private static int _measurementFrame;
    private static RenderTexture _measurementTarget;
    private static RenderTexture _previousTarget;
    private static Measurement _measurement;

    [Serializable]
    public sealed class Source { public string path; public string sha256; }
    [Serializable]
    public sealed class Settings
    {
        public int qualityIndex;
        public string quality;
        public string pipeline;
        public string renderer;
        public float renderScale;
        public bool assetSrpBatcher;
        public bool activeSrpBatcher;
        public int msaa;
        public string antialiasing;
        public bool postProcessing;
    }
    [Serializable]
    public sealed class Shot
    {
        public string path;
        public string sha256;
        public string runId;
        public string sourceFingerprint;
        public string utc;
        public int frame;
        public int width;
        public int height;
        public float orthographicSize;
        public Vector3 cameraPosition;
        public Settings settings;
    }
    [Serializable]
    public sealed class Resolution
    {
        public int screenHeight;
        public int integerScale;
        public int characterPixels;
        public float percent;
        public float orthographicSize;
    }
    [Serializable]
    public sealed class Plate
    {
        public string id;
        public float pitch = 30f;
        public float targetPercent;
        public float characterPixels;
        public float expectedTexelPixels;
        public float closeupTexelPixels;
        public List<Shot> shots = new List<Shot>();
    }
    [Serializable]
    public sealed class Collection
    {
        public string runId;
        public string status;
        public string baseCommit = "aad2461";
        public string sourceFingerprint;
        public string finalSourceFingerprint;
        public List<Source> sources;
        public List<Plate> plates = new List<Plate>();
        public List<Resolution> integerResolutions = new List<Resolution>();
        public string measurementNotes = "Same fixed F composition. F-snap omitted by user. Old F is the fifth comparison, not a newly captured plate. Motion camera offsets are recorded; no free registration.";
    }
    [Serializable]
    public sealed class Sample
    {
        public int frame;
        public int qualityIndex;
        public int draws;
        public int srpDraws;
        public int standardDraws;
        public int dynamicBatchDraws;
        public int setPass;
        public int triangles;
        public int shadowCasters;
        public float frameMs;
        public float renderMs;
        public float deltaMs;
    }
    [Serializable]
    public sealed class Measurement
    {
        public string runId;
        public string sourceFingerprint;
        public string utc;
        public string status;
        public Settings settings;
        public int width;
        public int height;
        public int monsters;
        public int distinctSpriteTextures;
        public int distinctSpriteMaterials;
        public double seconds;
        public string source = "Editor UnityStats, one sample per advancing frame. Not device GPU timing. Sprite shadows off, blob planes remain.";
        public List<Sample> samples = new List<Sample>();
    }

    static PixelLookdevCaptureUtils()
    {
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
    }

    [MenuItem("DeadSurvivor/HD2D/Round2/Capture All")]
    public static void CaptureAll()
    {
        PixelLookdevSceneUtils.Scope.RequireMobile();
        Directory.CreateDirectory(Output);
        var sources = ReadSources();
        _collection = new Collection
        {
            runId = "20261118-hd2d-lookdev-attempt3-" + DateTime.UtcNow.ToString("yyyyMMddTHHmmss"),
            status = "collecting", sources = sources, sourceFingerprint = Fingerprint(sources)
        };
        foreach (var height in new[] { 1080,1170,1440,1536 })
        {
            var scale = Mathf.RoundToInt(height * 0.1f / 48f);
            _collection.integerResolutions.Add(new Resolution { screenHeight = height, integerScale = scale, characterPixels = scale * 48, percent = scale * 48f / height * 100f, orthographicSize = height / (2f * scale * 48f) });
        }
        _camera = Camera.main;
        _plate = 0;
        _shot = 0;
        _lastFrame = Time.frameCount;
        PreparePlate();
        _next = EditorApplication.timeSinceStartup + 0.6;
        EditorApplication.update += OnCaptureUpdate;
        WriteCollection();
    }

    [MenuItem("DeadSurvivor/HD2D/Round2/Measure 300")]
    public static void Measure300()
    {
        PixelLookdevSceneUtils.Scope.RequireMobile();
        PixelLookdevSceneUtils.Configure("F2c");
        _camera = Camera.main;
        var parent = GameObject.Find("Monsters").transform;
        var originals = parent.GetComponentsInChildren<SpriteRenderer>().Where(r => r.name.StartsWith("Monster_")).ToArray();
        for (var i = originals.Length; i < 300; i++)
        {
            var copy = Object.Instantiate(originals[i % originals.Length].gameObject,parent);
            copy.name = "Monster_load_" + i;
        }
        var monsters = parent.GetComponentsInChildren<SpriteRenderer>().Where(r => r.name.StartsWith("Monster_")).ToArray();
        for (var i = 0; i < monsters.Length; i++)
        {
            monsters[i].transform.position = new Vector3((i % 25 - 12) * 0.44f,0,(i / 25 - 5.5f) * 0.62f);
            monsters[i].GetComponent<PixelShadowCaster>().SetCastingShadow(false);
        }
        _previousTarget = _camera.targetTexture;
        _measurementTarget = new RenderTexture(1920,1080,24) { name = "F2c 300 Mobile target" };
        _camera.targetTexture = _measurementTarget;
        _measurement = new Measurement
        {
            runId = _collection.runId, sourceFingerprint = _collection.sourceFingerprint, status = "collecting",
            utc = DateTime.UtcNow.ToString("O"), settings = ReadSettings(), width = _camera.pixelWidth, height = _camera.pixelHeight,
            monsters = monsters.Length, distinctSpriteTextures = monsters.Select(m => m.sprite.texture).Distinct().Count(), distinctSpriteMaterials = monsters.Select(m => m.sharedMaterial).Distinct().Count()
        };
        _measurementStart = EditorApplication.timeSinceStartup + 2f;
        _measurementFrame = Time.frameCount;
        _isMeasuring = true;
        EditorApplication.update += OnMeasurementUpdate;
    }

    public static Settings ReadSettings()
    {
        var pipeline = (UniversalRenderPipelineAsset)QualitySettings.renderPipeline;
        var data = Camera.main.GetUniversalAdditionalCameraData();
        return new Settings { qualityIndex = QualitySettings.GetQualityLevel(), quality = QualitySettings.names[QualitySettings.GetQualityLevel()], pipeline = pipeline.name, renderer = data.scriptableRenderer.GetType().FullName, renderScale = pipeline.renderScale, assetSrpBatcher = pipeline.useSRPBatcher, activeSrpBatcher = GraphicsSettings.useScriptableRenderPipelineBatching, msaa = pipeline.msaaSampleCount, antialiasing = data.antialiasing.ToString(), postProcessing = data.renderPostProcessing };
    }

    private static void PreparePlate()
    {
        PixelLookdevSceneUtils.Configure(PlateNames[_plate]);
        _origin = _camera.transform.position;
        var scale = _plate == 3 ? 2f : 2.25f;
        _collection.plates.Add(new Plate { id = PlateNames[_plate], targetPercent = _plate == 3 ? 100f * 96 / 1080 : 10f, characterPixels = scale * 48, expectedTexelPixels = scale, closeupTexelPixels = scale * 4 });
        _next = EditorApplication.timeSinceStartup + 0.6;
        _lastFrame = Time.frameCount;
    }

    private static void OnCaptureUpdate()
    {
        if (!Application.isPlaying)
        {
            EditorApplication.update -= OnCaptureUpdate;
            return;
        }
        if (EditorApplication.timeSinceStartup < _next || Time.frameCount == _lastFrame)
        {
            return;
        }
        try
        {
            PixelLookdevSceneUtils.Scope.RequireMobile();
            var plate = _collection.plates[_plate];
            var name = plate.id;
            if (_shot == 0)
            {
                Capture(name + "-main.png",1920,1080,plate);
                PixelLookdevSceneUtils.Pollen.Pause();
                foreach (var animation in Object.FindObjectsByType<PixelWalkPreview>(FindObjectsSortMode.None))
                {
                    animation.enabled = false;
                }
                CaptureMask(name + "-hero-mask.png",true,plate);
                if (name == "F2c" || name == "F-int")
                {
                    CaptureMask(name + "-foreground-mask.png",false,plate);
                }
            }
            else if (_shot < 9)
            {
                var requested = (_shot - 1) / 144f;
                var pixel = 2f * _camera.orthographicSize / 1080f;
                var move = Mathf.Round(requested / pixel) * pixel;
                _camera.transform.position = _origin + Vector3.right * move;
                Capture(name + "-move-" + (_shot - 1) + ".png",1920,1080,plate);
            }
            else if (_shot < 11)
            {
                _camera.transform.position = _origin;
                var height = _shot == 9 ? 1080 : 1536;
                var width = _shot == 9 ? 2520 : 2048;
                if (name == "F-int")
                {
                    _camera.orthographicSize = _collection.integerResolutions.Single(r => r.screenHeight == height).orthographicSize;
                }
                Capture(name + "-" + width + "x" + height + ".png",width,height,plate);
                _camera.orthographicSize = name == "F-int" ? 5.625f : 5f;
            }
            else if (_shot == 11)
            {
                SetupCloseup(name);
            }
            else
            {
                Capture("closeup-" + name + ".png",1920,1080,plate);
                CleanupCloseup();
                _plate++;
                _shot = -1;
                if (_plate == PlateNames.Length)
                {
                    _collection.finalSourceFingerprint = Fingerprint(ReadSources());
                    _collection.status = _collection.sourceFingerprint == _collection.finalSourceFingerprint ? "complete" : "source-changed";
                    WriteCollection();
                    EditorApplication.update -= OnCaptureUpdate;
                    PixelLookdevSceneUtils.Configure("F2c");
                    return;
                }
                PreparePlate();
            }
            _shot++;
            _next = EditorApplication.timeSinceStartup + 0.15;
            _lastFrame = Time.frameCount;
            WriteCollection();
        }
        catch (Exception exception)
        {
            CleanupCloseup();
            _collection.status = "failed";
            WriteCollection();
            File.WriteAllText(Output + "/capture3-error.txt",exception.ToString());
            EditorApplication.update -= OnCaptureUpdate;
            PixelLookdevSceneUtils.Scope.Restore();
            throw;
        }
    }

    private static void Capture(string file, int width, int height, Plate plate)
    {
        var path = Output + "/" + file;
        LookdevCaptureUtils.Capture(path,width,height);
        plate.shots.Add(new Shot { path = path, sha256 = Hash(File.ReadAllBytes(path)), runId = _collection.runId, sourceFingerprint = _collection.sourceFingerprint, utc = DateTime.UtcNow.ToString("O"), frame = Time.frameCount, width = width, height = height, orthographicSize = _camera.orthographicSize, cameraPosition = _camera.transform.position, settings = ReadSettings() });
    }

    private static void CaptureMask(string file, bool isHeroOnly, Plate plate)
    {
        var renderers = Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None);
        var enabled = renderers.Select(r => r.enabled).ToArray();
        var color = _camera.backgroundColor;
        var data = _camera.GetUniversalAdditionalCameraData();
        try
        {
            foreach (var renderer in renderers)
            {
                var body = renderer as SpriteRenderer;
                renderer.enabled = body != null && (isHeroOnly ? body.name == "Member_bruno" : body.name.StartsWith("Member_") || body.name.StartsWith("Monster_") || body.name.Contains("arc") || body.name.Contains("glow"));
            }
            _camera.backgroundColor = Color.black;
            data.renderPostProcessing = false;
            PixelLookdevSceneUtils.Scope.SetScreenEffects(false);
            Capture(file,1920,1080,plate);
        }
        finally
        {
            for (var i = 0; i < renderers.Length; i++)
            {
                renderers[i].enabled = enabled[i];
            }
            _camera.backgroundColor = color;
            data.renderPostProcessing = true;
            PixelLookdevSceneUtils.Scope.SetScreenEffects(true);
        }
    }

    private static void SetupCloseup(string name)
    {
        _closeupHidden = Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None);
        _closeupStates = _closeupHidden.Select(r => r.enabled).ToArray();
        var hero = GameObject.Find("Member_bruno").transform;
        foreach (var renderer in _closeupHidden)
        {
            renderer.enabled = renderer.name.Contains("texels per unit grass") || renderer.transform.IsChildOf(hero);
        }
        _camera.transform.position = _origin;
        _camera.orthographicSize = name == "F-int" ? 1.40625f : 1.25f;
        _closeup = PixelLookdevSceneUtils.CloseupProps(name);
    }

    private static void CleanupCloseup()
    {
        if (_closeupHidden == null)
        {
            return;
        }
        for (var i = 0; i < _closeupHidden.Length; i++)
        {
            _closeupHidden[i].enabled = _closeupStates[i];
        }
        _closeupHidden = null;
        Object.DestroyImmediate(_closeup);
    }

    private static void OnMeasurementUpdate()
    {
        if (!_isMeasuring || !Application.isPlaying)
        {
            StopMeasurement();
            return;
        }
        if (Time.frameCount == _measurementFrame || EditorApplication.timeSinceStartup < _measurementStart)
        {
            return;
        }
        try
        {
            PixelLookdevSceneUtils.Scope.RequireMobile();
            _measurementFrame = Time.frameCount;
            _measurement.samples.Add(new Sample { frame = Time.frameCount, qualityIndex = QualitySettings.GetQualityLevel(), draws = UnityStats.drawCalls, srpDraws = UnityStats.srpBatcherDrawCalls, standardDraws = UnityStats.standardDrawCalls, dynamicBatchDraws = UnityStats.dynamicBatchedDrawCalls, setPass = UnityStats.setPassCalls, triangles = UnityStats.triangles, shadowCasters = UnityStats.shadowCasters, frameMs = UnityStats.frameTime * 1000, renderMs = UnityStats.renderTime * 1000, deltaMs = Time.unscaledDeltaTime * 1000 });
            _measurement.seconds = EditorApplication.timeSinceStartup - _measurementStart;
            if (_measurement.seconds < 20)
            {
                return;
            }
            _measurement.status = "complete";
            File.WriteAllText(Output + "/measure2-mobile-300.json",JsonUtility.ToJson(_measurement,true));
            LookdevCaptureUtils.Capture(Output + "/F2c-load-300.png",1920,1080);
            StopMeasurement();
        }
        catch (Exception exception)
        {
            _measurement.status = "failed";
            File.WriteAllText(Output + "/measure2-mobile-300.json",JsonUtility.ToJson(_measurement,true));
            File.WriteAllText(Output + "/measurement3-error.txt",exception.ToString());
            StopMeasurement();
            PixelLookdevSceneUtils.Scope.Restore();
            throw;
        }
    }

    private static void StopMeasurement()
    {
        EditorApplication.update -= OnMeasurementUpdate;
        _isMeasuring = false;
        if (_measurementTarget != null)
        {
            _camera.targetTexture = _previousTarget;
            _measurementTarget.Release();
            Object.DestroyImmediate(_measurementTarget);
            _measurementTarget = null;
        }
    }

    private static List<Source> ReadSources()
    {
        var files = new HashSet<string>(AssetDatabase.GetDependencies(LookdevSceneBuilder.ScenePath,true));
        foreach (var folder in new[] { "Assets/Game/02.Scripts/Editor/HD2D", "Assets/Game/02.Scripts/Effect/HD2D", "Assets/Game/03.Resources/Art/HD2D" })
        {
            foreach (var path in Directory.GetFiles(folder,"*",SearchOption.AllDirectories))
            {
                files.Add(path.Replace('\\','/'));
            }
        }
        return files.Where(File.Exists).OrderBy(p => p,StringComparer.Ordinal).Select(p => new Source { path = p, sha256 = Hash(File.ReadAllBytes(p)) }).ToList();
    }

    private static string Fingerprint(List<Source> sources)
    {
        return Hash(Encoding.UTF8.GetBytes(string.Join("\n",sources.Select(s => s.path + ":" + s.sha256))));
    }

    private static string Hash(byte[] data)
    {
        using (var sha = SHA256.Create())
        {
            return BitConverter.ToString(sha.ComputeHash(data)).Replace("-","").ToLowerInvariant();
        }
    }

    private static void WriteCollection()
    {
        File.WriteAllText(Output + "/plates2.json",JsonUtility.ToJson(_collection,true));
    }

    private static void OnPlayModeStateChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.ExitingPlayMode)
        {
            EditorApplication.update -= OnCaptureUpdate;
            StopMeasurement();
            CleanupCloseup();
        }
    }
}
