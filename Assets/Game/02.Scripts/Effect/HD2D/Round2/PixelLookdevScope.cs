using System;
using System.Reflection;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public sealed class PixelLookdevScope : MonoBehaviour
{
    private int _originalQuality;
    private int _mobileIndex;
    private int _originalVSync;
    private int _mobileVSync;
    private int _originalFrameRate;
    private bool _originalBackground;
    private bool _isInitialized;
    private RenderPipelineAsset _originalPipeline;
    private RenderPipelineAsset _originalMobile;
    private UniversalRenderPipelineAsset _pipeline;
    private ScriptableRendererData _renderer;

    public void Initialize(UniversalRenderPipelineAsset mobile, ScriptableRendererData renderer)
    {
        _originalQuality = QualitySettings.GetQualityLevel();
        _originalPipeline = QualitySettings.renderPipeline;
        _originalVSync = QualitySettings.vSyncCount;
        _originalFrameRate = Application.targetFrameRate;
        _originalBackground = Application.runInBackground;
        _mobileIndex = Array.IndexOf(QualitySettings.names, "Mobile");
        if (_mobileIndex < 0)
        {
            throw new InvalidOperationException("Mobile quality is missing.");
        }
        QualitySettings.SetQualityLevel(_mobileIndex, false);
        _originalMobile = QualitySettings.renderPipeline;
        _mobileVSync = QualitySettings.vSyncCount;
        _isInitialized = true;
        try
        {
            _pipeline = Instantiate(mobile);
            _pipeline.name = "F2 transient Mobile";
            _pipeline.hideFlags = HideFlags.HideAndDontSave;
            _pipeline.renderScale = 1f;
            _pipeline.msaaSampleCount = 1;
            _pipeline.useSRPBatcher = true;
            _pipeline.shadowCascadeCount = 1;
            _pipeline.shadowDistance = 35f;
            _renderer = Instantiate(renderer);
            _renderer.hideFlags = HideFlags.HideAndDontSave;
            for (var i = 0; i < _renderer.rendererFeatures.Count; i++)
            {
                _renderer.rendererFeatures[i] = Instantiate(_renderer.rendererFeatures[i]);
                _renderer.rendererFeatures[i].hideFlags = HideFlags.HideAndDontSave;
            }
            typeof(UniversalRenderPipelineAsset).GetField("m_RendererDataList", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(_pipeline, new[] { _renderer });
            QualitySettings.renderPipeline = _pipeline;
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = -1;
            Application.runInBackground = true;
        }
        catch
        {
            Restore();
            throw;
        }
    }

    public void RequireMobile()
    {
        if (QualitySettings.GetQualityLevel() != _mobileIndex || QualitySettings.renderPipeline != _pipeline || _pipeline.renderScale != 1f || !_pipeline.useSRPBatcher)
        {
            throw new InvalidOperationException("Actual Mobile/scale/batcher settings changed during collection.");
        }
    }

    public void SetScreenEffects(bool isEnabled)
    {
        foreach (var feature in _renderer.rendererFeatures)
        {
            feature.SetActive(isEnabled);
        }
    }

    public void Restore()
    {
        if (!_isInitialized)
        {
            return;
        }
        _isInitialized = false;
        try
        {
            QualitySettings.SetQualityLevel(_mobileIndex, false);
            QualitySettings.renderPipeline = _originalMobile;
            QualitySettings.vSyncCount = _mobileVSync;
        }
        finally
        {
            QualitySettings.SetQualityLevel(_originalQuality, false);
            QualitySettings.renderPipeline = _originalPipeline;
            QualitySettings.vSyncCount = _originalVSync;
            Application.targetFrameRate = _originalFrameRate;
            Application.runInBackground = _originalBackground;
            Destroy(_pipeline);
            if (_renderer != null)
            {
                foreach (var feature in _renderer.rendererFeatures)
                {
                    Destroy(feature);
                }
                Destroy(_renderer);
            }
        }
    }

    private void OnDisable()
    {
        Restore();
    }
}
