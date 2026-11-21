Shader "DeadSurvivor/DQ/Meadow"
{
 Properties { _BaseMap("Velvet grass",2D)="white"{} _BaseColor("Grass",Color)=(.13,.71,.29,1) }
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
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
   TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
   CBUFFER_START(UnityPerMaterial)
   float4 _BaseColor;
   CBUFFER_END
   struct A { float4 positionOS:POSITION; float3 normalOS:NORMAL; };
   struct V { float4 positionCS:SV_POSITION; float3 world:TEXCOORD0; float3 normal:TEXCOORD1; };
   V Vert(A a) { V o; o.world=TransformObjectToWorld(a.positionOS.xyz);o.positionCS=TransformWorldToHClip(o.world);o.normal=TransformObjectToWorldNormal(a.normalOS);return o; }
   float Hash(float2 p) {return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453);}
   float Noise(float2 p) {float2 a=floor(p),b=frac(p);b=b*b*(3-2*b);return lerp(lerp(Hash(a),Hash(a+float2(1,0)),b.x),lerp(Hash(a+float2(0,1)),Hash(a+1),b.x),b.y);}
   half4 Frag(V i):SV_Target
   {
    Light sun=GetMainLight(TransformWorldToShadowCoord(i.world));
    // Amplify only the lighting normal. The walkable mesh remains gently rolling.
    float3 n=normalize(float3(i.normal.x*8,i.normal.y,i.normal.z*8));
    float lit=smoothstep(.08,.92,dot(n,sun.direction));
    float3 grass=lerp(float3(.004,.105,.021),float3(.018,.47,.05),lit);
    float broad=Noise(i.world.xz*.18)*.15+Noise(i.world.xz*.055+14)*.12;
    grass*=.84+broad;
    float grain=SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,i.world.xz/8).r;
    grass*=.67+1.55*grain;
    // Low amplitude velvet detail, continuous and irregular rather than a pixel lattice.
    grass*=.98+.04*Noise(i.world.xz*72);
    float roadX=3.4+1.7*sin(i.world.z*.13);
    float ragged=(Noise(i.world.xz*3.1)-.5)*.3+(Noise(i.world.xz*13)-.5)*.1;
    float road=1-smoothstep(.55,.98,abs(i.world.x-roadX)+ragged);
    float soilGrain=Noise(i.world.xz*16)*.055+Noise(i.world.xz*1.7)*.06;
    float3 soil=float3(.58,.47,.27)*(.88+soilGrain)*lerp(.48,1,lit);
    float3 color=lerp(grass,soil,road);
    float pondRadius=length((i.world.xz-float2(-8,1))/float2(2.5,4.5));
    float bank=1-smoothstep(1.02,1.28,pondRadius+(Noise(i.world.xz*4)-.5)*.08);
    float wet=saturate((1.12-pondRadius)*2.4);
    float3 bankColor=lerp(float3(.34,.28,.13),float3(.055,.085,.045),wet);
    color=lerp(color,bankColor*lerp(.6,1,lit),bank);
    color*=sun.color*lerp(.17,1,sun.shadowAttenuation);
    float haze=smoothstep(14,34,i.world.z)*.74;
    return half4(lerp(color,float3(.188,.463,.67),haze),1);
   }
   ENDHLSL
  }
 }
}
