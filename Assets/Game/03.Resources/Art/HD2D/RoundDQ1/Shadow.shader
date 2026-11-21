Shader "DeadSurvivor/DQ/Projected shade"
{
 Properties { _BaseMap("Soft canopy shadow",2D)="white"{} }
 SubShader
 {
  Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Geometry+20" "RenderType"="Transparent" }
  ZWrite Off Cull Off Blend DstColor Zero
  Pass
  {
   HLSLPROGRAM
   #pragma vertex Vert
   #pragma fragment Frag
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
   TEXTURE2D(_BaseMap);SAMPLER(sampler_BaseMap);
   struct A {float4 positionOS:POSITION;float2 uv:TEXCOORD0;};
   struct V {float4 positionCS:SV_POSITION;float2 uv:TEXCOORD0;};
   V Vert(A a) {V o;o.positionCS=TransformObjectToHClip(a.positionOS.xyz);o.uv=a.uv;return o;}
   half4 Frag(V i):SV_Target {float a=SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,i.uv).a;return half4(lerp(1,.065,a),lerp(1,.065,a),lerp(1,.065,a),1);}
   ENDHLSL
  }
 }
}
