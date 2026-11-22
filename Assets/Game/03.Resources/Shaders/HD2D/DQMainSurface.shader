// DQ continuous production surface. Retains terrain reference depth and actor-window contracts.
Shader "DeadSurvivor/DQ/Main surface"
{
    Properties
    {
        _Ground("Velvet grass",2D)="white" {}
        _Stone("Stone path",2D)="white" {}
        _Water("Original water",2D)="white" {}
        _Pattern("Point minerals",2D)="white" {}
        _Soil("Point soil and stones",2D)="white" {}
        _Category("0 floor, 2 water, 3 bank, 4 wood, 5 UV",Float)=0
        _Dirt("Soil gravel ground",2D)="white" {}
        _MainTex("UV albedo",2D)="white" {}
        _AlbedoTint("UV painted tint",Vector)=(1,1,1,1)
        _PathMask("Game path mask",2D)="black" {}
        _WaterMask("Water opening",2D)="black" {}
        _ZoneMask("R worn, G gravel, B tone, A meadow",2D)="gray" {}
        _FieldRect("Field rectangle",Vector)=(-36,-36,72,72)
        _FlatDepth("Depth at floor",Float)=0
        _Dither("Dither amount",Range(0,1))=0
        _Cull("Cull mode",Float)=2
        _AlphaClip("UV alpha cutout",Float)=0
        _Diagnostic("Lighting factor only",Float)=0
        _TerrainLightSlope("View-only slope light exaggeration",Float)=4
        _FlagWind("Cloth wind",Float)=0
        _DQModel("Model reference height",Float)=0
        _DQBaseHeight("Model floor height",Float)=0
        _LeafDetail("Leaf clusters",Float)=0
        _WorldTexture("World texture instead of UV",Float)=0
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" }
        Cull [_Cull]
        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
        TEXTURE2D(_Ground); SAMPLER(sampler_Ground);
        TEXTURE2D(_Stone); SAMPLER(sampler_Stone);
        TEXTURE2D(_Water); SAMPLER(sampler_Water);
        TEXTURE2D(_Pattern); SAMPLER(sampler_Pattern);
        TEXTURE2D(_Soil); SAMPLER(sampler_Soil);
        TEXTURE2D(_Dirt); SAMPLER(sampler_Dirt);
        TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
        TEXTURE2D(_WaterMask); SAMPLER(sampler_WaterMask);
        TEXTURE2D(_ZoneMask); SAMPLER(sampler_ZoneMask);
        TEXTURE2D(_PathMask); SAMPLER(sampler_PathMask);
        CBUFFER_START(UnityPerMaterial)
        float4 _FieldRect; float _FlatDepth; float _Dither;
        float _Category;
        float _Diagnostic; float _AlphaClip; int _OcclusionRectCount;
        float _TerrainLightSlope; float _FlagWind;
        float _DQModel; float _DQBaseHeight; float _LeafDetail; float _WorldTexture;
        float4 _AlbedoTint;
        float4 _OcclusionRects[64];
        CBUFFER_END
        #if defined(_TERRAIN_HEIGHT)
        struct A { float3 p:POSITION; float3 n:NORMAL; float2 uv:TEXCOORD0; float2 terrain:TEXCOORD1; };
        struct V { float4 p:SV_POSITION; float3 w:TEXCOORD0; float3 n:TEXCOORD1; float2 uv:TEXCOORD2; float2 floorDepth:TEXCOORD3; float4 screen:TEXCOORD4; float href:TEXCOORD5; float leafEdge:TEXCOORD6; };
        #else
        struct A { float3 p:POSITION; float3 n:NORMAL; float2 uv:TEXCOORD0; };
        struct V { float4 p:SV_POSITION; float3 w:TEXCOORD0; float3 n:TEXCOORD1; float2 uv:TEXCOORD2; float2 floorDepth:TEXCOORD3; float4 screen:TEXCOORD4; };
        #endif
        float _HD2DAmbientFrame;
        V Vert(A i)
        {
            if (_FlagWind>.5)
            {
                float wind=_HD2DAmbientFrame<.5 ? 0 : _HD2DAmbientFrame<1.5 ? 1 : -1;
                i.p.x+=wind*floor(i.uv.x*3.999)/48;
            }
        #if defined(_TERRAIN_HEIGHT)
            V o; o.w=TransformObjectToWorld(i.p); o.n=TransformObjectToWorldNormal(i.n); o.p=TransformWorldToHClip(o.w); o.uv=i.uv; o.screen=ComputeScreenPos(o.p); o.href=lerp(i.terrain.x,_DQBaseHeight,_DQModel); o.leafEdge=i.terrain.y; float4 flat=TransformWorldToHClip(float3(o.w.x,o.href,o.w.z)); o.floorDepth=flat.zw; return o;
        #else
            V o; o.w=TransformObjectToWorld(i.p); o.n=TransformObjectToWorldNormal(i.n); o.p=TransformWorldToHClip(o.w); o.uv=i.uv; o.screen=ComputeScreenPos(o.p); float4 flat=TransformWorldToHClip(float3(o.w.x,0,o.w.z)); o.floorDepth=flat.zw; return o;
        #endif
        }
        void ClipOpening(V i)
        {
            if (_AlphaClip > .5) clip(SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex,i.uv).a-.5);
            float waterOpening=SAMPLE_TEXTURE2D(_WaterMask,sampler_WaterMask,(i.w.xz-_FieldRect.xy)/_FieldRect.zw).r;
        #if defined(_TERRAIN_HEIGHT)
            if (_Category < .5 && i.w.y-i.href < .002) clip(.5-waterOpening);
        #else
            if (_Category < .5 && i.w.y < .002) clip(.5-waterOpening);
        #endif
            if (_Category>1.5 && _Category<2.5) clip(waterOpening-.5);
        }
        void ClipSurface(V i)
        {
            if (_Dither > .001)
            {
                float2 uv=i.screen.xy/i.screen.w;
                for (int index=0;index<_OcclusionRectCount;index++)
                {
                float4 window=_OcclusionRects[index];
                float2 nearEdge=min(uv-window.xy,window.zw-uv)*_ScaledScreenParams.xy;
                float border=min(nearEdge.x,nearEdge.y);
                if (border>0)
                {
                    // Fade only the actor window; preserve the scenery silhouette between actors.
                    clip(frac(dot(floor(i.p.xy),float2(.754877666,.569840296)))-_Dither*saturate(border/2));
                }
                }
            }
            ClipOpening(i);
        }
        struct F { half4 color:SV_Target; float depth:SV_Depth; };
        float Hash(float2 p) { return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453); }
        float Noise(float2 p)
        {
            float2 a=floor(p), b=frac(p); b=b*b*(3-2*b);
            return lerp(lerp(Hash(a),Hash(a+float2(1,0)),b.x),lerp(Hash(a+float2(0,1)),Hash(a+1),b.x),b.y);
        }
        F Frag(V i)
        {
            ClipSurface(i);
            F output; output.depth=lerp(i.p.z,i.floorDepth.x/i.floorDepth.y,_FlatDepth);
            float2 fieldUV=(i.w.xz-_FieldRect.xy)/_FieldRect.zw;
            float broad=Noise(i.w.xz*.12)*.13+Noise(i.w.xz*.047+14)*.08;
            half3 grass=half3(.025,.405,.057)*(.89+broad);
            float velvet=SAMPLE_TEXTURE2D(_Ground,sampler_Ground,i.w.xz/8).r;
            grass*=.67+1.55*velvet;
            half3 soil=SAMPLE_TEXTURE2D(_Dirt,sampler_Dirt,i.w.xz/3).rgb;
            float path=SAMPLE_TEXTURE2D(_PathMask,sampler_PathMask,fieldUV).r;
            half4 zone=SAMPLE_TEXTURE2D(_ZoneMask,sampler_ZoneMask,fieldUV);
            half3 stone=SAMPLE_TEXTURE2D(_Stone,sampler_Stone,i.w.xz/3).rgb;
            half3 c=lerp(grass,stone,smoothstep(.10,.85,zone.g));
            c=lerp(c,soil,smoothstep(.05,.95,path));
            if (_Category>2.5 && _Category<3.5)
            {
                c=lerp(soil*.60,grass,smoothstep(.35,.80,i.n.y));
            }
            else if (_Category>3.5 && _Category<4.5)
            {
                c=SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex,float2(i.w.x+i.w.z,i.w.y)*.8).rgb;
            }
            else if (_Category>4.5)
            {
                float2 uv=lerp(i.uv,float2(i.w.x+i.w.z,i.w.y)*.8,_WorldTexture);
                c=SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex,uv).rgb*_AlbedoTint.rgb;
                if (_LeafDetail>.5)
                {
                    float clump=Noise(i.w.xz*7+i.w.y*3);
                    c*=.78+.40*clump;
                    #if defined(_TERRAIN_HEIGHT)
                    if (i.leafEdge>.5) c=half3(.035,.18,.055)*(.8+.4*clump);
                    #endif
                }
            }
            Light sun=GetMainLight(TransformWorldToShadowCoord(i.w));
            float3 normal=normalize(float3(i.n.x*(_Category<.5 ? _TerrainLightSlope : 1),i.n.y,i.n.z*(_Category<.5 ? _TerrainLightSlope : 1)));
            float face=lerp(.55,1,smoothstep(.0,.9,dot(normal,sun.direction)));
            float shade=lerp(.25,1,sun.shadowAttenuation);
            c*=min(face,shade)*sun.color;
            if (_Diagnostic>.5) c=min(face,shade).xxx;
            output.color=half4(c,1);
            return output;
        }
        float3 _LightDirection;
        V Shadow(A i)
        {
            V o=Vert(i);o.p=TransformWorldToHClip(ApplyShadowBias(o.w,normalize(o.n),_LightDirection));o.p=ApplyShadowClamping(o.p);return o;
        }
        float Depth(V i):SV_Depth { ClipSurface(i); return lerp(i.p.z,i.floorDepth.x/i.floorDepth.y,_FlatDepth); }
        // Camera occlusion fading must not stamp its screen-space pattern into the shadow map.
        half4 ShadowDepth(V i):SV_Target { ClipOpening(i); return 0; }
        ENDHLSL
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }
            HLSLPROGRAM
            #pragma multi_compile_local _ _TERRAIN_HEIGHT
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            ENDHLSL
        }
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode"="ShadowCaster" }
            ColorMask 0
            HLSLPROGRAM
            #pragma multi_compile_local _ _TERRAIN_HEIGHT
            #pragma vertex Shadow
            #pragma fragment ShadowDepth
            ENDHLSL
        }
        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode"="DepthNormalsOnly" }
            HLSLPROGRAM
            #pragma multi_compile_local _ _TERRAIN_HEIGHT
            #pragma vertex Vert
            #pragma fragment Normals
            F Normals(V i) { ClipSurface(i); F o; o.color=half4(normalize(i.n),0); o.depth=lerp(i.p.z,i.floorDepth.x/i.floorDepth.y,_FlatDepth); return o; }
            ENDHLSL
        }
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode"="DepthOnly" }
            ColorMask R
            HLSLPROGRAM
            #pragma multi_compile_local _ _TERRAIN_HEIGHT
            #pragma vertex Vert
            #pragma fragment Depth
            ENDHLSL
        }
    }
}
