using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

// Editor-only production tool: capture and measure only the isolated lookdev scene.
[InitializeOnLoad]
public static class LookdevCaptureUtils
{
    public const string Output = "output/hd2d-시안";
    private static Camera _camera;
    private static int _plate;
    private static int _shot;
    private static int _lastFrame;
    private static double _nextCapture;
    private static Vector3 _cameraOrigin;
    private static readonly List<Plate> PlateList = new List<Plate>();
    private static readonly Dictionary<Renderer, Material[]> OriginalMaterialDict = new Dictionary<Renderer, Material[]>();
    private static readonly Dictionary<Renderer, Material[]> BaselineMaterialDict = new Dictionary<Renderer, Material[]>();
    private static List<Sample> _sampleList;
    private static int _measurement;
    private static double _measurementStart;
    private static int _measurementFrame;
    private static bool _isMeasuring;
    private static bool _isCapturing;
    private static RenderTexture _measurementTarget;
    private static RenderTexture _originalTarget;

    [Serializable]
    public sealed class Plate
    {
        public string id;
        public bool orthographic;
        public float angle;
        public float fov;
        public float orthographicSize;
        public float distance;
        public float measuredHeightPixels;
        public float measuredHeightPercent;
        public int renderedHeightPixels;
        public float topBottomPixelRatio;
        public string groundCorners;
        public int firstFrame;
        public int lastFrame;
    }
    [Serializable]
    private sealed class PlateCollection { public List<Plate> plates; }
    [Serializable]
    private sealed class Sample
    {
        public int frame;
        public int draws;
        public int setPass;
        public int triangles;
        public int shadowCasters;
        public float frameMs;
        public float renderMs;
        public float deltaMs;
    }
    [Serializable]
    private sealed class Measurement
    {
        public string utc;
        public string source;
        public bool realMonsterShadows;
        public int monsters;
        public int width;
        public int height;
        public double seconds;
        public string quality;
        public float renderScale;
        public bool srpBatcher;
        public List<Sample> samples;
    }

    static LookdevCaptureUtils()
    {
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
    }

    [MenuItem("DeadSurvivor/HD2D/Capture All")]
    public static void CaptureAll()
    {
        RequireLookdevPlay();
        Directory.CreateDirectory(Output);
        _camera = Camera.main;
        _plate = 0;
        _shot = 0;
        PlateList.Clear();
        CacheMaterials();
        _isCapturing = true;
        ConfigurePlate(0);
        _nextCapture = EditorApplication.timeSinceStartup + 1f;
        _lastFrame = Time.frameCount;
        EditorApplication.update += OnCaptureUpdate;
    }

    [MenuItem("DeadSurvivor/HD2D/Measure 300")]
    public static void Measure300()
    {
        RequireLookdevPlay();
        _camera = Camera.main;
        CacheMaterials();
        ConfigurePlate(1);
        var parent = GameObject.Find("Monsters").transform;
        var originals = parent.GetComponentsInChildren<SpriteRenderer>().Where(r => r.name.StartsWith("Monster_")).ToArray();
        for (var i = originals.Length; i < 300; i++)
        {
            var monster = Object.Instantiate(originals[i % originals.Length].gameObject, parent).transform;
            monster.name = "Monster_load_" + i;
        }
        var all = parent.GetComponentsInChildren<SpriteRenderer>().Where(r => r.name.StartsWith("Monster_")).ToArray();
        for (var i = 0; i < all.Length; i++)
        {
            var column = i % 25;
            var row = i / 25;
            all[i].transform.position = new Vector3((column - 12f) * 0.44f, 0f, (row - 5.5f) * 0.62f);
            all[i].GetComponent<PixelShadowCaster>().SetCastingShadow(false);
        }
        _originalTarget = _camera.targetTexture;
        _measurementTarget = new RenderTexture(1920, 1080, 24) { name = "HD2D measurement target" };
        _camera.targetTexture = _measurementTarget;
        _measurement = 0;
        _measurementStart = EditorApplication.timeSinceStartup + 2f;
        _measurementFrame = Time.frameCount;
        _sampleList = new List<Sample>();
        _isMeasuring = true;
        EditorApplication.update += OnMeasurementUpdate;
    }

