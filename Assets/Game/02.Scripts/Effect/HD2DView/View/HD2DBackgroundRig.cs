using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

// Native sprite cards share depth with a point-scaled background. Renderer assets are supplied by the scene.
[RequireComponent(typeof(Camera))]
public sealed class HD2DBackgroundRig : MonoBehaviour
{
    public const int BackgroundLayer = 27;
    public RTHandle Color
    {
        get; private set;
    }
    public RTHandle Depth
    {
        get; private set;
    }
    public int OutputWidth
    {
        get; private set;
    }
    public int OutputHeight
    {
        get; private set;
    }
    public int LowWidth => Color.rt.width;
    public int LowHeight => Color.rt.height;
    public int Scale
    {
        get; private set;
    }
    public bool EdgesEnabled { get; set; } = true;
    [SerializeField] private int backgroundRendererIndex;
    private Camera _camera;
    private Camera _backgroundCamera;
    private RenderTexture _target;
    private int _originalMask;

    private void OnEnable()
    {
        _camera = GetComponent<Camera>();
        _originalMask = _camera.cullingMask;
        _backgroundCamera = new GameObject("Point background camera").AddComponent<Camera>();
        _backgroundCamera.gameObject.hideFlags = HideFlags.DontSave;
        _backgroundCamera.CopyFrom(_camera);
        _backgroundCamera.tag = "Untagged";
        _backgroundCamera.depth = _camera.depth - 1;
        _backgroundCamera.cullingMask = 1 << BackgroundLayer;
        _backgroundCamera.allowMSAA = false;
        _backgroundCamera.gameObject.AddComponent<HD2DBackgroundSource>().Rig = this;
        var data = _backgroundCamera.GetUniversalAdditionalCameraData();
        data.SetRenderer(backgroundRendererIndex);
        data.renderPostProcessing = false;
        data.antialiasing = AntialiasingMode.None;
        data.requiresDepthTexture = true;
        data.volumeLayerMask = 0;
        _camera.cullingMask &= ~(1 << BackgroundLayer);
        Sync();
        RenderPipelineManager.beginContextRendering += OnBeginContext;
    }

    private void OnBeginContext(ScriptableRenderContext context, List<Camera> cameras) => Sync();

    private void Sync()
    {
        OutputWidth = _camera.pixelWidth;
        OutputHeight = _camera.pixelHeight;
        Scale = Mathf.Max(1, Mathf.RoundToInt(OutputHeight / (_camera.orthographicSize * 2 * 48)));
        var width = Mathf.CeilToInt((float)OutputWidth / Scale);
        var height = Mathf.CeilToInt((float)OutputHeight / Scale);
        if (Color == null || LowWidth != width || LowHeight != height)
        {
            ReleaseTargets();
            _target = Texture(width, height, 24, RenderTextureFormat.ARGBHalf, "Background target");
            Color = RTHandles.Alloc(Texture(width, height, 0, RenderTextureFormat.ARGBHalf, "Point background color"), transferOwnership: true);
            Depth = RTHandles.Alloc(Texture(width, height, 0, RenderTextureFormat.RFloat, "Point background depth"), transferOwnership: true);
        }
        _backgroundCamera.transform.SetPositionAndRotation(_camera.transform.position, _camera.transform.rotation);
        _backgroundCamera.orthographicSize = _camera.orthographicSize;
        _backgroundCamera.aspect = (float)OutputWidth / OutputHeight;
        _backgroundCamera.nearClipPlane = _camera.nearClipPlane;
        _backgroundCamera.farClipPlane = _camera.farClipPlane;
        _backgroundCamera.backgroundColor = _camera.backgroundColor;
        var projection = _camera.projectionMatrix;
        var ax = (float)OutputWidth / (width * Scale);
        var ay = (float)OutputHeight / (height * Scale);
        projection.m00 *= ax;
        projection.m03 = projection.m03 * ax + ax - 1;
        projection.m11 *= ay;
        projection.m13 = projection.m13 * ay + ay - 1;
        _backgroundCamera.projectionMatrix = projection;
        _backgroundCamera.targetTexture = _target;
    }

    private void OnDisable()
    {
        RenderPipelineManager.beginContextRendering -= OnBeginContext;
        if (_camera != null)
        {
            _camera.cullingMask = _originalMask;
        }
        if (_backgroundCamera != null)
        {
            DestroyOwned(_backgroundCamera.gameObject);
        }
        ReleaseTargets();
    }

    private void ReleaseTargets()
    {
        if (_backgroundCamera != null)
        {
            _backgroundCamera.targetTexture = null;
        }
        Color?.Release();
        Depth?.Release();
        Color = null;
        Depth = null;
        if (_target != null)
        {
            _target.Release();
            DestroyOwned(_target);
            _target = null;
        }
    }
    private static RenderTexture Texture(int width, int height, int depth, RenderTextureFormat format, string name)
    {
        var texture = new RenderTexture(width, height, depth, format) { name = name, filterMode = FilterMode.Point, antiAliasing = 1, useMipMap = false };
        texture.Create();
        return texture;
    }
    private static void DestroyOwned(Object value)
    {
        if (Application.isPlaying)
        {
            Destroy(value);
        }
        else
        {
            DestroyImmediate(value);
        }
    }
}
