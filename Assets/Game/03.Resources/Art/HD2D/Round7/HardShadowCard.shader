Shader "HD2D/Round7/HardShadowCard"
{
    Properties {[PerRendererData] _MainTex("Shadow mask",2D)="white" {}}
    SubShader
    {
        Tags {"RenderPipeline"="UniversalPipeline" "Queue"="Transparent"}
        Pass
        {
            Cull Off ZWrite Off Blend DstColor Zero
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_MainTex);SAMPLER(sampler_MainTex);
            struct A {float3 p:POSITION;float2 uv:TEXCOORD0;};struct V {float4 p:SV_POSITION;float2 uv:TEXCOORD0;};
            V Vert(A i){V o;o.p=TransformObjectToHClip(i.p);o.uv=i.uv;return o;}
            half4 Frag(V i):SV_Target {clip(SAMPLE_TEXTURE2D(_MainTex,sampler_PointClamp,i.uv).a-.5);return half4(.45,.4,.34,1);}
            ENDHLSL
        }
    }
}