    public static void ConfigurePlate(int index)
    {
        _camera = Camera.main;
        foreach (var pair in OriginalMaterialDict)
        {
            pair.Key.sharedMaterials = index == 0 ? BaselineMaterialDict[pair.Key] : pair.Value;
        }
        var angle = index == 2 || index == 3 || index == 4 ? 40f : 30f;
        // F starts from B: 30 degrees preserves the current floor extent.
        var isOrthographic = index != 3 && index != 4;
        var fov = index == 4 ? 8f : 12f;
        var heightFraction = index == 5 ? 0.10f : 0.133f;
        var hero = GameObject.Find("Member_bruno").GetComponent<SpriteRenderer>();
        hero.GetComponent<PixelWalkPreview>().enabled = false;
        hero.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(LookdevSceneBuilder.SpritePath + "/Characters/bruno/front/bruno_front.png");
        var focus = hero.transform.position + Quaternion.Euler(angle, 0, 0) * Vector3.up * 0.62f;
        TiltedCameraUtils.Configure(_camera, angle, isOrthographic, fov, heightFraction, focus);
        foreach (var body in Object.FindObjectsByType<SpriteRenderer>(FindObjectsSortMode.None))
        {
            if (body.name != "Blob shadow")
            {
                body.transform.rotation = Quaternion.Euler(angle, 0, 0);
            }
        }
        foreach (var blob in Object.FindObjectsByType<SpriteRenderer>(FindObjectsSortMode.None).Where(r => r.name == "Blob shadow"))
        {
            blob.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
        }
        _camera.GetUniversalAdditionalCameraData().renderPostProcessing = index != 0;
        // The fullscreen effect is toggled on a private renderer clone, never on its saved asset.
        var scopeRenderer = _camera.GetUniversalAdditionalCameraData().scriptableRenderer;
        var rendererData = (ScriptableRendererData[])typeof(UniversalRenderPipelineAsset).GetField("m_RendererDataList", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(QualitySettings.renderPipeline);
        foreach (var feature in rendererData[0].rendererFeatures)
        {
            feature.SetActive(index != 0);
        }
        _camera.aspect = 1920f / 1080f;
        var bounds = MeasureHeroPixels(hero);
        var scale = bounds.height / (1080f * heightFraction);
        if (isOrthographic)
        {
            _camera.orthographicSize *= scale;
        }
        else
        {
            _camera.transform.position = focus + (_camera.transform.position - focus) * scale;
        }
        bounds = MeasureHeroPixels(hero);
        _cameraOrigin = _camera.transform.position;
        var near = TiltedCameraUtils.GroundPoint(_camera, new Vector2(0.5f, 0f));
        var far = TiltedCameraUtils.GroundPoint(_camera, new Vector2(0.5f, 1f));
        var nearDepth = Vector3.Dot(near - _camera.transform.position, _camera.transform.forward);
        var farDepth = Vector3.Dot(far - _camera.transform.position, _camera.transform.forward);
        if (_isCapturing)
        {
            PlateList.Add(new Plate
            {
                id = ((char)('A' + index)).ToString(), orthographic = isOrthographic, angle = angle,
                fov = fov, orthographicSize = _camera.orthographicSize,
                distance = Vector3.Distance(_camera.transform.position, focus), measuredHeightPixels = bounds.height,
                measuredHeightPercent = bounds.height / 1080f * 100f,
                renderedHeightPixels = CaptureHeroMask(hero, ((char)('A' + index)).ToString()),
                topBottomPixelRatio = isOrthographic ? 1f : nearDepth / farDepth,
                groundCorners = string.Join(";", new[] { TiltedCameraUtils.GroundPoint(_camera, Vector2.zero), TiltedCameraUtils.GroundPoint(_camera, Vector2.right), TiltedCameraUtils.GroundPoint(_camera, Vector2.one), TiltedCameraUtils.GroundPoint(_camera, Vector2.up) }.Select(p => p.ToString("F3"))),
                firstFrame = Time.frameCount
            });
        }
    }

    public static void Capture(string path, int width, int height)
    {
        RequireLookdevPlay();
        var camera = Camera.main;
        var previousAspect = camera.aspect;
        var previousActive = RenderTexture.active;
        var target = RenderTexture.GetTemporary(width, height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
        target.antiAliasing = 1;
        var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
        try
        {
            camera.aspect = (float)width / height;
            RenderPipeline.SubmitRenderRequest(camera, new UniversalRenderPipeline.SingleCameraRequest { destination = target });
            RenderTexture.active = target;
            texture.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            texture.Apply();
            File.WriteAllBytes(path, texture.EncodeToPNG());
        }
        finally
        {
            camera.aspect = previousAspect;
            RenderTexture.active = previousActive;
            RenderTexture.ReleaseTemporary(target);
            Object.DestroyImmediate(texture);
        }
    }

    private static int CaptureHeroMask(SpriteRenderer hero, string id)
    {
        var renderers = Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None);
        var enabledStates = renderers.Select(r => r.enabled).ToArray();
        var color = _camera.backgroundColor;
        var data = _camera.GetUniversalAdditionalCameraData();
        var isPostProcessing = data.renderPostProcessing;
        var path = Output + "/" + id + "-height-mask.png";
        try
        {
            foreach (var renderer in renderers)
            {
                renderer.enabled = renderer == hero;
            }
            _camera.backgroundColor = Color.black;
            data.renderPostProcessing = false;
            Capture(path, 1920, 1080);
        }
        finally
        {
            for (var i = 0; i < renderers.Length; i++)
            {
                renderers[i].enabled = enabledStates[i];
            }
            _camera.backgroundColor = color;
            data.renderPostProcessing = isPostProcessing;
        }
        var texture = new Texture2D(2, 2);
        texture.LoadImage(File.ReadAllBytes(path));
        var pixels = texture.GetPixels32();
        var minY = texture.height;
        var maxY = -1;
        for (var y = 0; y < texture.height; y++)
        {
            for (var x = 0; x < texture.width; x++)
            {
                var pixel = pixels[y * texture.width + x];
                if (pixel.r > 2 || pixel.g > 2 || pixel.b > 2)
                {
                    minY = Mathf.Min(minY, y);
                    maxY = Mathf.Max(maxY, y);
                }
            }
        }
        Object.DestroyImmediate(texture);
        return maxY - minY + 1;
    }

    private static Rect MeasureHeroPixels(SpriteRenderer hero)
    {
        // PNG alpha bounds: the imported sprite bounds include the transparent 64px canvas.
        var sprite = hero.sprite;
        var texture = new Texture2D(2, 2);
        texture.LoadImage(File.ReadAllBytes(AssetDatabase.GetAssetPath(sprite)));
        var pixels = texture.GetPixels32();
        var minPixel = new Vector2(texture.width, texture.height);
        var maxPixel = Vector2.zero;
        for (var y = 0; y < texture.height; y++)
        {
            for (var x = 0; x < texture.width; x++)
            {
                if (pixels[y * texture.width + x].a >= 128)
                {
                    minPixel = Vector2.Min(minPixel, new Vector2(x, y));
                    maxPixel = Vector2.Max(maxPixel, new Vector2(x + 1, y + 1));
                }
            }
        }
        var min = (minPixel - sprite.pivot) / sprite.pixelsPerUnit;
        var max = (maxPixel - sprite.pivot) / sprite.pixelsPerUnit;
        Object.DestroyImmediate(texture);
        var points = new[] { new Vector3(min.x,min.y,0), new Vector3(max.x,min.y,0), new Vector3(min.x,max.y,0), new Vector3(max.x,max.y,0) };
        var projected = points.Select(p => _camera.WorldToViewportPoint(hero.transform.TransformPoint(p))).ToArray();
        var minY = projected.Min(p => p.y);
        var maxY = projected.Max(p => p.y);
        return new Rect(0, minY * 1080, 0, (maxY - minY) * 1080);
    }

    private static void CacheMaterials()
    {
        if (OriginalMaterialDict.Count > 0)
        {
            return;
        }
        foreach (var renderer in Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None).Where(r => r.shadowCastingMode != ShadowCastingMode.ShadowsOnly))
        {
            OriginalMaterialDict[renderer] = renderer.sharedMaterials;
            var baseline = renderer.sharedMaterials.Select(m =>
            {
                if (m.shader.name.StartsWith("DeadSurvivor/"))
                {
                    return m;
                }
                if (m.name == "ShorePalette")
                {
                    return AssetDatabase.LoadAssetAtPath<Material>("Assets/DeadSurvivor/Art/Environment/Map/Stone045s2.mat");
                }
                if (m.name.StartsWith("Palette_") || m.name.StartsWith("World_"))
                {
                    return AssetDatabase.LoadAssetAtPath<Material>(LookdevSceneBuilder.ArtPath + "/Baseline_" + m.name.Split('_')[1] + ".mat");
                }
                var original = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                original.hideFlags = HideFlags.HideAndDontSave;
                original.color = renderer.name.Contains("grass") ? new Color(0.43f,0.52f,0.29f) : m.HasProperty("_BaseColor") ? m.GetColor("_BaseColor") : Color.white;
                return original;
            }).ToArray();
            BaselineMaterialDict[renderer] = baseline;
        }
    }

