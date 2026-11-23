// Native painted background lighting from Assets/Game/03.Resources/Art/HD2D/PixelSpriteLit.shader SHA256 62b98c9663935462ee12f2a7dcc7480d4adbe6ce83e7129eee469e6e43d3e85e
// Source: Assets/Game/03.Resources/Art/HD2D/Round8/StableSpriteLit.shader SHA256 f0701ac97deae4f6d041567695cb83ca97e93db13d4a0513be14b8061c784a1f
Shader "HD2D/HD2D_SpriteLit"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite", 2D) = "white" {}
        _DQLighting ("DQ scene lighting", Float) = 0
        _DQExposure ("DQ source exposure", Float) = .95
        [PerRendererData] _DQSpriteUVRect ("DQ atlas inner bounds", Vector) = (0,0,1,1)
        _ReceiveShadows ("Receive shadows", Float) = 1
        [PerRendererData] _Appearance ("Spawn appearance", Range(0,1)) = 1
        _SpriteRenderer ("SpriteRenderer data", Float) = 1
        _BaseColor ("Tint", Color) = (1,1,1,1)
        _Emission ("Emission", Range(0,4)) = 0
        _BackgroundCard ("Native painted background card", Float) = 0
        _Dither ("Dither amount", Range(0,1)) = 0
        _HueShift ("Palette hue shift", Range(0,1)) = 0
        _MeadowLight ("DQ floor cloud shade (copy of the floor material value; 0 = off)", Vector) = (0,0,0,0)
        _SideShade ("DQ sun-side dot rim (x) and away-side darkening (y); 0 = off", Vector) = (0,0,0,0)
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="TransparentCutout" "Queue"="AlphaTest" }
        Cull Off
        ZWrite On
        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
        #include "DQMeadow.hlsl"
        TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
        float4 _MainTex_TexelSize;
        CBUFFER_START(UnityPerMaterial)
        half4 _BaseColor;
        float _Emission;
        float _Dither;
        int _OcclusionRectCount;
        float4 _OcclusionRects[64];
        float _BackgroundCard;
        float _HueShift;
        float _SpriteRenderer;
        float _ReceiveShadows;
        float _DQLighting;
        float _DQExposure;
        float4 _MeadowLight;
        float4 _SideShade;
        CBUFFER_END
        TEXTURE2D(_HD2DTerrainHeight);
        float4 _HD2DTerrainHeight_TexelSize;
        float4 _HD2DTerrainGrid;
        float _Appearance;
        float4 _DQSpriteUVRect;
        float4 _DQLightFocus, _DQAirColor;
        struct Attributes
        {
            float3 positionOS : POSITION;
            float2 uv : TEXCOORD0;
            half4 color : COLOR;
            UNITY_VERTEX_INPUT_INSTANCE_ID
        };
        struct Varyings
        {
            float4 positionCS : SV_POSITION;
            float2 uv : TEXCOORD0;
            float3 positionWS : TEXCOORD1;
            half4 color : COLOR;
            float4 screen : TEXCOORD2;
            float sunU : TEXCOORD3;
        };
        // Same triangle split as TerrainHeightMath.Sample (C#), clamped to the grid.
        float TerrainHeightAt(float2 xz)
        {
            float2 size = _HD2DTerrainHeight_TexelSize.zw;
            float2 g = clamp((xz - _HD2DTerrainGrid.xy) * _HD2DTerrainGrid.z, 0, size - 1);
            int2 c = (int2)min(floor(g), size - 2);
            float2 f = g - c;
            float h00 = LOAD_TEXTURE2D(_HD2DTerrainHeight, c).r;
            float h11 = LOAD_TEXTURE2D(_HD2DTerrainHeight, c + int2(1, 1)).r;
            if (f.x >= f.y)
            {
                float h10 = LOAD_TEXTURE2D(_HD2DTerrainHeight, c + int2(1, 0)).r;
                return h00 + f.x * (h10 - h00) + f.y * (h11 - h10);
            }
            float h01 = LOAD_TEXTURE2D(_HD2DTerrainHeight, c + int2(0, 1)).r;
            return h00 + f.y * (h01 - h00) + f.x * (h11 - h01);
        }
        // One view-depth pull per sprite (same rule as TerrainHeightTexture.SpriteBias): walk the camera ray from points
        // every 0.1 m on the renderer bounds' bottom edge until it leaves the ground. Any point still buried after 1.5 m
        // is behind a ridge: occlusion wins and the sprite keeps its depth.
        float SpriteDepthBias()
        {
            if (_HD2DTerrainGrid.w < .5)
            {
                return 0;
            }
            float bias = 0;
            int count = clamp((int)ceil((unity_RendererBounds_Max.x - unity_RendererBounds_Min.x) / .1) + 1, 2, 32);
            [loop] for (int k = 0; k < count; k++)
            {
                float3 p = float3(lerp(unity_RendererBounds_Min.x, unity_RendererBounds_Max.x, k / (count - 1.0)), unity_RendererBounds_Min.y, unity_RendererBounds_Min.z);
                float3 ray = normalize(_WorldSpaceCameraPos - p);
                bool isOut = false;
                [loop] for (float t = 0; t <= 1.5; t += .08)
                {
                    float3 q = p + ray * t;
                    if (TerrainHeightAt(q.xz) <= q.y)
                    {
                        bias = max(bias, t);
                        isOut = true;
                        break;
                    }
                }
                if (!isOut)
                {
                    return 0;
                }
            }
            return bias + .08;
        }
        // Same flip and alpha in all three passes, including the shadow silhouette.
        Varyings SpriteVertex(Attributes v)
        {
            UNITY_SETUP_INSTANCE_ID(v);
            Varyings o;
            float3 p = float3(v.positionOS.xy * lerp(float2(1,1), unity_SpriteProps.xy, _SpriteRenderer), v.positionOS.z);
            o.positionWS = TransformObjectToWorld(p);
            o.positionCS = TransformWorldToHClip(o.positionWS);
            float depthBias = SpriteDepthBias();
            if (depthBias > 0)
            {
                float3 viewPosition = TransformWorldToView(o.positionWS);
                viewPosition.z += depthBias;
                float4 biased = mul(UNITY_MATRIX_P, float4(viewPosition, 1));
                o.positionCS.z = biased.z / biased.w * o.positionCS.w;
            }
            o.screen = ComputeScreenPos(o.positionCS);
            o.uv = v.uv;
            // Sun direction along the sprite's texture x axis (flip-aware): + means the sun is toward +u.
            float3 textureRight = TransformObjectToWorldDir(float3(lerp(1, unity_SpriteProps.x, _SpriteRenderer), 0, 0));
            o.sunU = dot(GetMainLight().direction, textureRight);
            o.color = v.color * lerp(half4(1,1,1,1), unity_SpriteColor, _SpriteRenderer) * _BaseColor;
            return o;
        }
        float4 DQCell(float2 uv)
        {
            float4 c = SAMPLE_TEXTURE2D_LOD(_MainTex, sampler_MainTex, clamp(uv, _DQSpriteUVRect.xy, _DQSpriteUVRect.zw), 0);
            c.rgb *= c.a;
            return c;
        }
        // Per texel: the two texels nearest the sun edge lift, the far third of the body darkens.
        // Distances are counted in whole texels along the sun axis, so the dot grid stays intact.
        float SideShade(float2 uv, float sunU)
        {
            float weight = saturate(abs(sunU) * 4);
            if (weight < .01)
            {
                return 1;
            }
            float2 texel = _MainTex_TexelSize.xy;
            float2 center = (floor(uv / texel) + .5) * texel;
            float2 toward = float2(sunU > 0 ? texel.x : -texel.x, 0);
            float toSun = 13, toAway = 13;
            [unroll] for (int k = 12; k >= 1; k--)
            {
                if (DQCell(center + toward * k).a < .5) toSun = k;
                if (DQCell(center - toward * k).a < .5) toAway = k;
            }
            // 0 at the sun edge, 1 at the away edge; evaluated once per texel so steps follow the dot grid.
            float across = toSun / (toSun + toAway);
            float rim = toSun < 1.5 ? _SideShade.x : (toSun < 2.5 ? _SideShade.x * .5 : 0);
            float away = _SideShade.y * smoothstep(.45, .75, across);
            return 1 + (rim - away) * weight;
        }
        half4 SpriteSample(Varyings i)
        {
            half4 c = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv + float2(0.00001, 0.00001)) * i.color;
            if (_DQLighting > .5)
            {
                float2 texel = 1.0 / float2(_MainTex_TexelSize.z, _MainTex_TexelSize.w);
                float2 pixel = i.uv / texel - .5;
                float2 baseUV = (floor(pixel) + .5) * texel;
                float4 a = DQCell(baseUV), b = DQCell(baseUV + float2(texel.x,0));
                float4 d = DQCell(baseUV + float2(0,texel.y)), e = DQCell(baseUV + texel);
                float2 w = smoothstep(.45,.55,frac(pixel));
                if (min(min(a.a,b.a),min(d.a,e.a)) < max(max(a.a,b.a),max(d.a,e.a)))
                {
                    w = smoothstep(.14,.86,frac(pixel));
                }
                c = lerp(lerp(a,b,w.x),lerp(d,e,w.x),w.y);
                c.rgb /= max(c.a,.0001);
                c *= i.color;
            }
            clip(c.a - (_DQLighting > .5 ? .01 : .5));
            if (_Dither > .001)
            {
                float2 uv=i.screen.xy/i.screen.w;
                for (int index=0;index<_OcclusionRectCount;index++)
                {
                float4 window=_OcclusionRects[index];
                float2 nearEdge=min(uv-window.xy,window.zw-uv)*_ScaledScreenParams.xy;
                float border=min(nearEdge.x,nearEdge.y);
                if (border>0) clip(frac(dot(floor(i.positionCS.xy),float2(.754877666,.569840296)))-_Dither*saturate(border/2));
                }
            }
            return c;
        }
        half4 LitFragment(Varyings i) : SV_Target
        {
            half4 c = SpriteSample(i);
            float4 k = float4(0,-1.0/3.0,2.0/3.0,-1);
            float4 p = lerp(float4(c.bg,k.wz),float4(c.gb,k.xy),step(c.b,c.g));
            float4 q = lerp(float4(p.xyw,c.r),float4(c.r,p.yzx),step(p.x,c.r));
            float d = q.x - min(q.w,q.y);
            float3 hsv = float3(abs(q.z+(q.w-q.y)/(6*d+1e-6)),d/(q.x+1e-6),q.x);
            float3 ramp = abs(frac(frac(hsv.x+_HueShift)+float3(0,2.0/3.0,1.0/3.0))*6-3);
            c.rgb = hsv.z*lerp(float3(1,1,1),saturate(ramp-1),hsv.y);
            Light sun = GetMainLight(TransformWorldToShadowCoord(i.positionWS));
            if (_DQLighting > .5)
            {
                float3 ambient = SampleSH(float3(0,1,0));
                float visibility = lerp(1,sun.shadowAttenuation,_ReceiveShadows);
                float energy = min(1, max(.04,dot(ambient,float3(.2126,.7152,.0722))) + dot(sun.color,float3(.2126,.7152,.0722))*.95*visibility)*_DQExposure;
                float3 tint = lerp(ambient / max(max(ambient.r,ambient.g),max(ambient.b,.0001)), sun.color/max(max(sun.color.r,sun.color.g),max(sun.color.b,.0001)), visibility);
                c.rgb = saturate(c.rgb * energy * lerp(1,tint,.3));
                // Same cloud shade as the floor under the actor, and a dot-step sun side.
                c.rgb *= DQCloudTint(DQCloudShade(i.positionWS.xz, _MeadowLight));
                if (_SideShade.y > 0)
                {
                    c.rgb = saturate(c.rgb * SideShade(i.uv, i.sunU));
                }
                float eye = -TransformWorldToView(i.positionWS).z;
                c.rgb = lerp(c.rgb,_DQAirColor.rgb,smoothstep(_DQLightFocus.x+_DQLightFocus.y,_DQLightFocus.x+_DQLightFocus.z,eye)*.65*_DQLightFocus.w);
                return half4(lerp(half3(.55,.68,.64),c.rgb,_Appearance),c.a);
            }
            half diffuse = saturate(dot(normalize(float3(0,0.85,-0.53)),sun.direction));
            half3 ambient = half3(0.43,0.32,0.27);
            diffuse = lerp(diffuse < .25 ? .58 : (diffuse < .68 ? .80 : 1.0), diffuse, _BackgroundCard);
            half3 lit = c.rgb * (ambient + sun.color * diffuse * lerp(1,step(.5,sun.shadowAttenuation),_ReceiveShadows)) + c.rgb * _Emission;
            return half4(lerp(half3(.55,.68,.64), lit, _Appearance), 1);
        }
        float3 _LightDirection;
        Varyings ShadowVertex(Attributes v)
        {
            Varyings o = SpriteVertex(v);
            o.positionCS = TransformWorldToHClip(ApplyShadowBias(o.positionWS,float3(0,1,0),_LightDirection));
            o.positionCS = ApplyShadowClamping(o.positionCS);
            return o;
        }
        half4 DepthFragment(Varyings i) : SV_Target
        {
            SpriteSample(i);
            return 0;
        }
        ENDHLSL
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            HLSLPROGRAM
            #pragma vertex SpriteVertex
            #pragma fragment LitFragment
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            ENDHLSL
        }
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode"="ShadowCaster" }
            ColorMask 0
            HLSLPROGRAM
            #pragma vertex ShadowVertex
            #pragma fragment DepthFragment
            ENDHLSL
        }
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode"="DepthOnly" }
            ColorMask R
            HLSLPROGRAM
            #pragma vertex SpriteVertex
            #pragma fragment DepthFragment
            ENDHLSL
        }
    }
}
