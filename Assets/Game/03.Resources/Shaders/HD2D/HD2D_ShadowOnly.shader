Shader "HD2D/HD2D_ShadowOnly"
{
 Properties { [PerRendererData] _MainTex ("Sprite", 2D) = "white" {} }
 SubShader {
 Tags { "RenderPipeline"="UniversalPipeline" "Queue"="AlphaTest" }
 Pass {
 Tags { "LightMode"="ShadowCaster" }
 Cull Off ZWrite On ZTest LEqual
  ColorMask 0
 HLSLPROGRAM
 #pragma vertex Vert
 #pragma fragment Frag
 #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
 TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
 struct A { float3 positionOS:POSITION; float2 uv:TEXCOORD0; half4 color:COLOR; UNITY_VERTEX_INPUT_INSTANCE_ID };
 struct V { float4 positionCS:SV_POSITION; float2 uv:TEXCOORD0; half4 color:COLOR; };
 V Vert(A v) { UNITY_SETUP_INSTANCE_ID(v); V o; float3 p=float3(v.positionOS.xy*unity_SpriteProps.xy,v.positionOS.z); o.positionCS=TransformObjectToHClip(p); o.uv=v.uv; o.color=v.color*unity_SpriteColor; return o; }
 half4 Frag(V i):SV_Target { half4 c=SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex,i.uv)*i.color; clip(c.a - 0.5); return c; }
 ENDHLSL
 } }
}
