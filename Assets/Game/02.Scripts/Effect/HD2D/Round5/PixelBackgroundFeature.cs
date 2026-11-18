using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering.RenderGraphModule.Util;

public sealed class PixelBackgroundFeature : ScriptableRendererFeature
{
    public bool captureBackground;
    public Material outlineMaterial;
    public Material depthMaterial;
    public Material compositeMaterial;
    private BackgroundPass _pass;

    public override void Create()
    {
        _pass = new BackgroundPass(this);
    }

    public override void AddRenderPasses(ScriptableRenderer renderer,ref RenderingData renderingData)
    {
        var camera = renderingData.cameraData.camera;
        if (captureBackground ? camera.GetComponent<PixelBackgroundSource>() == null : camera.GetComponent<PixelBackgroundRig>() == null) return;
        renderer.EnqueuePass(_pass);
    }

    private sealed class BackgroundPass : ScriptableRenderPass
    {
        private readonly PixelBackgroundFeature _settings;
        private sealed class PassData
        {
            public Material material;
            public TextureHandle source;
            public TextureHandle depth;
            public TextureHandle normals;
            public bool composite;
            public Vector4 texelSize;
            public Vector4 outputSize;
        }

        public BackgroundPass(PixelBackgroundFeature settings)
        {
            _settings = settings;
            renderPassEvent = settings.captureBackground ? RenderPassEvent.AfterRenderingTransparents : RenderPassEvent.BeforeRenderingOpaques;
            if (settings.captureBackground) ConfigureInput(ScriptableRenderPassInput.Depth|ScriptableRenderPassInput.Normal);
            requiresIntermediateTexture = true;
        }

        public override void RecordRenderGraph(RenderGraph graph,ContextContainer frameData)
        {
            var resources = frameData.Get<UniversalResourceData>();
            var camera = frameData.Get<UniversalCameraData>().camera;
            var rig = _settings.captureBackground ? camera.GetComponent<PixelBackgroundSource>().Rig : camera.GetComponent<PixelBackgroundRig>();
            if (rig == null || rig.Color == null) return;
            if (_settings.captureBackground)
            {
                var exportedDepth = graph.ImportTexture(rig.Depth);
                graph.AddBlitPass(new RenderGraphUtils.BlitMaterialParameters(resources.cameraDepthTexture,exportedDepth,_settings.depthMaterial,0),passName:"Export point background depth");
                var finalColor = resources.activeColorTexture;
                if (rig.EdgesEnabled)
                {
                var description = graph.GetTextureDesc(resources.activeColorTexture);
                description.name = "One texel depth normal outlines";
                var destination = graph.CreateTexture(description);
                using (var builder = graph.AddRasterRenderPass<PassData>("Low resolution depth normal outlines",out var pass))
                {
                    pass.material = _settings.outlineMaterial;
                    pass.source = resources.activeColorTexture;
                    pass.depth = resources.cameraDepthTexture;
                    pass.normals = resources.cameraNormalsTexture;
                    pass.composite = false;
                    pass.texelSize = new Vector4(1f/rig.LowWidth,1f/rig.LowHeight,rig.LowWidth,rig.LowHeight);
                    pass.outputSize = new Vector4(rig.OutputWidth,rig.OutputHeight,rig.Scale,0);
                    builder.UseTexture(pass.source);builder.UseTexture(pass.depth);builder.UseTexture(pass.normals);
                    builder.SetRenderAttachment(destination,0,AccessFlags.Write);
                    builder.SetRenderFunc(static (PassData data,RasterGraphContext context) => Draw(data,context));
                }
                resources.cameraColor = destination;
                finalColor = destination;
                }
                graph.AddBlitPass(finalColor,graph.ImportTexture(rig.Color),Vector2.one,Vector2.zero,filterMode:RenderGraphUtils.BlitFilterMode.ClampNearest,passName:"Export point background color");
            }
            else
            {
                using (var builder = graph.AddRasterRenderPass<PassData>("Point background composition with depth",out var pass))
                {
                    pass.material = _settings.compositeMaterial;
                    pass.source = graph.ImportTexture(rig.Color);
                    pass.depth = graph.ImportTexture(rig.Depth);
                    pass.normals = TextureHandle.nullHandle;
                    pass.composite = true;
                    pass.texelSize = new Vector4(1f/rig.LowWidth,1f/rig.LowHeight,rig.LowWidth,rig.LowHeight);
                    pass.outputSize = new Vector4(rig.OutputWidth,rig.OutputHeight,rig.Scale,0);
                    builder.UseTexture(pass.source);builder.UseTexture(pass.depth);
                    builder.SetRenderAttachment(resources.activeColorTexture,0,AccessFlags.Write);
                    builder.SetRenderAttachmentDepth(resources.activeDepthTexture,AccessFlags.Write);
                    builder.SetRenderFunc(static (PassData data,RasterGraphContext context) => Draw(data,context));
                }
            }
        }

        private static void Draw(PassData data,RasterGraphContext context)
        {
            var properties = new MaterialPropertyBlock();
            properties.SetTexture("_BlitTexture",data.source);
            properties.SetTexture("_BackgroundDepth",data.depth);
            if (!data.composite) properties.SetTexture("_BackgroundNormals",data.normals);
            properties.SetVector("_BackgroundDepth_TexelSize",data.texelSize);
            properties.SetVector("_PixelOutputSize",data.outputSize);
            properties.SetVector("_BlitScaleBias",new Vector4(1,1,0,0));
            context.cmd.DrawProcedural(Matrix4x4.identity,data.material,0,MeshTopology.Triangles,3,1,properties);
        }
    }
}
