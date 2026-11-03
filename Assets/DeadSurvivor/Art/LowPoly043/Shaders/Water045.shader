// 045 stage 2: faceted low-poly water for the key-art pond (copied into Assets/DeadSurvivor/Scenes/Proto045/Art_s2 by the build tool).
// Opaque, writes depth, receives main-light shadows, casts none. Facets come from the mesh (jittered heights -> flat
// per-triangle normals via screen derivatives) plus a time-varying per-cell normal tilt that moves the sun sparkle.
// Colours are palette cells of the shared 043 palette (row 3 = water).
Shader "DeadSurvivor/Water045"
{
    Properties
    {
        [NoScaleOffset] _Palette ("Palette (8x4)", 2D) = "white" {}
        _DeepCell ("Deep cell (x,y)", Vector) = (1,3,0,0)
        _ShallowCell ("Shallow cell (x,y)", Vector) = (2,3,0,0)
        _FoamCell ("Light cell (x,y)", Vector) = (3,3,0,0)
        _CellScale ("Facet cell size (world)", Float) = 0.45
        _TiltStrength ("Facet tilt", Range(0,1)) = 0.35
        _WaveSpeed ("Tilt speed", Float) = 0.6
        _Sparkle ("Sun sparkle", Range(0,4)) = 1.6
        _SparklePower ("Sparkle sharpness", Range(8,512)) = 160
        _Shade ("Light wrap", Range(0,1)) = 0.5
        _TimeOffset ("Time offset (verification)", Float) = 0
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry" }
        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        TEXTURE2D(_Palette); SAMPLER(sampler_Palette);
        CBUFFER_START(UnityPerMaterial)
            float4 _DeepCell, _ShallowCell, _FoamCell;
            float _CellScale, _TiltStrength, _WaveSpeed, _Sparkle, _SparklePower, _Shade, _TimeOffset;
        CBUFFER_END
        float3 Cell(float4 c) { return SAMPLE_TEXTURE2D_LOD(_Palette, sampler_Palette, float2((c.x + .5) / 8.0, (3.0 - c.y + .5) / 4.0), 0).rgb; }
        float Hash(float2 p) { p = frac(p * float2(123.34, 456.21)); p += dot(p, p + 45.32); return frac(p.x * p.y); }
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }
            ZWrite On
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            struct A { float4 positionOS : POSITION; };
            struct V { float4 positionCS : SV_POSITION; float3 positionWS : TEXCOORD0; float fog : TEXCOORD1; };
            V Vert(A i)
            {
                V o; o.positionWS = TransformObjectToWorld(i.positionOS.xyz); o.positionCS = TransformWorldToHClip(o.positionWS);
                o.fog = ComputeFogFactor(o.positionCS.z); return o;
            }
            half4 Frag(V i) : SV_Target
            {
                float3 n = normalize(cross(ddy(i.positionWS), ddx(i.positionWS)));
                n = n.y < 0 ? -n : n;
                // per-facet randomness: the flat normal is constant on a triangle (jittered mesh heights) -> hash it
                float2 cell = floor(n.xz * 4000.0);
                float t = (_Time.y + _TimeOffset) * _WaveSpeed;
                float h1 = Hash(cell), h2 = Hash(cell + 17.3);
                float3 tilt = float3(sin(t + h1 * 6.283), 0, cos(t * 0.8 + h2 * 6.283)) * _TiltStrength * 0.5;
                n = normalize(n + tilt);
                float mixv = saturate(Hash(cell + 3.1) * 0.6 + n.x * 0.5 + 0.2);
                float3 albedo = lerp(Cell(_DeepCell), Cell(_ShallowCell), mixv);
                Light l = GetMainLight(TransformWorldToShadowCoord(i.positionWS));
                float ndl = saturate((dot(n, l.direction) + _Shade) / (1 + _Shade));
                float3 viewDir = GetWorldSpaceNormalizeViewDir(i.positionWS);
                float spec = pow(saturate(dot(n, normalize(l.direction + viewDir))), _SparklePower) * _Sparkle;
                float3 ambient = SampleSH(float3(0, 1, 0));
                float3 c = albedo * (ambient + l.color * ndl * l.shadowAttenuation) + Cell(_FoamCell) * spec * l.shadowAttenuation;
                c = MixFog(c, i.fog);
                return half4(c, 1);
            }
            ENDHLSL
        }
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode"="DepthOnly" }
            ZWrite On ColorMask R
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            struct A { float4 positionOS : POSITION; };
            struct V { float4 positionCS : SV_POSITION; };
            V Vert(A i) { V o; o.positionCS = TransformObjectToHClip(i.positionOS.xyz); return o; }
            half Frag(V i) : SV_Target { return i.positionCS.z; }
            ENDHLSL
        }
        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode"="DepthNormals" }
            ZWrite On
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            struct A { float4 positionOS : POSITION; };
            struct V { float4 positionCS : SV_POSITION; float3 positionWS : TEXCOORD0; };
            V Vert(A i) { V o; o.positionWS = TransformObjectToWorld(i.positionOS.xyz); o.positionCS = TransformWorldToHClip(o.positionWS); return o; }
            half4 Frag(V i) : SV_Target
            {
                float3 n = normalize(cross(ddy(i.positionWS), ddx(i.positionWS))); n = n.y < 0 ? -n : n;
                return half4(NormalizeNormalPerPixel(n), 0);
            }
            ENDHLSL
        }
    }
    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
