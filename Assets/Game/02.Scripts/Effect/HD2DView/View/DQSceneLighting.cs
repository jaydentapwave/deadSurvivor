using UnityEngine;
using UnityEngine.Rendering;

// Visual globals are scoped to the Main camera. Camera transform remains TiltedCameraRig-owned.
[ExecuteAlways, RequireComponent(typeof(Camera))]
public sealed class DQSceneLighting : MonoBehaviour
{
    private Vector4 _previousFocus;
    private Vector4 _previousFog;
    private Camera _camera;
    private void OnEnable()
    {
        _camera = GetComponent<Camera>();
        _previousFocus = Shader.GetGlobalVector("_DQLightFocus");
        _previousFog = Shader.GetGlobalVector("_DQAirColor");
        RenderPipelineManager.beginCameraRendering += OnCameraRendering;
    }

    private void OnCameraRendering(ScriptableRenderContext context, Camera camera)
    {
        if (camera != _camera)
        {
            return;
        }
        var rig = GetComponent<TiltedCameraRig>();
        var distance = rig != null && rig.Distance > 0 ? rig.Distance : TiltedCameraRig.DQDistance;
        Shader.SetGlobalVector("_DQLightFocus", new Vector4(distance, 8, 48, 1));
        var color = new Color(120f / 255, 181f / 255, 214f / 255).linear;
        Shader.SetGlobalVector("_DQAirColor", new Vector4(color.r, color.g, color.b, 1));
    }

    private void OnDisable()
    {
        RenderPipelineManager.beginCameraRendering -= OnCameraRendering;
        Shader.SetGlobalVector("_DQLightFocus", _previousFocus);
        Shader.SetGlobalVector("_DQAirColor", _previousFog);
    }
}
