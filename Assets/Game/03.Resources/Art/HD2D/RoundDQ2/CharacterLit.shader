Shader "DeadSurvivor/DQ2/Character Lit"
{
    Properties
    {
        [PerRendererData] _MainTex("Sprite", 2D) = "white" {}
        _Color("Tint", Color) = (1,1,1,1)
        _CanopyMap("Existing scene canopy transmission", 2D) = "white" {}
        _Exposure("Light scale (no brightening above source)", Float) = 1
        _EdgeWidth("Cell boundary blend width", Range(0.01,1)) = 0.10
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
        Cull Off ZWrite Off Blend SrcAlpha OneMinusSrcAlpha
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
            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
            TEXTURE2D(_CanopyMap); SAMPLER(sampler_CanopyMap);
            float4 _MainTex_TexelSize;
            float4 _Color;
            float _Exposure, _EdgeWidth;
            struct A { float4 positionOS:POSITION; float2 uv:TEXCOORD0; float4 color:COLOR; };
            struct V { float4 positionCS:SV_POSITION; float2 uv:TEXCOORD0; float4 color:COLOR; float3 world:TEXCOORD1; float3 foot:TEXCOORD2; };
            V Vert(A a)
            {
                V o;
                o.world=TransformObjectToWorld(a.positionOS.xyz);
                o.positionCS=TransformWorldToHClip(o.world);
                o.foot=TransformObjectToWorld(float3(0,0,0));
                o.uv=a.uv; o.color=a.color*_Color;
                return o;
            }
            float4 Premult(float2 uv)
            {
                float4 c=SAMPLE_TEXTURE2D_LOD(_MainTex,sampler_MainTex,uv,0);
                c.rgb*=c.a;
                return c;
            }
            half4 Frag(V i):SV_Target
            {
                // Sample four POINT-filtered cell centres. Blend only a narrow cell boundary band.
                float2 p=i.uv*_MainTex_TexelSize.zw-.5;
                float2 base=(floor(p)+.5)*_MainTex_TexelSize.xy;
                float2 w=smoothstep(.5-_EdgeWidth*.5,.5+_EdgeWidth*.5,frac(p));
                float4 q0=Premult(base),q1=Premult(base+float2(_MainTex_TexelSize.x,0));
                float4 q2=Premult(base+float2(0,_MainTex_TexelSize.y)),q3=Premult(base+_MainTex_TexelSize.xy);
                // Only silhouette cells use a wider coverage band, approximately one screen pixel each side.
                if (min(min(q0.a,q1.a),min(q2.a,q3.a))<max(max(q0.a,q1.a),max(q2.a,q3.a)))
                {
                    w=smoothstep(.14,.86,frac(p));
                }
                float4 a=lerp(q0,q1,w.x);
                float4 b=lerp(q2,q3,w.x);
                float4 c=lerp(a,b,w.y);
                c.rgb/=max(c.a,.0001);
                Light sun=GetMainLight(TransformWorldToShadowCoord(i.world));
                float canopy=SAMPLE_TEXTURE2D(_CanopyMap,sampler_CanopyMap,(i.foot.xz-float2(-70,-35))/140).r;
                float3 ambient=SampleSH(float3(0,1,0));
                // Keep sunlight at the authored color/value range: never lift RGB toward a pale pastel.
                // Scene intensity still controls attenuation, and scene chroma tints rather than adds white.
                float3 luma=float3(.2126,.7152,.0722);
                float visibility=sun.shadowAttenuation*canopy;
                float mainEnergy=dot(sun.color,luma);
                float ambientEnergy=dot(ambient,luma);
                float energy=min(1,(max(.055,ambientEnergy)+mainEnergy*.95*visibility)*_Exposure);
                float3 sunTint=sun.color/max(max(sun.color.r,sun.color.g),max(sun.color.b,.0001));
                float3 ambientTint=ambient/max(max(ambient.r,ambient.g),max(ambient.b,.0001));
                float3 tint=lerp(lerp(1,ambientTint,.12),lerp(1,sunTint,.45),visibility);
                c.rgb*=tint*energy;
                c.rgb=lerp(c.rgb,float3(.188,.463,.67),smoothstep(14,34,i.world.z)*.74);
                return c*i.color;
            }
            ENDHLSL
        }
    }
}
