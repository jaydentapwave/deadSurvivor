Shader "HD2D/Round8/BackgroundComposite"
{
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Cull Off ZTest Always ZWrite On
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
            TEXTURE2D(_BackgroundDepth);SAMPLER(sampler_BackgroundDepth);
            float4 _PixelOutputSize;
            float4 _BackgroundDepth_TexelSize;
            struct Output {half4 color:SV_Target;float depth:SV_Depth;};
            Output Frag(Varyings i)
            {
                Output o;
                float2 pixel=floor(i.texcoord*_PixelOutputSize.xy);
                float2 uv=(floor(pixel/_PixelOutputSize.z)+.5)*_BackgroundDepth_TexelSize.xy;
                o.color=SAMPLE_TEXTURE2D_X(_BlitTexture,sampler_PointClamp,uv);
                o.depth=SAMPLE_TEXTURE2D(_BackgroundDepth,sampler_PointClamp,uv).r;
                return o;
            }
            ENDHLSL
        }
    }
}
