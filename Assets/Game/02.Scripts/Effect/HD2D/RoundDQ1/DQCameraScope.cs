using System;
using System.Reflection;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace DeadSurvivor.DQLookdev
{
    // Appends a private renderer slot; never assigns a pipeline or quality setting.
    [ExecuteAlways]
    [RequireComponent(typeof(Camera))]
    public sealed class DQCameraScope : MonoBehaviour
    {
        public ScriptableRendererData RendererData;
        private UniversalRenderPipelineAsset _pipeline;
        private ScriptableRendererData[] _originalList;
        private static readonly FieldInfo RendererListField = typeof(UniversalRenderPipelineAsset).GetField("m_RendererDataList", BindingFlags.Instance | BindingFlags.NonPublic);

        private void OnEnable()
        {
            if (RendererData == null)
            {
                return;
            }
            _pipeline = UniversalRenderPipeline.asset;
            if (_pipeline == null)
            {
                throw new InvalidOperationException("DQ lookdev requires URP.");
            }
            _originalList = (ScriptableRendererData[])RendererListField.GetValue(_pipeline);
            var extendedList = new ScriptableRendererData[_originalList.Length + 1];
            Array.Copy(_originalList, extendedList, _originalList.Length);
            extendedList[_originalList.Length] = RendererData;
            RendererListField.SetValue(_pipeline, extendedList);
            GetComponent<Camera>().GetUniversalAdditionalCameraData().SetRenderer(_originalList.Length);
        }

        private void OnDisable()
        {
            if (_pipeline != null && _originalList != null)
            {
                RendererListField.SetValue(_pipeline, _originalList);
                GetComponent<Camera>().GetUniversalAdditionalCameraData().SetRenderer(-1);
                _originalList = null;
            }
        }

        public void RefreshRenderer()
        {
            OnDisable();
            OnEnable();
        }
    }
}
