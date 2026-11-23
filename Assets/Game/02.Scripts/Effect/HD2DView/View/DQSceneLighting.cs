using UnityEngine;
using UnityEngine.Rendering;

// Visual globals are scoped to the Main camera. Camera transform remains TiltedCameraRig-owned.
[ExecuteAlways, RequireComponent(typeof(Camera))]
public sealed class DQSceneLighting : MonoBehaviour
{
    // Air colour starts this far beyond the focus and is full at AirEnd (metres): across the stream the meadow
    // already pales (step 14), the far forest is fully hazed.
    public const float AirStart = 5f;
    public const float AirEnd = 44f;
    private Vector4 _previousFocus;
    private Vector4 _previousFog;
    private Camera _camera;
    private bool _applied;
    private void OnEnable()
    {
        _camera = GetComponent<Camera>();
        _previousFocus = Shader.GetGlobalVector("_DQLightFocus");
        _previousFog = Shader.GetGlobalVector("_DQAirColor");
        RenderPipelineManager.beginCameraRendering += OnCameraRendering;
        RenderPipelineManager.endCameraRendering += OnCameraRendered;
    }

    private void OnCameraRendering(ScriptableRenderContext context, Camera camera)
    {
        if (camera != _camera)
        {
            return;
        }
        _previousFocus = Shader.GetGlobalVector("_DQLightFocus");
        _previousFog = Shader.GetGlobalVector("_DQAirColor");
        _applied = true;
        var rig = GetComponent<TiltedCameraRig>();
        var distance = rig != null && rig.Distance > 0 ? rig.Distance : TiltedCameraRig.DQDistance;
        Shader.SetGlobalVector("_DQLightFocus", new Vector4(distance, AirStart, AirEnd, 1));
        var color = new Color(120f / 255, 181f / 255, 214f / 255).linear;
        Shader.SetGlobalVector("_DQAirColor", new Vector4(color.r, color.g, color.b, 1));
    }

    private void OnDisable()
    {
        RenderPipelineManager.beginCameraRendering -= OnCameraRendering;
        RenderPipelineManager.endCameraRendering -= OnCameraRendered;
        Restore();
    }

    private void OnCameraRendered(ScriptableRenderContext context, Camera camera)
    {
        if (camera == _camera) Restore();
    }

    private void Restore()
    {
        if (!_applied) return;
        Shader.SetGlobalVector("_DQLightFocus", _previousFocus);
        Shader.SetGlobalVector("_DQAirColor", _previousFog);
        _applied = false;
    }
}
