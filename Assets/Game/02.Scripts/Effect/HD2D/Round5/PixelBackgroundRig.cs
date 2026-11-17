using System;
using System.Reflection;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

// Separate low-resolution geometry from native-resolution sprite cards.
public sealed class PixelBackgroundRig : MonoBehaviour
{
    public const int BackgroundLayer = 27;
    public Camera BackgroundCamera { get; private set; }
    public RTHandle Color { get; private set; }
    public RTHandle Depth { get; private set; }
    public int Scale { get; private set; }
    public int OutputWidth { get; private set; }
    public int OutputHeight { get; private set; }
    public int LowWidth => Color.rt.width;
    public int LowHeight => Color.rt.height;
    public bool EdgesEnabled { get; set; } = true;
    private Camera _camera;
    private RenderTexture _cameraTarget;
    private UniversalRenderPipelineAsset _pipeline;
    private ScriptableRendererData[] _originalRenderers;
    private ScriptableRendererData _backgroundRenderer;
    private int _originalMask;
    private static readonly FieldInfo RendererList = typeof(UniversalRenderPipelineAsset).GetField("m_RendererDataList",BindingFlags.NonPublic|BindingFlags.Instance);

    public void Initialize(ScriptableRendererData backgroundRenderer)
    {
        _camera = GetComponent<Camera>();
        _originalMask = _camera.cullingMask;
        _pipeline = (UniversalRenderPipelineAsset)QualitySettings.renderPipeline;
        _originalRenderers = (ScriptableRendererData[])RendererList.GetValue(_pipeline);
        _backgroundRenderer = Instantiate(backgroundRenderer);
        _backgroundRenderer.hideFlags = HideFlags.HideAndDontSave;
        for (var i = 0; i < _backgroundRenderer.rendererFeatures.Count; i++)
        {
            _backgroundRenderer.rendererFeatures[i] = Instantiate(_backgroundRenderer.rendererFeatures[i]);
            _backgroundRenderer.rendererFeatures[i].hideFlags = HideFlags.HideAndDontSave;
        }
        RendererList.SetValue(_pipeline,new[] { _originalRenderers[0],_backgroundRenderer });
        BackgroundCamera = new GameObject("Pixel background camera").AddComponent<Camera>();
        BackgroundCamera.gameObject.hideFlags = HideFlags.DontSave;
        BackgroundCamera.CopyFrom(_camera);
        BackgroundCamera.tag = "Untagged";
        BackgroundCamera.depth = _camera.depth-1;
        BackgroundCamera.gameObject.AddComponent<PixelBackgroundSource>().Rig = this;
        var data = BackgroundCamera.GetUniversalAdditionalCameraData();
        data.SetRenderer(1);
        data.renderPostProcessing = false;
        data.antialiasing = AntialiasingMode.None;
        data.requiresDepthTexture = true;
        data.volumeLayerMask = 0;
        BackgroundCamera.cullingMask = 1<<BackgroundLayer;
        _camera.cullingMask &= ~(1<<BackgroundLayer);
        Sync(1920,1080);
    }

    private void LateUpdate()
    {
        if (BackgroundCamera != null) Sync(_camera.pixelWidth,_camera.pixelHeight);
    }

    public void Sync(int width,int height)
    {
        OutputWidth = width;OutputHeight = height;
        Scale = Mathf.Max(1,Mathf.RoundToInt(height/(_camera.orthographicSize*2*48)));
        var lowWidth = Mathf.CeilToInt((float)width/Scale);
        var lowHeight = Mathf.CeilToInt((float)height/Scale);
        if (Color == null || Color.rt.width != lowWidth || Color.rt.height != lowHeight)
        {
            Color?.Release();Depth?.Release();
            if (_cameraTarget != null) { _cameraTarget.Release();Destroy(_cameraTarget); }
            _cameraTarget = new RenderTexture(lowWidth,lowHeight,24,RenderTextureFormat.ARGBHalf) { name = "Background camera target",filterMode = FilterMode.Point,antiAliasing = 1,useMipMap = false };
            _cameraTarget.Create();
            var color = new RenderTexture(lowWidth,lowHeight,0,RenderTextureFormat.ARGBHalf) { name = "Point background color",filterMode = FilterMode.Point,antiAliasing = 1,useMipMap = false };
            var depth = new RenderTexture(lowWidth,lowHeight,0,RenderTextureFormat.RFloat) { name = "Point background depth",filterMode = FilterMode.Point,antiAliasing = 1,useMipMap = false };
            color.Create();depth.Create();
            Color = RTHandles.Alloc(color);Depth = RTHandles.Alloc(depth);
        }
        BackgroundCamera.transform.SetPositionAndRotation(_camera.transform.position,_camera.transform.rotation);
        BackgroundCamera.orthographicSize = _camera.orthographicSize;
        BackgroundCamera.aspect = (float)width/height;
        BackgroundCamera.nearClipPlane = _camera.nearClipPlane;
        BackgroundCamera.farClipPlane = _camera.farClipPlane;
        BackgroundCamera.backgroundColor = _camera.backgroundColor;
        var projection = _camera.projectionMatrix;
        var ax = (float)width/(lowWidth*Scale);
        var ay = (float)height/(lowHeight*Scale);
        projection.m00 *= ax;projection.m03 = projection.m03*ax+ax-1;
        projection.m11 *= ay;projection.m13 = projection.m13*ay+ay-1;
        BackgroundCamera.projectionMatrix = projection;
        BackgroundCamera.targetTexture = _cameraTarget;
    }

    public void RenderBackground(int width,int height)
    {
        Sync(width,height);
        var request = new UniversalRenderPipeline.SingleCameraRequest { destination = _cameraTarget };
        RenderPipeline.SubmitRenderRequest(BackgroundCamera,request);
    }

    public void KeepCompositeEnabled()
    {
        foreach (var feature in _originalRenderers[0].rendererFeatures)
        {
            if (feature is PixelBackgroundFeature) feature.SetActive(true);
        }
    }

    private void OnDisable()
    {
        if (_camera != null) _camera.cullingMask = _originalMask;
        if (_pipeline != null && _originalRenderers != null) RendererList.SetValue(_pipeline,_originalRenderers);
        if (BackgroundCamera != null) Destroy(BackgroundCamera.gameObject);
        Color?.Release();Depth?.Release();Color = null;Depth = null;
        if (_cameraTarget != null) { _cameraTarget.Release();Destroy(_cameraTarget); }
        if (_backgroundRenderer != null)
        {
            foreach (var feature in _backgroundRenderer.rendererFeatures) Destroy(feature);
            Destroy(_backgroundRenderer);
        }
    }
}

public sealed class PixelBackgroundSource : MonoBehaviour
{
    public PixelBackgroundRig Rig { get; set; }
}
