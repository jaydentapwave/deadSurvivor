Shader "HD2D/ScreenBandBlur"
{
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            ZTest Always ZWrite Off Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
            half4 Frag(Varyings i):SV_Target
            {
                float2 uv=i.texcoord;
                // Clear band [0.13,0.80], centered slightly below screen center.
                float band=max(1-smoothstep(0.0,0.13,uv.y),smoothstep(0.80,1.0,uv.y));
                float2 d=_BlitTexture_TexelSize.xy*band*1.6;
                half4 c=SAMPLE_TEXTURE2D_X(_BlitTexture,sampler_PointClamp,uv)*0.4;
                c+=SAMPLE_TEXTURE2D_X(_BlitTexture,sampler_PointClamp,uv+float2(d.x,0))*0.15;
                c+=SAMPLE_TEXTURE2D_X(_BlitTexture,sampler_PointClamp,uv-float2(d.x,0))*0.15;
                c+=SAMPLE_TEXTURE2D_X(_BlitTexture,sampler_PointClamp,uv+float2(0,d.y))*0.15;
                c+=SAMPLE_TEXTURE2D_X(_BlitTexture,sampler_PointClamp,uv-float2(0,d.y))*0.15;
                return c;
            }
            ENDHLSL
        }
    }
}
