Shader "HD2D/HD2D_AmbientDust"
{
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
        Pass
        {
            Tags { "LightMode"="SRPDefaultUnlit" }
            Cull Off ZWrite Off Blend SrcAlpha OneMinusSrcAlpha
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct A { float3 p:POSITION; half4 c:COLOR; };
            struct V { float4 p:SV_POSITION; half4 c:COLOR; };
            V Vert(A i) { V o; o.p=TransformObjectToHClip(i.p); o.c=i.c; return o; }
            half4 Frag(V i):SV_Target { return i.c; }
            ENDHLSL
        }
    }
}
