Shader "DeadSurvivor/DQ/Shallow water"
{
 SubShader
 {
  Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent-20" "RenderType"="Transparent" }
  ZWrite Off Cull Off Blend SrcAlpha OneMinusSrcAlpha
  Pass
  {
   HLSLPROGRAM
   #pragma vertex Vert
   #pragma fragment Frag
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
   struct A { float4 positionOS:POSITION; };
   struct V { float4 positionCS:SV_POSITION;float3 world:TEXCOORD0; };
   V Vert(A a) {V o;o.world=TransformObjectToWorld(a.positionOS.xyz);o.positionCS=TransformWorldToHClip(o.world);return o;}
   float Hash(float2 p) {return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453);}
   float Noise(float2 p) {float2 a=floor(p),b=frac(p);b=b*b*(3-2*b);return lerp(lerp(Hash(a),Hash(a+float2(1,0)),b.x),lerp(Hash(a+float2(0,1)),Hash(a+1),b.x),b.y);}
   half4 Frag(V i):SV_Target
   {
    float2 p=(i.world.xz-float2(-8,1))/float2(2.5,4.5);
    float radius=length(p)+(Noise(i.world.xz*2.1)-.5)*.07;
    float alpha=1-smoothstep(.89,1.06,radius);clip(alpha-.003);
    float depth=1-smoothstep(.15,.98,radius);
    float3 color=lerp(float3(.065,.38,.39),float3(.009,.15,.23),depth*.8);
    float ripple=sin(i.world.z*30+sin(i.world.x*5)*2)*.5+.5;
    color+=float3(.014,.045,.05)*pow(ripple,8)*(.3+.7*Noise(i.world.xz*4));
    float foam=exp(-pow((radius-.91)/.04,2))*(.25+.5*Noise(i.world.xz*8));
    color=lerp(color,float3(.72,.86,.77),foam);
    float2 cell=floor(i.world.xz*3.8);float2 v=frac(i.world.xz*3.8)-.5;
    float shine=step(.90,Hash(cell))*saturate(1-abs(v.x)*35-abs(v.y)*8);
    shine+=step(.90,Hash(cell))*saturate(1-abs(v.y)*35-abs(v.x)*8);
    color+=shine*float3(2.4,2.5,2.15)*smoothstep(.95,.65,radius);
    return half4(color,alpha*lerp(.55,.88,depth));
   }
   ENDHLSL
  }
 }
}
