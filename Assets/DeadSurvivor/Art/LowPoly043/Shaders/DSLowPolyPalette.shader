// 043 — Dead Survivor shared low-poly shader (URP 17.4).
// One palette texture is the single color source; materials only carry style parameters.
// Design: docs/design/lowpoly-shader-043.md. Passes follow URP SimpleLit 17.4.0 keywords.
Shader "DeadSurvivor/LowPolyPalette"
{
    Properties
    {
        [NoScaleOffset] _Palette ("Palette (8x4 cells; A: 1 = non-emissive)", 2D) = "white" {}
        [Header(Height Gradient)]
        _GradientBottom ("Bottom Multiplier", Range(0, 1)) = 1
        _GradientHeight ("Height above pivot (world units)", Float) = 1
        [Header(Top Projection)]
        _TopCell ("Palette Cell (col, row)", Vector) = (2, 0, 0, 0)
        _TopStrength ("Strength", Range(0, 1)) = 0
        _TopMin ("Start (normal.y)", Range(0, 1)) = 0.55
        _TopMax ("Full (normal.y)", Range(0, 1)) = 0.85
        [Header(Ground Variation)]
        _NoiseStrength ("World Noise Strength", Range(0, 0.5)) = 0
        _NoiseScale ("World Noise Scale (world units)", Float) = 4
        [Header(Lighting)]
        _ShadeWrap ("Diffuse Wrap", Range(0, 1)) = 0.5
        _ShadeSoftness ("Ramp Softness", Range(0.01, 1)) = 1
        _AmbientStrength ("Ambient Strength", Range(0, 2)) = 1
        _RimStrength ("Rim Light (characters)", Range(0, 1)) = 0
        [Toggle(_FLAT_SHADING)] _FlatShading ("Flat Shading (characters)", Float) = 0
        [Header(Wind and Emission)]
        _WindStrength ("Wind Strength (world units)", Range(0, 0.2)) = 0
        _WindFrequency ("Wind Frequency", Float) = 1.3
        _EmissionStrength ("Emission Strength", Range(0, 8)) = 2
        [Toggle(_VERTEX_ALPHA_CLIP)] _VertexAlphaClip ("Clip by Vertex Alpha (soft-edged layers)", Float) = 0
        _Cutoff ("Vertex Alpha Cutoff", Range(0, 1)) = 0.5
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull", Float) = 2
        [Header(Occlusion Fade (045))]
        _BaseColor ("Fade Color (alpha written by DSMapVisibility)", Color) = (1,1,1,1)
        [ToggleUI] _DitherFade ("Dither Fade when alpha < 1", Float) = 0
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "UniversalMaterialType" = "SimpleLit" "IgnoreProjector" = "True" }
        LOD 300

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        TEXTURE2D(_Palette);
        SAMPLER(sampler_Palette);

        // Every material property used in code lives here so the SRP Batcher can batch
        // all 043 materials that share a variant.
        CBUFFER_START(UnityPerMaterial)
            float4 _TopCell;
            float _GradientBottom;
            float _GradientHeight;
            float _TopStrength;
            float _TopMin;
            float _TopMax;
            float _ShadeWrap;
            float _ShadeSoftness;
            float _AmbientStrength;
            float _RimStrength;
            float _WindStrength;
            float _WindFrequency;
            float _EmissionStrength;
            float _FlatShading;
            float _VertexAlphaClip;
            float _NoiseStrength;
            float _NoiseScale;
            float _Cutoff;
            float _Cull;
            half4 _BaseColor;
            float _DitherFade;
        CBUFFER_END

        // Global time-of-day values. All default to 0 = effect off, so an unset world
        // renders as plain day lighting.
        float4 _DS_ShadowTint;   // rgb tint for ambient-lit (shadowed) areas, a = amount
        float4 _DS_CloudShadow;  // x strength, y world scale, z speed, w unused
        float4 _DS_CloudDir;     // xy wind direction on world XZ
        float4 _DS_GradeTint;    // rgb tint for desaturated albedo (night grading), a = amount

        float2 DSPaletteCellUV(float2 cell)
        {
            // PNG row 0 is the top; UV v = 0 is the bottom of the texture.
            return float2((cell.x + 0.5) / 8.0, (3.0 - cell.y + 0.5) / 4.0);
        }

        void DSClip(float alpha)
        {
            #if defined(_VERTEX_ALPHA_CLIP)
                clip(alpha - _Cutoff);
            #endif
        }

        // 045: occlusion fade without a blend pass. Only materials with _DitherFade = 1 react to
        // _BaseColor.a; shared 043 materials keep _DitherFade = 0 and render exactly as before.
        static const float DSBayer4[16] = {0, 8, 2, 10, 12, 4, 14, 6, 3, 11, 1, 9, 15, 7, 13, 5};
        void DSFade(float2 pixel)
        {
            if (_DitherFade > 0.5 && _BaseColor.a < 0.999)
            {
                uint2 p = (uint2)pixel & 3;
                clip(_BaseColor.a - (DSBayer4[p.y * 4 + p.x] + 0.5) / 16.0);
            }
        }

        // Height above the object's pivot in world units. Independent of import axis/scale
        // (Blender FBX arrives Z-up with a -90 deg X rotation and x100 scale on the child).
        float DSHeight01(float3 positionOS)
        {
            float above = TransformObjectToWorld(positionOS).y - TransformObjectToWorld(float3(0, 0, 0)).y;
            return saturate(above / max(_GradientHeight, 1e-3));
        }

        float3 DSWorldPosition(float3 positionOS)
        {
            float3 positionWS = TransformObjectToWorld(positionOS);
            if (_WindStrength > 0)
            {
                float h = DSHeight01(positionOS);
                float t = _Time.y * _WindFrequency + dot(positionWS.xz, float2(0.7, 0.4));
                float2 sway = float2(sin(t), 0.5 * sin(t * 0.83 + 1.7));
                positionWS.xz += sway * (_WindStrength * h * h);
            }
            return positionWS;
        }
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            Cull [_Cull]
            ZWrite On

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex DSVert
            #pragma fragment DSFrag
            #pragma shader_feature_local _FLAT_SHADING
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile _ _LIGHT_LAYERS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fragment _ _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fragment _ _LIGHT_COOKIES
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Fog.hlsl"
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/RenderingLayers.hlsl"
            #pragma multi_compile_instancing
            #pragma shader_feature_local _VERTEX_ALPHA_CLIP
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float4 color : COLOR;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                float3 normalWS : TEXCOORD2;
                float4 shadowCoord : TEXCOORD3;
                float2 gradientFog : TEXCOORD4; // x height gradient multiplier, y fog factor
                float vertexAlpha : TEXCOORD5;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings DSVert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                VertexPositionInputs vpi;
                vpi.positionWS = DSWorldPosition(input.positionOS.xyz);
                vpi.positionVS = TransformWorldToView(vpi.positionWS);
                vpi.positionCS = TransformWorldToHClip(vpi.positionWS);
                float4 ndc = vpi.positionCS * 0.5f;
                vpi.positionNDC = float4(float2(ndc.x, ndc.y * _ProjectionParams.x) + ndc.w, vpi.positionCS.zw);

                output.positionCS = vpi.positionCS;
                output.positionWS = vpi.positionWS;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.uv = input.uv;
                output.vertexAlpha = input.color.a;
                #if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
                    output.shadowCoord = GetShadowCoord(vpi);
                #endif
                float g = DSHeight01(input.positionOS.xyz);
                output.gradientFog = float2(lerp(_GradientBottom, 1.0, g), ComputeFogFactor(vpi.positionCS.z));
                return output;
            }

            float DSValueNoise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                float2 u = f * f * (3.0 - 2.0 * f);
                float a = frac(sin(dot(i, float2(127.1, 311.7))) * 43758.5453);
                float b = frac(sin(dot(i + float2(1, 0), float2(127.1, 311.7))) * 43758.5453);
                float c = frac(sin(dot(i + float2(0, 1), float2(127.1, 311.7))) * 43758.5453);
                float d = frac(sin(dot(i + float2(1, 1), float2(127.1, 311.7))) * 43758.5453);
                return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y);
            }

            half DSCloudShadow(float3 positionWS)
            {
                if (_DS_CloudShadow.x <= 0) return 1;
                float2 p = positionWS.xz / max(_DS_CloudShadow.y, 1e-3) + _DS_CloudDir.xy * (_Time.y * _DS_CloudShadow.z);
                float n = 0.65 * DSValueNoise(p) + 0.35 * DSValueNoise(p * 2.13 + 17.0);
                return 1 - _DS_CloudShadow.x * smoothstep(0.42, 0.55, n);
            }

            half DSRamp(half ndotl)
            {
                half wrapped = (ndotl + _ShadeWrap) / (1 + _ShadeWrap);
                return smoothstep(0, _ShadeSoftness, wrapped);
            }

            half4 DSFrag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                DSClip(input.vertexAlpha);
                DSFade(input.positionCS.xy);

                float3 viewWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
                float3 normalWS = normalize(input.normalWS);
                #if defined(_FLAT_SHADING)
                    normalWS = normalize(cross(ddy(input.positionWS), ddx(input.positionWS)));
                    normalWS = dot(normalWS, viewWS) < 0 ? -normalWS : normalWS;
                #endif

                half4 pal = SAMPLE_TEXTURE2D(_Palette, sampler_Palette, input.uv);
                half3 albedo = pal.rgb * input.gradientFog.x;
                if (_NoiseStrength > 0)
                {
                    float2 q = input.positionWS.xz / max(_NoiseScale, 1e-3);
                    float gn = 0.6 * DSValueNoise(q) + 0.4 * DSValueNoise(q * 3.1 + 5.0);
                    albedo *= 1 + (gn - 0.5) * 2 * _NoiseStrength;
                }
                if (_DS_GradeTint.a > 0)
                {
                    half luma = dot(albedo, half3(0.2126, 0.7152, 0.0722));
                    albedo = lerp(albedo, luma * _DS_GradeTint.rgb * 1.6, saturate(_DS_GradeTint.a));
                }
                half top = smoothstep(_TopMin, _TopMax, normalWS.y) * _TopStrength;
                if (top > 0)
                {
                    half3 topColor = SAMPLE_TEXTURE2D(_Palette, sampler_Palette, DSPaletteCellUV(_TopCell.xy)).rgb;
                    albedo = lerp(albedo, topColor, top);
                }

                // Same shadow-coordinate contract as URP SimpleLitForwardPass: cascades are
                // resolved per fragment, single-cascade / screen-space use the interpolator.
                #if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
                    float4 shadowCoord = input.shadowCoord;
                #elif defined(MAIN_LIGHT_CALCULATE_SHADOWS)
                    float4 shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                #else
                    float4 shadowCoord = float4(0, 0, 0, 0);
                #endif
                float2 screenUV = GetNormalizedScreenSpaceUV(input.positionCS);
                AmbientOcclusionFactor ao = GetScreenSpaceAmbientOcclusion(screenUV);
                #if defined(_LIGHT_LAYERS)
                    uint meshRenderingLayers = GetMeshRenderingLayer();
                #endif

                Light mainLight = GetMainLight(shadowCoord, input.positionWS, half4(1, 1, 1, 1));
                half direct = DSRamp(dot(normalWS, mainLight.direction)) * mainLight.shadowAttenuation * DSCloudShadow(input.positionWS) * ao.directAmbientOcclusion;
                #if defined(_LIGHT_LAYERS)
                    if (!IsMatchingLightLayer(mainLight.layerMask, meshRenderingLayers)) direct = 0;
                #endif
                half3 ambient = SampleSH(normalWS) * _AmbientStrength * ao.indirectAmbientOcclusion;
                ambient *= lerp(half3(1, 1, 1), _DS_ShadowTint.rgb, saturate(_DS_ShadowTint.a));
                half3 lighting = ambient + mainLight.color * (direct * mainLight.distanceAttenuation);

                // Additional lights are always evaluated per pixel; the vertex-lighting keyword
                // is treated the same way (supported range, see docs/design/lowpoly-shader-043.md).
                #if defined(_ADDITIONAL_LIGHTS) || defined(_ADDITIONAL_LIGHTS_VERTEX)
                    InputData inputData = (InputData)0;
                    inputData.positionWS = input.positionWS;
                    inputData.normalizedScreenSpaceUV = screenUV;
                    uint pixelLightCount = GetAdditionalLightsCount();
                    #if USE_CLUSTER_LIGHT_LOOP
                    [loop] for (uint lightIndex = 0; lightIndex < min(URP_FP_DIRECTIONAL_LIGHTS_COUNT, MAX_VISIBLE_LIGHTS); lightIndex++)
                    {
                        CLUSTER_LIGHT_LOOP_SUBTRACTIVE_LIGHT_CHECK
                        Light dl = GetAdditionalLight(lightIndex, inputData, half4(1, 1, 1, 1), ao); // AO overload: same as URP (043-V7)
                        #if defined(_LIGHT_LAYERS)
                            if (!IsMatchingLightLayer(dl.layerMask, meshRenderingLayers)) continue;
                        #endif
                        lighting += dl.color * (DSRamp(dot(normalWS, dl.direction)) * dl.distanceAttenuation * dl.shadowAttenuation);
                    }
                    #endif
                    LIGHT_LOOP_BEGIN(pixelLightCount)
                        Light al = GetAdditionalLight(lightIndex, inputData, half4(1, 1, 1, 1), ao);
                        #if defined(_LIGHT_LAYERS)
                            if (IsMatchingLightLayer(al.layerMask, meshRenderingLayers))
                        #endif
                        lighting += al.color * (saturate(dot(normalWS, al.direction)) * al.distanceAttenuation * al.shadowAttenuation);
                    LIGHT_LOOP_END
                #endif

                half3 color = albedo * lighting;
                half rim = pow(1 - saturate(dot(normalWS, viewWS)), 3) * _RimStrength;
                color += albedo * rim * (mainLight.color + ambient + 0.5);
                color += pal.rgb * ((1 - pal.a) * _EmissionStrength);
                color = MixFog(color, input.gradientFog.y);
                return half4(color, 1);
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull [_Cull]

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex DSShadowVert
            #pragma fragment DSShadowFrag
            #pragma multi_compile_instancing
            #pragma shader_feature_local _VERTEX_ALPHA_CLIP
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            float3 _LightDirection;
            float3 _LightPosition;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float4 color : COLOR;
                float3 normalOS : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float vertexAlpha : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            Varyings DSShadowVert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                float3 positionWS = DSWorldPosition(input.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
                #if _CASTING_PUNCTUAL_LIGHT_SHADOW
                    float3 lightDirectionWS = normalize(_LightPosition - positionWS);
                #else
                    float3 lightDirectionWS = _LightDirection;
                #endif
                output.vertexAlpha = input.color.a;
                output.positionCS = ApplyShadowClamping(TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, lightDirectionWS)));
                return output;
            }

            half4 DSShadowFrag(Varyings input) : SV_Target
            {
                DSClip(input.vertexAlpha);
                DSFade(input.positionCS.xy);
                return 0;
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On
            ColorMask R
            Cull [_Cull]

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex DSDepthVert
            #pragma fragment DSDepthFrag
            #pragma multi_compile_instancing
            #pragma shader_feature_local _VERTEX_ALPHA_CLIP

            struct Attributes
            {
                float4 positionOS : POSITION;
                float4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float vertexAlpha : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings DSDepthVert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionCS = TransformWorldToHClip(DSWorldPosition(input.positionOS.xyz));
                output.vertexAlpha = input.color.a;
                return output;
            }

            half DSDepthFrag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                DSClip(input.vertexAlpha);
                DSFade(input.positionCS.xy);
                return input.positionCS.z;
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }
            ZWrite On
            Cull [_Cull]

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex DSDepthNormalsVert
            #pragma fragment DSDepthNormalsFrag
            #pragma shader_feature_local _FLAT_SHADING
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/RenderingLayers.hlsl"
            #pragma multi_compile_instancing
            #pragma shader_feature_local _VERTEX_ALPHA_CLIP

            struct Attributes
            {
                float4 positionOS : POSITION;
                float4 color : COLOR;
                float3 normalOS : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 normalWS : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                float vertexAlpha : TEXCOORD2;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings DSDepthNormalsVert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionWS = DSWorldPosition(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.vertexAlpha = input.color.a;
                return output;
            }

            void DSDepthNormalsFrag(
                Varyings input
                , out half4 outNormalWS : SV_Target0
            #ifdef _WRITE_RENDERING_LAYERS
                , out uint outRenderingLayers : SV_Target1
            #endif
            )
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                DSClip(input.vertexAlpha);
                DSFade(input.positionCS.xy);
                float3 normalWS = normalize(input.normalWS);
                #if defined(_FLAT_SHADING)
                    normalWS = normalize(cross(ddy(input.positionWS), ddx(input.positionWS)));
                    normalWS = dot(normalWS, GetWorldSpaceNormalizeViewDir(input.positionWS)) < 0 ? -normalWS : normalWS;
                #endif
                outNormalWS = half4(NormalizeNormalPerPixel(normalWS), 0.0);
                #ifdef _WRITE_RENDERING_LAYERS
                    outRenderingLayers = EncodeMeshRenderingLayer();
                #endif
            }
            ENDHLSL
        }
    }
    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
