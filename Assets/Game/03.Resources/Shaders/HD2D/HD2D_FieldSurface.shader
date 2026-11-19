// Adapted from Assets/Game/03.Resources/Art/HD2D/Round8/FieldSurface.shader SHA256 e6920e1cb487afafa828de56baedfbec2c0f61c4d1a860a2ed9e003417e56dc4
Shader "HD2D/HD2D_FieldSurface"
{
    Properties
    {
        _Ground("Original field",2D)="white" {}
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
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" }
        Cull [_Cull]
        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
        TEXTURE2D(_Ground); SAMPLER(sampler_Ground);
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
        float _TerrainLightSlope;
        float4 _AlbedoTint;
        float4 _OcclusionRects[64];
        CBUFFER_END
        #if defined(_TERRAIN_HEIGHT)
        struct A { float3 p:POSITION; float3 n:NORMAL; float2 uv:TEXCOORD0; float2 terrain:TEXCOORD1; };
        struct V { float4 p:SV_POSITION; float3 w:TEXCOORD0; float3 n:TEXCOORD1; float2 uv:TEXCOORD2; float2 floorDepth:TEXCOORD3; float4 screen:TEXCOORD4; float href:TEXCOORD5; };
        #else
        struct A { float3 p:POSITION; float3 n:NORMAL; float2 uv:TEXCOORD0; };
        struct V { float4 p:SV_POSITION; float3 w:TEXCOORD0; float3 n:TEXCOORD1; float2 uv:TEXCOORD2; float2 floorDepth:TEXCOORD3; float4 screen:TEXCOORD4; };
        #endif
        V Vert(A i)
        {
        #if defined(_TERRAIN_HEIGHT)
            V o; o.w=TransformObjectToWorld(i.p); o.n=TransformObjectToWorldNormal(i.n); o.p=TransformWorldToHClip(o.w); o.uv=i.uv; o.screen=ComputeScreenPos(o.p); o.href=i.terrain.x; float4 flat=TransformWorldToHClip(float3(o.w.x,o.href,o.w.z)); o.floorDepth=flat.zw; return o;
        #else
            V o; o.w=TransformObjectToWorld(i.p); o.n=TransformObjectToWorldNormal(i.n); o.p=TransformWorldToHClip(o.w); o.uv=i.uv; o.screen=ComputeScreenPos(o.p); float4 flat=TransformWorldToHClip(float3(o.w.x,0,o.w.z)); o.floorDepth=flat.zw; return o;
        #endif
        }
        void ClipOpening(V i)
        {
            if (_AlphaClip > .5) clip(SAMPLE_TEXTURE2D(_MainTex,sampler_PointClamp,i.uv).a-.5);
            float waterOpening=SAMPLE_TEXTURE2D(_WaterMask,sampler_PointClamp,(i.w.xz-_FieldRect.xy)/_FieldRect.zw).r;
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
        F Frag(V i)
        {
            ClipSurface(i);
            F output; output.depth=lerp(i.p.z,i.floorDepth.x/i.floorDepth.y,_FlatDepth);
            float2 cells=floor(float2(i.w.x,dot(i.w,float3(0,.8660254,.5)))*48+.00001);
            float grain=SAMPLE_TEXTURE2D(_Pattern,sampler_Pattern,(cells+.5)/128).r;
            float2 floorUV=i.w.xz/42.6666667+.5;
            half3 grass=LinearToSRGB(SAMPLE_TEXTURE2D(_Ground,sampler_Ground,floorUV).rgb);
            float path=SAMPLE_TEXTURE2D(_PathMask,sampler_PathMask,(i.w.xz-_FieldRect.xy)/_FieldRect.zw).r;
            half3 soilGround=LinearToSRGB(SAMPLE_TEXTURE2D(_Dirt,sampler_Dirt,i.w.xz/2.666667).rgb);
            half4 zone=SAMPLE_TEXTURE2D(_ZoneMask,sampler_PointClamp,(i.w.xz-_FieldRect.xy)/_FieldRect.zw);
            // Zone hue shifts preserve luminance, retaining the existing +/-7/255 broad-light budget.
            float tone=(zone.b*255-128)/100;
            grass+=tone*half3(-9,3,-4)/255;
            half3 meadow=half3(5,8,1);
            grass+=zone.a*(meadow-dot(meadow,half3(.2126,.7152,.0722)))/255;
            grass=lerp(grass,soilGround,zone.r*.45);
            grass=lerp(grass,soilGround,zone.g);
            grass=lerp(grass,soilGround,step(.5,path));
            Light sun=GetMainLight(TransformWorldToShadowCoord(i.w));
            float3 cellNormal=normalize(i.n);
            float d=saturate(dot(cellNormal,sun.direction));
            float band=d<.25 ? .58 : d<.68 ? .80 : 1;
            float finger=fmod(abs(cells.x+floor(cells.x/5)*3),9)<3 ? .055 : .018;
        #if defined(_TERRAIN_HEIGHT)
            if (_Diagnostic>1.5) { output.color=_Category<.5 && i.w.y-i.href>.3 ? half4(1,0,0,1) : _Category>2.5 && _Category<3.5 ? (i.n.y>.7 ? half4(1,0,0,1) : i.w.y-i.href<=.36-finger && i.w.y-i.href>.36-finger-1.0/48 ? half4(0,1,0,1) : i.w.y-i.href>.36-finger ? half4(0,0,0,1) : half4(0,0,1,1)) : half4(0,0,0,1); return output; }
        #else
            if (_Diagnostic>1.5) { output.color=_Category<.5 && i.w.y>.3 ? half4(1,0,0,1) : _Category>2.5 && _Category<3.5 ? (i.n.y>.7 ? half4(1,0,0,1) : i.w.y<=.36-finger && i.w.y>.36-finger-1.0/48 ? half4(0,1,0,1) : i.w.y>.36-finger ? half4(0,0,0,1) : half4(0,0,1,1)) : half4(0,0,0,1); return output; }
        #endif
            if (_Diagnostic>.5) { output.color=half4(SRGBToLinear(band.xxx),1); return output; }
            half3 c=grass;
        #if defined(_TERRAIN_HEIGHT)
            // Normals originate in the height-grid mesh. Exaggerate only lighting;
            // positions, support heights and the keyword-OFF path stay unchanged.
            if (_Category < .5)
            {
                float3 slopeNormal=normalize(float3(i.n.x*_TerrainLightSlope,i.n.y,i.n.z*_TerrainLightSlope));
                float slopeLight=dot(slopeNormal,sun.direction);
                c*=slopeLight<.47 ? .90 : slopeLight<.59 ? 1 : 1.07;
            }
        #endif
            if (_Category>.5 && _Category<1.5)
            {
                float rank=d<.12 ? 0 : d<.45 ? 1 : 2;
                if (grain<.3) rank=max(0,rank-1);
                else if (grain>.8) rank=min(2,rank+1);
                c=rank<.5 ? half3(64,61,42)/255 : rank<1.5 ? half3(157,151,120)/255 : half3(216,211,169)/255;

            }
            else if (_Category>1.5 && _Category<2.5)
            {
                c=LinearToSRGB(SAMPLE_TEXTURE2D(_Water,sampler_Water,floorUV).rgb);
            }
            else if (_Category>2.5 && _Category<3.5)
            {
        #if defined(_TERRAIN_HEIGHT)
                float2 soilUV=(floor(float2(i.w.x+i.w.z,i.w.y-i.href)*48)+.5)/128;
        #else
                float2 soilUV=(floor(float2(i.w.x+i.w.z,i.w.y)*48)+.5)/128;
        #endif
                half3 soil=LinearToSRGB(SAMPLE_TEXTURE2D(_Soil,sampler_Soil,soilUV).rgb);
                if (i.n.y>.7) c=grass;
        #if defined(_TERRAIN_HEIGHT)
                else if (i.w.y-i.href>.36-finger) c=grass;
                else if (i.w.y-i.href>.36-finger-1.0/48) c=saturate(grass*1.2);
        #else
                else if (i.w.y>.36-finger) c=grass;
                else if (i.w.y>.36-finger-1.0/48) c=saturate(grass*1.2);
        #endif
                else
                {
        #if defined(_TERRAIN_HEIGHT)
                    float row=floor((i.w.y-i.href)*24);
        #else
                    float row=floor(i.w.y*24);
        #endif
                    float bandSide=row<3 ? .7 : row<6 ? .88 : 1.05;
                    c=soil*bandSide;
                }
            }
            else if (_Category>4.5) { c=LinearToSRGB(SAMPLE_TEXTURE2D(_MainTex,sampler_PointClamp,i.uv).rgb)*_AlbedoTint.rgb; }
            else if (_Category>3.5)
            {
                float rank=(d<.25 ? 0 : d<.68 ? 1 : 2);
                float stripe=fmod(cells.x+floor(cells.y/9),7);
                if (stripe<1) rank=max(0,rank-1);
                c=rank<.5 ? half3(66,52,32)/255 : rank<1.5 ? half3(111,94,62)/255 : half3(168,165,133)/255;
            }
            float shadow=(_Category>.5 && _Category<1.5) ? 1 : _Category>2.5 && _Category<3.5 && i.n.y<.7 ? 1 : step(.5,sun.shadowAttenuation);
            half3 shadowFactor=lerp(half3(.52,.45,.36),half3(1,1,1),shadow);
            // Toon face shade and hard shadow describe the same occluded light.
            // Applying both multiplicatively crushed tent cloth to grey.
            c*=_Category>4.5 ? min(band.xxx,shadowFactor) : shadowFactor;
            output.color=half4(SRGBToLinear(c),1); return output;
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
