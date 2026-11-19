// Source: Assets/Game/03.Resources/Art/HD2D/Round8/FieldBandBlur.shader SHA256 1d29094add56e0e6c719a81d025cb91ffbfb61e6ab5b3e48a7b82b8607c1cc16
Shader "HD2D/HD2D_BandBlur"
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
                // Keep every point sample on an output-pixel offset. Vary only
                // strength: shrinking subpixel offsets erased the intended band edges.
                float4 center=SAMPLE_TEXTURE2D_X(_BlitTexture,sampler_PointClamp,uv);
                if (amount<=0) return center;
                float2 d=_BlitTexture_TexelSize.xy*2;
                float4 c=center*0.28;
                c+=SAMPLE_TEXTURE2D_X(_BlitTexture,sampler_PointClamp,uv+d*float2(1,0))*0.12;
                c+=SAMPLE_TEXTURE2D_X(_BlitTexture,sampler_PointClamp,uv-d*float2(1,0))*0.12;
                c+=SAMPLE_TEXTURE2D_X(_BlitTexture,sampler_PointClamp,uv+d*float2(0,1))*0.12;
                c+=SAMPLE_TEXTURE2D_X(_BlitTexture,sampler_PointClamp,uv-d*float2(0,1))*0.12;
                c+=SAMPLE_TEXTURE2D_X(_BlitTexture,sampler_PointClamp,uv+d)*0.06;
                c+=SAMPLE_TEXTURE2D_X(_BlitTexture,sampler_PointClamp,uv-d)*0.06;
                c+=SAMPLE_TEXTURE2D_X(_BlitTexture,sampler_PointClamp,uv+d*float2(-1,1))*0.06;
                c+=SAMPLE_TEXTURE2D_X(_BlitTexture,sampler_PointClamp,uv+d*float2(1,-1))*0.06;
                return lerp(center,c,amount*.5);

            }
            ENDHLSL
        }
    }
}
