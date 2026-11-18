// Source: Assets/Game/03.Resources/Art/HD2D/Round8/BackgroundDepth.shader SHA256 8ed58d4d50a92eb15f6dd6e32da748edbf014e9e5d5b27da0ef2d452aaff751f
Shader "HD2D/HD2D_BackgroundDepth"
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
