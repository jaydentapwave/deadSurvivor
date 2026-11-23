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
        _SoftFade("Actor window alpha fade instead of dither",Float)=0
        [HideInInspector] _SrcBlend("Src blend",Float)=1
        [HideInInspector] _DstBlend("Dst blend",Float)=0
        [HideInInspector] _ZWrite("Z write",Float)=1
        _GrassAlbedo("Painted grass albedo",2D)="white" {}
        _GrassTile("Painted grass tile metres",Float)=6
        _GrassWeight("Painted grass amount",Range(0,1))=0
        _GrassContrast("Painted grass detail contrast",Float)=1
        _PathEdge("Path edge grass mixing",Float)=0
        _MeadowSlope("View-only rolling hill shading (x scale, y strength)",Vector)=(0,0,0,0)
        _Leaves("Painted leaf detail",2D)="gray" {}
        _LeafPaint("Leaf tile, contrast, underside, top lift",Vector)=(0,0,1,1)
        _MeadowLight("Cloud shade (x scale, y depth) and sun patch (z scale, w lift)",Vector)=(0,0,0,0)
        _ShadeColor("Shadowed grass multiplier",Vector)=(.045,.045,.045,1)
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" }
        Cull [_Cull]
        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
        #include "DQMeadow.hlsl"
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
        TEXTURE2D(_GrassAlbedo); SAMPLER(sampler_GrassAlbedo);
        TEXTURE2D(_Leaves); SAMPLER(sampler_Leaves);
        CBUFFER_START(UnityPerMaterial)
        float4 _FieldRect; float _FlatDepth; float _Dither;
        float _Category;
        float _Diagnostic; float _AlphaClip; int _OcclusionRectCount;
        float _TerrainLightSlope; float _FlagWind;
        float _DQModel; float _DQBaseHeight; float _LeafDetail; float _WorldTexture;
        float _SoftFade; float _SrcBlend; float _DstBlend; float _ZWrite;
        float _GrassTile; float _GrassWeight; float4 _MeadowLight; float4 _ShadeColor;
        float _GrassContrast; float _PathEdge; float4 _LeafPaint; float4 _MeadowSlope;
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
        float4 _DQLightFocus, _DQAirColor;
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
        // Strongest actor-window coverage; edges ramp over several pixels so the fade reads as glass, not a cut.
        float ActorWindowFade(V i)
        {
            float fade=0;
            float2 uv=i.screen.xy/i.screen.w;
            for (int index=0;index<_OcclusionRectCount;index++)
            {
                float4 window=_OcclusionRects[index];
                float2 nearEdge=min(uv-window.xy,window.zw-uv)*_ScaledScreenParams.xy;
                fade=max(fade,saturate(min(nearEdge.x,nearEdge.y)/14));
            }
            return fade*_Dither;
        }
        void ClipSurface(V i)
        {
            if (_Dither > .001 && _SoftFade < .5)
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
            float2 fieldUV=(i.w.xz-_FieldRect.xy)/_FieldRect.zw;
            // Default (_GrassWeight 0) keeps the original .13/.08 mix; painted grass moves weight to the larger scale.
            float broad=Noise(i.w.xz*.12)*lerp(.13,.06,_GrassWeight)+Noise(i.w.xz*.047+14)*lerp(.08,.10,_GrassWeight);
            half3 grass=lerp(half3(.025,.405,.057),half3(.026,.405,.085),_GrassWeight)*(.89+broad);
            float velvet=SAMPLE_TEXTURE2D(_Ground,sampler_Ground,i.w.xz/8).r;
            // Painted grass is a mean-0.5 detail map; 1.44 keeps the old velvet average brightness.
            float painted=SAMPLE_TEXTURE2D(_GrassAlbedo,sampler_GrassAlbedo,i.w.xz/_GrassTile).r*2;
            grass*=lerp(.67+1.55*velvet,1.44*max(0,1+(painted-1)*_GrassContrast),_GrassWeight);
            float sunPatch=smoothstep(.55,.85,Noise(i.w.xz*_MeadowLight.z+57))*_MeadowLight.w;
            grass*=lerp(1,half3(1.10,1.05,.80),sunPatch);
            // Broad hue drift (warmer and cooler meadow) and a slightly softer saturation, scaled by the painted-grass weight.
            float hueDrift=Noise(i.w.xz*.05+23);
            grass*=lerp(1,lerp(half3(.92,1,1.10),half3(1.12,1,.82),hueDrift),_GrassWeight);
            grass=lerp(grass,dot(grass,half3(.3,.59,.11)).xxx,.10*_GrassWeight);
            half3 soil=SAMPLE_TEXTURE2D(_Dirt,sampler_Dirt,i.w.xz/3).rgb;
            float path=SAMPLE_TEXTURE2D(_PathMask,sampler_PathMask,fieldUV).r;
            half4 zone=SAMPLE_TEXTURE2D(_ZoneMask,sampler_ZoneMask,fieldUV);
            half3 stone=SAMPLE_TEXTURE2D(_Stone,sampler_Stone,i.w.xz/3).rgb;
            half3 c=lerp(grass,stone,smoothstep(.10,.85,zone.g));
            float pathEdge=(painted-1)*.55+(Noise(i.w.xz*5.3+9)-.5)*.45;
            c=lerp(c,soil,smoothstep(.05,.95,saturate(path+pathEdge*_PathEdge*(1-abs(path*2-1)))));
            if (_Category>2.5 && _Category<3.5)
            {
                if (_WorldTexture>.5) soil=SAMPLE_TEXTURE2D(_Dirt,sampler_Dirt,float2(i.w.x+i.w.z,i.w.y)/3).rgb;
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
                    // Default (_LeafPaint.y 0) keeps the original .78+.40 clump; painted leaves calm it.
                    float leafWeight=saturate(_LeafPaint.y*2);
                    c*=lerp(.78,.90,leafWeight)+lerp(.40,.20,leafWeight)*clump;
                    #if defined(_TERRAIN_HEIGHT)
                    if (i.leafEdge>.5) c=half3(.035,.18,.055)*(.8+.4*clump);
                    #endif
                    // Triplanar leaf clusters (after the edge colour so every crown surface carries leaves).
                    float3 blend=pow(abs(normalize(i.n)),4); blend/=blend.x+blend.y+blend.z;
                    float leaf=(SAMPLE_TEXTURE2D(_Leaves,sampler_Leaves,i.w.zy*_LeafPaint.x).r*blend.x
                        +SAMPLE_TEXTURE2D(_Leaves,sampler_Leaves,i.w.xz*_LeafPaint.x).r*blend.y
                        +SAMPLE_TEXTURE2D(_Leaves,sampler_Leaves,i.w.xy*_LeafPaint.x).r*blend.z)*2;
                    // Fine 2-3 px leaf speckle on top of the painted clusters (reference foliage reads leaf by leaf).
                    // Crown light first (lit top, dark underside), then fine even leaves that fade out with distance.
                    c*=lerp(_LeafPaint.z,_LeafPaint.w,smoothstep(-.6,.8,i.n.y));
                    float leafFar=1-.75*smoothstep(40,80,-TransformWorldToView(i.w).z);
                    float speck=Noise(float2(i.w.x+i.w.z*.7,i.w.y-i.w.z*.4)*34);
                    c*=max(0,1+(leaf-1)*_LeafPaint.y*leafFar)*(1+(speck-.5)*.12*leafFar*leafWeight);
                    // Pull the cyan model albedo toward leaf green.
                    c*=lerp(1,half3(1.06,1.02,.74),leafWeight);
                }
            }
            Light sun=GetMainLight(TransformWorldToShadowCoord(i.w));
            float3 surfaceNormal=i.n;
            if (_Category<.5 && _MeadowSlope.y>0)
            {
                // Lighting-only rolling hills: tilt the floor normal by a slow height field; positions stay flat.
                float2 p=i.w.xz*_MeadowSlope.x; float e=.35;
                float2 slope=float2(Noise(p+float2(e,0))-Noise(p-float2(e,0)),Noise(p+float2(0,e))-Noise(p-float2(0,e)))/(2*e);
                surfaceNormal=normalize(float3(i.n.x-slope.x*_MeadowSlope.y,i.n.y,i.n.z-slope.y*_MeadowSlope.y));
            }
            float3 normal=normalize(float3(surfaceNormal.x*(_Category<.5 ? _TerrainLightSlope : 1),surfaceNormal.y,surfaceNormal.z*(_Category<.5 ? _TerrainLightSlope : 1)));
            float face=lerp(.35,1,smoothstep(.0,.9,dot(normal,sun.direction)));
            float3 shade=lerp(_ShadeColor.rgb,1,sun.shadowAttenuation);
            // A small chromatic sky term reaches both sunlit and shaded scenery.
            c*=min(face,shade)*sun.color + SampleSH(normal)*.2;
            if (_Category<.5 || (_Category>2.5 && _Category<3.5))
            {
                // Slow cloud shade over the whole meadow, slightly cooler where shaded.
                c*=DQCloudTint(DQCloudShade(i.w.xz,_MeadowLight));
            }
            if (_Diagnostic>.5) c=min(face,shade.x).xxx;
            // Measurement-only color override; preserve original vertex/reference depth,
            // water opening and actor-window clip paths in the exact same GPU pass.
            if (_Diagnostic>1.5) c=_Diagnostic<2.5 ? half3(1,0,0) : _Diagnostic<3.5 ? half3(0,1,0) : half3(0,0,0);
            float eye=-TransformWorldToView(i.w).z;
            if (_Diagnostic<.5) c=lerp(c,_DQAirColor.rgb,smoothstep(_DQLightFocus.x+_DQLightFocus.y,_DQLightFocus.x+_DQLightFocus.z,eye)*.82*_DQLightFocus.w);
            output.color=half4(c,_SoftFade>.5 ? 1-ActorWindowFade(i) : 1);
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
            Blend [_SrcBlend] [_DstBlend]
            ZWrite [_ZWrite]
            HLSLPROGRAM
            #pragma multi_compile_local _ _TERRAIN_HEIGHT
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
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
