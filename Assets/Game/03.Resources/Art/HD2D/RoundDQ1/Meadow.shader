Shader "DeadSurvivor/DQ/Meadow"
{
 Properties { _BaseMap("Velvet grass",2D)="white"{} _BaseColor("Grass",Color)=(0.03,0.38,0.06,1) }
 SubShader
 {
  Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" }
  Pass
  {
   Tags { "LightMode"="UniversalForward" }
   HLSLPROGRAM
   #pragma vertex Vert
   #pragma fragment Frag
   #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
   #pragma multi_compile_fragment _ _SHADOWS_SOFT
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
   TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
   CBUFFER_START(UnityPerMaterial)
   float4 _BaseColor;
   CBUFFER_END
   struct A { float4 positionOS:POSITION; float3 normalOS:NORMAL; };
   struct V { float4 positionCS:SV_POSITION; float3 world:TEXCOORD0; float3 normal:TEXCOORD1; };
   V Vert(A a) { V o; o.world=TransformObjectToWorld(a.positionOS.xyz);o.positionCS=TransformWorldToHClip(o.world);o.normal=TransformObjectToWorldNormal(a.normalOS);return o; }
   half4 Frag(V i):SV_Target
   {
    Light l=GetMainLight(TransformWorldToShadowCoord(i.world));
    float shade=lerp(0.075,1,l.shadowAttenuation)*(.18+.94*saturate(dot(normalize(i.normal),l.direction)));
    float grain=SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,i.world.xz/8).r;
    float macro=1+.06*sin(i.world.x*.37+i.world.z*.23)+.045*sin(i.world.z*.8-i.world.x*.25);
    float pathX=3.4+1.7*sin(i.world.z*.13);
    float edge=.16*sin(i.world.z*5)+.1*sin(i.world.z*11);
    float road=1-smoothstep(.53+edge,1.15+edge,abs(i.world.x-pathX));
    float3 albedo=lerp(_BaseColor.rgb*(.90+.2*grain)*macro,float3(.578,.467,.246),road);
    float3 color=albedo*shade;
    float haze=smoothstep(45,110,distance(i.world,_WorldSpaceCameraPos))*.7;
    return half4(lerp(color,float3(.188,.463,.67),haze),1);
   }
   ENDHLSL
  }
 }
}
