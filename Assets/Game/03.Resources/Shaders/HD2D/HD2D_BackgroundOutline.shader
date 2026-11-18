// Source: Assets/Game/03.Resources/Art/HD2D/Round8/BackgroundOutline.shader SHA256 71c43215fb8b6ea863a372045acda4c7a8fd9885f28854cee27eea1edf8e843c
Shader "HD2D/HD2D_BackgroundOutline"
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
            TEXTURE2D(_BackgroundDepth);SAMPLER(sampler_BackgroundDepth);
            TEXTURE2D(_BackgroundNormals);SAMPLER(sampler_BackgroundNormals);
            float4 _BackgroundDepth_TexelSize;
            float Eye(float raw)
            {
                #if UNITY_REVERSED_Z
                return lerp(_ProjectionParams.z,_ProjectionParams.y,raw);
                #else
                return lerp(_ProjectionParams.y,_ProjectionParams.z,raw);
                #endif
            }
            half4 Frag(Varyings i):SV_Target
            {
                float2 uv=i.texcoord;
                float raw=SAMPLE_TEXTURE2D(_BackgroundDepth,sampler_PointClamp,uv).r;
                float center=Eye(raw);
                float edge=0;
                const float2 offsets[4]={float2(1,0),float2(-1,0),float2(0,1),float2(0,-1)};
                for(int k=0;k<4;k++)
                {
                    float2 p=uv+offsets[k]*_BackgroundDepth_TexelSize.xy;
                    float depth=Eye(SAMPLE_TEXTURE2D(_BackgroundDepth,sampler_PointClamp,p).r);
                    // Draw on the near side only: one low-resolution pixel, not two.
                    edge=max(edge,(depth-center>.055) ? 1 : 0);
                }
                half4 color=SAMPLE_TEXTURE2D_X(_BlitTexture,sampler_PointClamp,uv);
                color.rgb=lerp(color.rgb,SRGBToLinear(half3(52,20,14)/255),edge);
                return color;
            }
            ENDHLSL
        }
    }
}
