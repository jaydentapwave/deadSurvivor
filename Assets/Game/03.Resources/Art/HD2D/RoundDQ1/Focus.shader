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
    // Optical blur: a Gaussian footprint instead of sparse duplicate edges.
    float sigma=1.85*smoothstep(.80,1,uv.y)+.45*(1-smoothstep(0,.13,uv.y));
    if (sigma < .02) return SAMPLE_TEXTURE2D_X(_BlitTexture,sampler_LinearClamp,uv);
    half4 color=0;float weight=0;
    [unroll] for(int y=-3;y<=3;y++)
    {
     [unroll] for(int x=-3;x<=3;x++)
     {
      float w=exp(-(x*x+y*y)/4.5);
      color+=SAMPLE_TEXTURE2D_X(_BlitTexture,sampler_LinearClamp,uv+float2(x,y)*_BlitTexture_TexelSize.xy*sigma)*w;
      weight+=w;
     }
    }
    return color/weight;
   }
   ENDHLSL
  }
 }
}
