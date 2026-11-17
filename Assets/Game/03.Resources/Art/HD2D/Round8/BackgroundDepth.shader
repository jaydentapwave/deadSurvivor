Shader "HD2D/Round8/BackgroundDepth"
{
    SubShader
    {
        Pass
        {
            Cull Off ZTest Always ZWrite Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
            float Frag(Varyings i):SV_Target {return SAMPLE_TEXTURE2D_X(_BlitTexture,sampler_PointClamp,i.texcoord).r;}
            ENDHLSL
        }
    }
}
