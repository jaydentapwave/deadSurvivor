Shader "HD2D/HD2D_ShadowOnly"
{
    Properties { [PerRendererData] _MainTex ("Sprite", 2D) = "white" {} }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="AlphaTest" }
        Pass
        {
            Tags { "LightMode"="ShadowCaster" }
            Cull Off ZWrite On ZTest LEqual ColorMask 0
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"
            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
            float3 _LightDirection;
            float3 _LightPosition;
            struct Attributes { float3 positionOS:POSITION; float2 uv:TEXCOORD0; };
            struct Varyings { float4 positionCS:SV_POSITION; float2 uv:TEXCOORD0; };
            Varyings Vert(Attributes input)
            {
                Varyings output;
                float3 positionWS = TransformObjectToWorld(input.positionOS);
                float3 normalWS = TransformObjectToWorldNormal(float3(0,0,-1));
                #if _CASTING_PUNCTUAL_LIGHT_SHADOW
                float3 direction = normalize(_LightPosition-positionWS);
                #else
                float3 direction = _LightDirection;
                #endif
                output.positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS,normalWS,direction));
                #if UNITY_REVERSED_Z
                output.positionCS.z = min(output.positionCS.z, UNITY_NEAR_CLIP_VALUE);
                #else
                output.positionCS.z = max(output.positionCS.z, UNITY_NEAR_CLIP_VALUE);
                #endif
                output.uv = input.uv;
                return output;
            }
            half4 Frag(Varyings input):SV_Target
            {
                clip(SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex,input.uv).a-.5);
                return 0;
            }
            ENDHLSL
        }
    }
}