    private static void OnCaptureUpdate()
    {
        if (!_isCapturing || !EditorApplication.isPlaying)
        {
            EditorApplication.update -= OnCaptureUpdate;
            return;
        }
        if (EditorApplication.timeSinceStartup < _nextCapture || Time.frameCount == _lastFrame)
        {
            return;
        }
        try
        {
            var id = ((char)('A' + _plate)).ToString();
            var width = _shot == 9 ? 2520 : _shot == 10 ? 2048 : 1920;
            var height = _shot == 10 ? 1536 : 1080;
            var suffix = _shot == 0 ? "main" : _shot < 9 ? "move-" + (_shot - 1) : width + "x" + height;
            var move = _shot > 0 && _shot < 9 ? (_shot - 1) / 144f : 0f;
            if (_camera.orthographic)
            {
                var screenPixel = 2f * _camera.orthographicSize / 1080f;
                move = Mathf.Round(move / screenPixel) * screenPixel;
            }
            _camera.transform.position = _cameraOrigin + Vector3.right * move;
            Capture(Output + "/" + id + "-" + suffix + ".png", width, height);
            _lastFrame = Time.frameCount;
            _nextCapture = EditorApplication.timeSinceStartup + 0.15f;
            _shot++;
            if (_shot == 11)
            {
                PlateList[_plate].lastFrame = Time.frameCount;
                _plate++;
                _shot = 0;
                if (_plate == 6)
                {
                    File.WriteAllText(Output + "/plates.json", JsonUtility.ToJson(new PlateCollection { plates = PlateList }, true));
                    _isCapturing = false;
                    EditorApplication.update -= OnCaptureUpdate;
                    ConfigurePlate(1);
                }
                else
                {
                    ConfigurePlate(_plate);
                    _nextCapture += 0.5f;
                }
            }
        }
        catch (Exception exception)
        {
            File.WriteAllText(Output + "/capture-error.txt", exception.ToString());
            _isCapturing = false;
            EditorApplication.update -= OnCaptureUpdate;
            throw;
        }
    }

