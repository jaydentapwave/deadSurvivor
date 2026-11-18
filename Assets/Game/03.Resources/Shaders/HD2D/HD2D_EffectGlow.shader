Shader "HD2D/HD2D_EffectGlow"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite", 2D) = "white" {}
        _CoreIntensity ("Core intensity", Float) = 2.4
        _BodyIntensity ("Body intensity", Float) = 1
        [Enum(Off,0,On,1)] _ZWrite ("Depth write", Float) = 1
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="AlphaTest" "RenderType"="TransparentCutout" }
        Pass
        {
            Tags { "LightMode"="SRPDefaultUnlit" }
            Cull Off
            ZWrite [_ZWrite]
            ZTest LEqual
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
            CBUFFER_START(UnityPerMaterial)
                float _CoreIntensity;
                float _BodyIntensity;
            CBUFFER_END
            struct A { float3 positionOS:POSITION; float2 uv:TEXCOORD0; half4 color:COLOR; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct V { float4 positionCS:SV_POSITION; float2 uv:TEXCOORD0; half4 color:COLOR; };
            V Vert(A v)
            {
                UNITY_SETUP_INSTANCE_ID(v);
                V o;
                o.positionCS = TransformObjectToHClip(float3(v.positionOS.xy * unity_SpriteProps.xy, v.positionOS.z));
                o.uv = v.uv;
                o.color = v.color * unity_SpriteColor;
                return o;
            }
            half4 Frag(V i):SV_Target
            {
                half4 c = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv) * i.color;
                clip(c.a - 0.5);
                // 기준서 문턱은 PNG의 sRGB 팔레트 기준이다.
                float3 srgb = c.rgb;
                #ifndef UNITY_COLORSPACE_GAMMA
                    srgb = LinearToSRGB(srgb);
                #endif
                float hi = max(srgb.r, max(srgb.g, srgb.b));
                float lo = min(srgb.r, min(srgb.g, srgb.b));
                float saturation = (hi - lo) / max(hi, 0.0001);
                float intensity = hi >= 0.96 && saturation < 0.12 ? _CoreIntensity : _BodyIntensity;
                return half4(c.rgb * intensity, 1);
            }
            ENDHLSL
        }
    }
}
