using System.Reflection;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

// All global overrides live on an in-memory clone. No project asset is modified.
public sealed class LookdevRenderScope : MonoBehaviour
{
    [SerializeField] private ScriptableRendererData rendererData;
    [SerializeField] private UniversalRenderPipelineAsset mobilePipeline;
    private RenderPipelineAsset _originalPipeline;
    private UniversalRenderPipelineAsset _pipeline;
    private ScriptableRendererData _renderer;
    private int _originalQuality;
    private bool _originalBackground;
    private int _originalVSync;
    private int _originalFrameRate;

    private void OnEnable()
    {
        _originalQuality = QualitySettings.GetQualityLevel();
        _originalPipeline = QualitySettings.renderPipeline;
        _originalBackground = Application.runInBackground;
        _originalVSync = QualitySettings.vSyncCount;
        _originalFrameRate = Application.targetFrameRate;
        QualitySettings.SetQualityLevel(0, false);
        _pipeline = Instantiate(mobilePipeline);
        _pipeline.name = "HD2D transient Mobile";
        _pipeline.hideFlags = HideFlags.HideAndDontSave;
        _pipeline.renderScale = 1f;
        _pipeline.useSRPBatcher = true;
        _pipeline.msaaSampleCount = 1;
        _pipeline.shadowCascadeCount = 1;
        _pipeline.shadowDistance = 100f;
        QualitySettings.vSyncCount = 0;
        Application.targetFrameRate = -1;
        Application.runInBackground = true;
        _renderer = Instantiate(rendererData);
        _renderer.hideFlags = HideFlags.HideAndDontSave;
        for (var i = 0; i < _renderer.rendererFeatures.Count; i++)
        {
            _renderer.rendererFeatures[i] = Instantiate(_renderer.rendererFeatures[i]);
            _renderer.rendererFeatures[i].hideFlags = HideFlags.HideAndDontSave;
        }
        typeof(UniversalRenderPipelineAsset).GetField("m_RendererDataList", BindingFlags.Instance | BindingFlags.NonPublic)
            .SetValue(_pipeline, new[] { _renderer });
        QualitySettings.renderPipeline = _pipeline;
        QualitySettings.vSyncCount = 0;
        Application.targetFrameRate = -1;
        Application.runInBackground = true;
    }

    private void OnDisable()
    {
        QualitySettings.renderPipeline = mobilePipeline;
        QualitySettings.SetQualityLevel(_originalQuality, false);
        QualitySettings.renderPipeline = _originalPipeline;
        QualitySettings.vSyncCount = _originalVSync;
        Application.targetFrameRate = _originalFrameRate;
        Application.runInBackground = _originalBackground;
        Destroy(_pipeline);
        foreach (var feature in _renderer.rendererFeatures)
        {
            Destroy(feature);
        }
        Destroy(_renderer);
    }
}
