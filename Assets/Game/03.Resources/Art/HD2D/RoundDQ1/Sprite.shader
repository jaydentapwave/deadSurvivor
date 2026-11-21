Shader "DeadSurvivor/DQ/Sprite"
{
 Properties { [PerRendererData] _MainTex("Sprite",2D)="white"{} _Color("Tint",Color)=(1,1,1,1) _Mask("Diagnostic mask",Float)=0 }
 SubShader
 {
  Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
  Cull Off ZWrite Off Blend SrcAlpha OneMinusSrcAlpha
  Pass
  {
   Tags { "LightMode"="SRPDefaultUnlit" }
   HLSLPROGRAM
   #pragma vertex Vert
   #pragma fragment Frag
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
   TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
   float4 _Color;float _Mask;
   struct A { float4 positionOS:POSITION;float2 uv:TEXCOORD0;float4 color:COLOR; };
   struct V { float4 positionCS:SV_POSITION;float2 uv:TEXCOORD0;float4 color:COLOR; };
   V Vert(A a) { V o;o.positionCS=TransformObjectToHClip(a.positionOS.xyz);o.uv=a.uv;o.color=a.color*_Color;return o; }
   half4 Frag(V i):SV_Target {half4 c=SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex,i.uv);c.rgb=lerp(c.rgb,1,_Mask);return c*i.color;}
   ENDHLSL
  }
 }
}
