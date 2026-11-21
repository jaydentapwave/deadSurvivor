Shader "DeadSurvivor/DQ/Leaves"
{
 Properties { _BaseMap("Hand painted leaves",2D)="white"{} _BaseColor("Tint",Color)=(.035,.55,.32,1) _Mask("Diagnostic mask",Float)=0 _Grass("Grass card",Float)=0 }
 SubShader
 {
  Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="TransparentCutout" "Queue"="AlphaTest" }
  Cull Off
  HLSLINCLUDE
  #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
  #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
  TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
  CBUFFER_START(UnityPerMaterial)
  float4 _BaseColor;float _Mask;float _Grass;
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
   half4 Frag(V i):SV_Target
   {
    half4 t=SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,i.uv);clip(t.a-.42);
    Light sun=GetMainLight();
    float lighting=pow(saturate(dot(normalize(i.normal),sun.direction)*.85+.08),2.4);
    float3 color=float3(.0007+.11*pow(lighting,4),.04+.35*lighting,.025+.04*lighting);
    if (_Grass > .5) color=lerp(float3(.015,.14,.025),float3(.08,.40,.04),sqrt(lighting));
    // The atlas supplies leaf shapes and gentle veins, not high contrast dirt/noise.
    color*=lerp(.94,1.08,t.g)*i.color.rgb*sun.color;
    if (_Mask > .5) return 1;
    return half4(lerp(color,float3(.188,.463,.67),smoothstep(14,34,i.world.z)*.74),1);
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
