Shader "HD2D/Round8/BandBlur"
{
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Cull Off ZTest Always ZWrite Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
            half4 Frag(Varyings i):SV_Target
            {
                float2 uv=i.texcoord;
                float amount=max(1-smoothstep(0.0,0.16,uv.y),smoothstep(0.76,1.0,uv.y));
                float2 d=_BlitTexture_TexelSize.xy*amount*1.8;
                half4 c=SAMPLE_TEXTURE2D_X(_BlitTexture,sampler_PointClamp,uv)*0.28;
                c+=SAMPLE_TEXTURE2D_X(_BlitTexture,sampler_PointClamp,uv+d*float2(1,0))*0.12;
                c+=SAMPLE_TEXTURE2D_X(_BlitTexture,sampler_PointClamp,uv-d*float2(1,0))*0.12;
                c+=SAMPLE_TEXTURE2D_X(_BlitTexture,sampler_PointClamp,uv+d*float2(0,1))*0.12;
                c+=SAMPLE_TEXTURE2D_X(_BlitTexture,sampler_PointClamp,uv-d*float2(0,1))*0.12;
                c+=SAMPLE_TEXTURE2D_X(_BlitTexture,sampler_PointClamp,uv+d)*0.06;
                c+=SAMPLE_TEXTURE2D_X(_BlitTexture,sampler_PointClamp,uv-d)*0.06;
                c+=SAMPLE_TEXTURE2D_X(_BlitTexture,sampler_PointClamp,uv+d*float2(-1,1))*0.06;
                c+=SAMPLE_TEXTURE2D_X(_BlitTexture,sampler_PointClamp,uv+d*float2(1,-1))*0.06;
                float edge=smoothstep(0.38,0.7071068,length(uv-0.5));
                c.rgb*=lerp(1,0.82,edge);
                return c;
            }
            ENDHLSL
        }
    }
}
