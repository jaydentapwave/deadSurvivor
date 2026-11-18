// Native painted background lighting from Assets/Game/03.Resources/Art/HD2D/PixelSpriteLit.shader SHA256 62b98c9663935462ee12f2a7dcc7480d4adbe6ce83e7129eee469e6e43d3e85e
// Source: Assets/Game/03.Resources/Art/HD2D/Round8/StableSpriteLit.shader SHA256 f0701ac97deae4f6d041567695cb83ca97e93db13d4a0513be14b8061c784a1f
Shader "HD2D/HD2D_SpriteLit"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite", 2D) = "white" {}
        _ReceiveShadows ("Receive shadows", Float) = 1
        _SpriteRenderer ("SpriteRenderer data", Float) = 1
        _BaseColor ("Tint", Color) = (1,1,1,1)
        _Emission ("Emission", Range(0,4)) = 0
        _BackgroundCard ("Native painted background card", Float) = 0
        _Dither ("Dither amount", Range(0,1)) = 0
        _HueShift ("Palette hue shift", Range(0,1)) = 0
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="TransparentCutout" "Queue"="AlphaTest" }
        Cull Off
        ZWrite On
        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
        TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
        CBUFFER_START(UnityPerMaterial)
        half4 _BaseColor;
        float _Emission;
        float _Dither;
        int _OcclusionRectCount;
        float4 _OcclusionRects[64];
        float _BackgroundCard;
        float _HueShift;
        float _SpriteRenderer;
        float _ReceiveShadows;
        CBUFFER_END
        struct Attributes
        {
            float3 positionOS : POSITION;
            float2 uv : TEXCOORD0;
            half4 color : COLOR;
            UNITY_VERTEX_INPUT_INSTANCE_ID
        };
        struct Varyings
        {
            float4 positionCS : SV_POSITION;
            float2 uv : TEXCOORD0;
            float3 positionWS : TEXCOORD1;
            half4 color : COLOR;
            float4 screen : TEXCOORD2;
        };
        // Same flip and alpha in all three passes, including the shadow silhouette.
        Varyings SpriteVertex(Attributes v)
        {
            UNITY_SETUP_INSTANCE_ID(v);
            Varyings o;
            float3 p = float3(v.positionOS.xy * lerp(float2(1,1), unity_SpriteProps.xy, _SpriteRenderer), v.positionOS.z);
            o.positionWS = TransformObjectToWorld(p);
            o.positionCS = TransformWorldToHClip(o.positionWS);
            o.screen = ComputeScreenPos(o.positionCS);
            o.uv = v.uv;
            o.color = v.color * lerp(half4(1,1,1,1), unity_SpriteColor, _SpriteRenderer) * _BaseColor;
            return o;
        }
        half4 SpriteSample(Varyings i)
        {
            half4 c = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv + float2(0.00001, 0.00001)) * i.color;
            clip(c.a - 0.5);
            if (_Dither > .001)
            {
                float2 uv=i.screen.xy/i.screen.w;
                for (int index=0;index<_OcclusionRectCount;index++)
                {
                float4 window=_OcclusionRects[index];
                float2 nearEdge=min(uv-window.xy,window.zw-uv)*_ScaledScreenParams.xy;
                float border=min(nearEdge.x,nearEdge.y);
                if (border>0) clip(frac(dot(floor(i.positionCS.xy),float2(.754877666,.569840296)))-_Dither*saturate(border/2));
                }
            }
            return c;
        }
        half4 LitFragment(Varyings i) : SV_Target
        {
            half4 c = SpriteSample(i);
            float4 k = float4(0,-1.0/3.0,2.0/3.0,-1);
            float4 p = lerp(float4(c.bg,k.wz),float4(c.gb,k.xy),step(c.b,c.g));
            float4 q = lerp(float4(p.xyw,c.r),float4(c.r,p.yzx),step(p.x,c.r));
            float d = q.x - min(q.w,q.y);
            float3 hsv = float3(abs(q.z+(q.w-q.y)/(6*d+1e-6)),d/(q.x+1e-6),q.x);
            float3 ramp = abs(frac(frac(hsv.x+_HueShift)+float3(0,2.0/3.0,1.0/3.0))*6-3);
            c.rgb = hsv.z*lerp(float3(1,1,1),saturate(ramp-1),hsv.y);
            Light sun = GetMainLight(TransformWorldToShadowCoord(i.positionWS));
            half diffuse = saturate(dot(normalize(float3(0,0.85,-0.53)),sun.direction));
            half3 ambient = half3(0.43,0.32,0.27);
            diffuse = lerp(diffuse < .25 ? .58 : (diffuse < .68 ? .80 : 1.0), diffuse, _BackgroundCard);
            return half4(c.rgb * (ambient + sun.color * diffuse * lerp(1,step(.5,sun.shadowAttenuation),_ReceiveShadows)) + c.rgb * _Emission,1);
        }
        float3 _LightDirection;
        Varyings ShadowVertex(Attributes v)
        {
            Varyings o = SpriteVertex(v);
            o.positionCS = TransformWorldToHClip(ApplyShadowBias(o.positionWS,float3(0,1,0),_LightDirection));
            o.positionCS = ApplyShadowClamping(o.positionCS);
            return o;
        }
        half4 DepthFragment(Varyings i) : SV_Target
        {
            SpriteSample(i);
            return 0;
        }
        ENDHLSL
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }
            HLSLPROGRAM
            #pragma vertex SpriteVertex
            #pragma fragment LitFragment
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            ENDHLSL
        }
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode"="ShadowCaster" }
            ColorMask 0
            HLSLPROGRAM
            #pragma vertex ShadowVertex
            #pragma fragment DepthFragment
            ENDHLSL
        }
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode"="DepthOnly" }
            ColorMask R
            HLSLPROGRAM
            #pragma vertex SpriteVertex
            #pragma fragment DepthFragment
            ENDHLSL
        }
    }
}
