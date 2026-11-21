Shader "Hidden/DeadSurvivor/DQ/Focus"
{
 SubShader
 {
  Tags { "RenderPipeline"="UniversalPipeline" }
  ZWrite Off Cull Off ZTest Always
  Pass
  {
   HLSLPROGRAM
   #pragma vertex Vert
   #pragma fragment Frag
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
   #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
   half4 Frag(Varyings i):SV_Target
   {
    float2 uv=i.texcoord;
    float radius=lerp(0,3.8,smoothstep(.76,1,uv.y))+1.1*(1-smoothstep(0,.15,uv.y));
    float2 d=_BlitTexture_TexelSize.xy*radius;
    half4 c=SAMPLE_TEXTURE2D_X(_BlitTexture,sampler_LinearClamp,uv)*.2;
    c+=SAMPLE_TEXTURE2D_X(_BlitTexture,sampler_LinearClamp,uv+float2(d.x,0))*.12;
    c+=SAMPLE_TEXTURE2D_X(_BlitTexture,sampler_LinearClamp,uv-float2(d.x,0))*.12;
    c+=SAMPLE_TEXTURE2D_X(_BlitTexture,sampler_LinearClamp,uv+float2(0,d.y))*.12;
    c+=SAMPLE_TEXTURE2D_X(_BlitTexture,sampler_LinearClamp,uv-float2(0,d.y))*.12;
    c+=SAMPLE_TEXTURE2D_X(_BlitTexture,sampler_LinearClamp,uv+d)*.08;
    c+=SAMPLE_TEXTURE2D_X(_BlitTexture,sampler_LinearClamp,uv-d)*.08;
    c+=SAMPLE_TEXTURE2D_X(_BlitTexture,sampler_LinearClamp,uv+float2(d.x,-d.y))*.08;
    c+=SAMPLE_TEXTURE2D_X(_BlitTexture,sampler_LinearClamp,uv+float2(-d.x,d.y))*.08;
    return c;
   }
   ENDHLSL
  }
 }
}