    private static void OnMeasurementUpdate()
    {
        if (!EditorApplication.isPlaying)
        {
            EditorApplication.update -= OnMeasurementUpdate;
            return;
        }
        var now = EditorApplication.timeSinceStartup;
        if (now < _measurementStart || Time.frameCount == _measurementFrame)
        {
            return;
        }
        _measurementFrame = Time.frameCount;
        _sampleList.Add(new Sample { frame = Time.frameCount, draws = UnityStats.drawCalls, setPass = UnityStats.setPassCalls, triangles = UnityStats.triangles, shadowCasters = UnityStats.shadowCasters, frameMs = UnityStats.frameTime * 1000f, renderMs = UnityStats.renderTime * 1000f, deltaMs = Time.unscaledDeltaTime * 1000f });
        if (now - _measurementStart < 20f)
        {
            return;
        }
        var pipeline = (UniversalRenderPipelineAsset)QualitySettings.renderPipeline;
        var record = new Measurement { utc = DateTime.UtcNow.ToString("O"), source = "Unity Editor Mobile settings; UnityStats; one sample per advancing rendered frame; editor timings, not device GPU", realMonsterShadows = _measurement == 1, monsters = 300, width = _camera.pixelWidth, height = _camera.pixelHeight, seconds = now - _measurementStart, quality = QualitySettings.names[QualitySettings.GetQualityLevel()], renderScale = pipeline.renderScale, srpBatcher = pipeline.useSRPBatcher, samples = _sampleList };
        File.WriteAllText(Output + "/measure-" + (_measurement == 0 ? "blob" : "real") + ".json", JsonUtility.ToJson(record, true));
        Capture(Output + "/load-" + (_measurement == 0 ? "blob" : "real") + ".png", 1920, 1080);
        _measurement++;
        if (_measurement == 2)
        {
            _isMeasuring = false;
            RestoreMeasurementTarget();
            EditorApplication.update -= OnMeasurementUpdate;
            return;
        }
        foreach (var monster in GameObject.Find("Monsters").GetComponentsInChildren<SpriteRenderer>().Where(r => r.name.StartsWith("Monster_")))
        {
            monster.GetComponent<PixelShadowCaster>().SetCastingShadow(true);
        }
        _sampleList = new List<Sample>();
        _measurementStart = EditorApplication.timeSinceStartup + 2f;
    }

    private static void OnPlayModeStateChanged(PlayModeStateChange state)
    {
        if (state != PlayModeStateChange.ExitingPlayMode)
        {
            return;
        }
        RestoreMeasurementTarget();
        EditorApplication.update -= OnCaptureUpdate;
        EditorApplication.update -= OnMeasurementUpdate;
        _isCapturing = false;
        _isMeasuring = false;
        OriginalMaterialDict.Clear();
        foreach (var materials in BaselineMaterialDict.Values)
        {
            foreach (var material in materials.Where(m => m.hideFlags == HideFlags.HideAndDontSave))
            {
                Object.DestroyImmediate(material);
            }
        }
        BaselineMaterialDict.Clear();
    }

    private static void RestoreMeasurementTarget()
    {
        if (_measurementTarget == null)
        {
            return;
        }
        _camera.targetTexture = _originalTarget;
        _measurementTarget.Release();
        Object.DestroyImmediate(_measurementTarget);
        _measurementTarget = null;
    }

    private static void RequireLookdevPlay()
    {
        if (!EditorApplication.isPlaying || UnityEngine.SceneManagement.SceneManager.GetActiveScene().path != LookdevSceneBuilder.ScenePath)
        {
            throw new InvalidOperationException("This command requires Play Mode in Lookdev_HD2D only.");
        }
    }
}
