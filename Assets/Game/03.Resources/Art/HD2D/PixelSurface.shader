Shader "HD2D/PixelSurface"
{
    Properties
    {
        _MainTex ("Point texture", 2D) = "white" {}
        _WorldTex ("World projected pixel texture", 2D) = "white" {}
        _BaseColor ("Color", Color) = (1,1,1,1)
        _WorldTexture ("Triplanar pixel variation", Float) = 0
        _GroundTexture ("48 texels per unit ground", Float) = 0
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" }
        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
        TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
        TEXTURE2D(_WorldTex); SAMPLER(sampler_WorldTex);
        CBUFFER_START(UnityPerMaterial)
        half4 _BaseColor;
        float _WorldTexture;
        float _GroundTexture;
        float4 _MainTex_ST;
        CBUFFER_END
        struct A { float3 p:POSITION; float3 n:NORMAL; float2 uv:TEXCOORD0; };
        struct V { float4 p:SV_POSITION; float3 w:TEXCOORD0; float3 n:TEXCOORD1; float2 uv:TEXCOORD2; };
        V Vert(A i)
        {
            V o; o.w=TransformObjectToWorld(i.p); o.p=TransformWorldToHClip(o.w);
            o.n=TransformObjectToWorldNormal(i.n); o.uv=i.uv; return o;
        }
        half4 Frag(V i):SV_Target
        {
            float2 uv=lerp(i.uv,i.w.xz,_GroundTexture);
            half3 c=SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex,uv).rgb*_BaseColor.rgb;
            float3 weights=abs(normalize(i.n)); weights/=dot(weights,float3(1,1,1));
            float3 q=floor(i.w*48)/48;
            half3 projected=SAMPLE_TEXTURE2D(_WorldTex,sampler_WorldTex,q.yz).rgb*weights.x;
            projected+=SAMPLE_TEXTURE2D(_WorldTex,sampler_WorldTex,q.xz).rgb*weights.y;
            projected+=SAMPLE_TEXTURE2D(_WorldTex,sampler_WorldTex,q.xy).rgb*weights.z;
            c*=lerp(1,0.85+projected*0.3,_WorldTexture);
            Light sun=GetMainLight(TransformWorldToShadowCoord(i.w));
            half d=saturate(dot(normalize(i.n),sun.direction));
            return half4(c*(half3(0.43,0.32,0.27)+sun.color*d*sun.shadowAttenuation),1);
        }
        ENDHLSL
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            ENDHLSL
        }
        UsePass "Universal Render Pipeline/Lit/ShadowCaster"
        UsePass "Universal Render Pipeline/Lit/DepthOnly"
    }
}
