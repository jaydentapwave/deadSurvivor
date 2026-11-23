Shader "DeadSurvivor/DQ/Main water"
{
    Properties
    {
        _WaterMask("Water opening",2D)="black"{}
        _DiagnosticMask("Measurement mask only",Float)=0
        _FieldRect("Field rectangle",Vector)=(-36,-36,72,72)
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
        ZWrite Off ZTest LEqual Cull Off Blend SrcAlpha OneMinusSrcAlpha
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_local _ _TERRAIN_HEIGHT
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_WaterMask); SAMPLER(sampler_WaterMask);
            CBUFFER_START(UnityPerMaterial)
            float4 _FieldRect;
            float4 _WaterMask_TexelSize; float _DiagnosticMask;
            CBUFFER_END
            struct A { float3 p:POSITION; };
            struct V { float4 p:SV_POSITION; float3 w:TEXCOORD0; };
            V Vert(A a) { V o; o.w=TransformObjectToWorld(a.p);o.p=TransformWorldToHClip(o.w);return o; }
            half4 Frag(V i):SV_Target
            {
                float2 uv=(i.w.xz-_FieldRect.xy)/_FieldRect.zw;
                float opening=SAMPLE_TEXTURE2D(_WaterMask,sampler_WaterMask,uv).r;
                clip(opening-.05);
                float ripple=pow(saturate(sin(i.w.z*20+sin(i.w.x*3)+_Time.y*1.2)*.5+.5),12);
                float2 width=float2(.16,.16)/_FieldRect.zw;
                float shore=1-min(min(SAMPLE_TEXTURE2D(_WaterMask,sampler_WaterMask,uv+float2(width.x,0)).r,SAMPLE_TEXTURE2D(_WaterMask,sampler_WaterMask,uv-float2(width.x,0)).r),
                    min(SAMPLE_TEXTURE2D(_WaterMask,sampler_WaterMask,uv+float2(0,width.y)).r,SAMPLE_TEXTURE2D(_WaterMask,sampler_WaterMask,uv-float2(0,width.y)).r));
                float foam=shore*(.45+.15*sin(i.w.z*25+i.w.x*11));
                float2 cell=floor(i.w.xz*2.7), p=frac(i.w.xz*2.7)-.5;
                float star=step(.96,frac(sin(dot(cell,float2(12.7,31.7)))*43758.5));
                star*=saturate(1-abs(p.x)*35-abs(p.y)*6)+saturate(1-abs(p.y)*35-abs(p.x)*6);
                star*=pow(saturate(sin(_Time.y*2+cell.x)*.5+.5),3);
                half3 water=half3(.065,.39,.44)+ripple*half3(.015,.045,.05);
                water=lerp(water,half3(.72,.86,.80),foam);
                float alpha=smoothstep(.05,.4,opening)*.78;
                if (_DiagnosticMask>.5) return half4(0,0,0,alpha);
                return half4(water+star*1.6,alpha);
            }
            ENDHLSL
        }
    }
}
