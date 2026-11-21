Shader "DeadSurvivor/DQ/Leaves"
{
 Properties { _BaseMap("Hand painted leaves",2D)="white"{} _BaseColor("Tint",Color)=(.025,.28,.13,1) _Mask("Diagnostic mask",Float)=0 }
 SubShader
 {
  Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="TransparentCutout" "Queue"="AlphaTest" }
  Cull Off
  HLSLINCLUDE
  #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
  #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
  TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
  CBUFFER_START(UnityPerMaterial)
  float4 _BaseColor;float _Mask;
  CBUFFER_END
  struct A { float4 positionOS:POSITION; float3 normalOS:NORMAL;float2 uv:TEXCOORD0;float4 color:COLOR; };
  struct V { float4 positionCS:SV_POSITION;float2 uv:TEXCOORD0;float3 world:TEXCOORD1;float3 normal:TEXCOORD2;float4 color:COLOR; };
  V Vert(A a) { V o;o.world=TransformObjectToWorld(a.positionOS.xyz);o.positionCS=TransformWorldToHClip(o.world);o.uv=a.uv;o.normal=TransformObjectToWorldNormal(a.normalOS);o.color=a.color;return o; }
  ENDHLSL
  Pass
  {
   Tags { "LightMode"="UniversalForward" }
   HLSLPROGRAM
   #pragma vertex Vert
   #pragma fragment Frag
   #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
   #pragma multi_compile_fragment _ _SHADOWS_SOFT
   half4 Frag(V i):SV_Target
   {
    half4 t=SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,i.uv);clip(t.a-.42);
    Light l=GetMainLight(TransformWorldToShadowCoord(i.world));
    float lighting=.24+.76*saturate(dot(normalize(i.normal),l.direction)) * l.shadowAttenuation;
    float3 color=_BaseColor.rgb*t.rgb*lighting*i.color.rgb;
    if (_Mask > .5) return 1;
    return half4(lerp(color,float3(.188,.463,.67),smoothstep(45,100,distance(i.world,_WorldSpaceCameraPos))*.65),1);
   }
   ENDHLSL
  }
  Pass
  {
   Tags { "LightMode"="ShadowCaster" }
   ZWrite On ZTest LEqual ColorMask 0
   HLSLPROGRAM
   #pragma vertex Vert
   #pragma fragment Shadow
   half4 Shadow(V i):SV_Target { clip(SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,i.uv).a-.42);return 0; }
   ENDHLSL
  }
 }
}
