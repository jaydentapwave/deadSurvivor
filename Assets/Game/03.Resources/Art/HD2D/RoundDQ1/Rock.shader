Shader "DeadSurvivor/DQ/Faceted rock"
{
 SubShader
 {
  Tags {"RenderPipeline"="UniversalPipeline" "RenderType"="Opaque"}
  Pass
  {
   Tags {"LightMode"="UniversalForward"}
   HLSLPROGRAM
   #pragma vertex Vert
   #pragma fragment Frag
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
   struct A {float4 positionOS:POSITION;float3 normalOS:NORMAL;};
   struct V {float4 positionCS:SV_POSITION;float3 normal:TEXCOORD0;};
   V Vert(A a){V o;o.positionCS=TransformObjectToHClip(a.positionOS.xyz);o.normal=TransformObjectToWorldNormal(a.normalOS);return o;}
   half4 Frag(V i):SV_Target {float l=saturate(dot(normalize(i.normal),normalize(float3(-.48,.8,-.4))));float3 c=lerp(float3(.038,.07,.053),float3(.22,.28,.19),l);return half4(c,1);}
   ENDHLSL
  }
 }
}
