Shader "HD2D/Round2/ToonSurface"
{
    Properties
    {
        _Ground ("Ground", 2D) = "white" {}
        _Pattern ("Three value point pattern", 2D) = "white" {}
        _Palette ("Source face palette", 2D) = "white" {}
        _Category ("0 ground, 1 tree, 2 stone, 3 water", Float) = 0
        _Density ("View aligned texels per unit", Float) = 48
        _TrunkTop ("Tree trunk top in world units", Float) = 0.32
        _Diagnostic ("0 beauty, 1 lighting bands", Float) = 0
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" }
        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
        TEXTURE2D(_Ground); SAMPLER(sampler_Ground);
        TEXTURE2D(_Pattern); SAMPLER(sampler_Pattern);
        TEXTURE2D(_Palette); SAMPLER(sampler_Palette);
        CBUFFER_START(UnityPerMaterial)
        float _Category;
        float _Density;
        float _Diagnostic;
        float _TrunkTop;
        CBUFFER_END
        struct A { float3 p:POSITION; float3 n:NORMAL; float2 uv:TEXCOORD0; };
        struct V { float4 p:SV_POSITION; float3 w:TEXCOORD0; float3 n:TEXCOORD1; float2 uv:TEXCOORD2; };
        V Vert(A i)
        {
            V o;
            o.w=TransformObjectToWorld(i.p);
            o.p=TransformWorldToHClip(o.w);
            o.n=TransformObjectToWorldNormal(i.n);
            o.uv=i.uv;
            return o;
        }
        half4 Frag(V i):SV_Target
        {
            // Camera is fixed at pitch 30: this projection makes both axes match sprite pixels.
            float2 pixel=floor(float2(i.w.x,dot(i.w,float3(0,0.8660254,0.5)))*_Density+0.0001);
            float noise=SAMPLE_TEXTURE2D(_Pattern,sampler_Pattern,(pixel+0.5)/128).r;
            if (_Diagnostic>1.5)
            {
                float2 cell=_Category<0.5 ? floor(float2(i.w.x*48,i.w.z*24)+0.0001) : pixel;
                float checker=fmod(abs(cell.x+cell.y),2);
                return half4(checker.xxx,1);
            }
            half3 c;
            if (_Category<0.5)
            {
                c=SAMPLE_TEXTURE2D(_Ground,sampler_Ground,i.w.xz/42.6666667+0.5).rgb;
            }
            else if (_Category<1.5)
            {
                half3 old=SAMPLE_TEXTURE2D(_Palette,sampler_Palette,i.uv).rgb;
                bool leaf=i.w.y>_TrunkTop && old.g>old.r*0.88 && old.g>old.b*1.12;
                c=leaf ? (noise<0.25 ? half3(0.19,0.31,0.13) : noise<0.75 ? half3(0.29,0.43,0.18) : half3(0.43,0.57,0.25))
                       : (noise<0.25 ? half3(0.24,0.12,0.07) : noise<0.75 ? half3(0.38,0.23,0.11) : half3(0.52,0.35,0.18));
            }
            else if (_Category<2.5)
            {
                c=noise<0.25 ? half3(0.35,0.31,0.29) : noise<0.75 ? half3(0.49,0.45,0.40) : half3(0.64,0.59,0.51);
            }
            else
            {
                c=noise<0.25 ? half3(0.26,0.41,0.39) : noise<0.75 ? half3(0.34,0.51,0.46) : half3(0.49,0.64,0.55);
            }
            Light sun=GetMainLight(TransformWorldToShadowCoord(i.w));
            // Palette constants are authored as sRGB, like the adopted PNG art.
            // The ground texture is already converted by its sRGB importer.
            if (_Category>0.5) c=SRGBToLinear(c);
            float diffuse=saturate(dot(normalize(i.n),sun.direction));
            float band=diffuse<0.25 ? 0.58 : diffuse<0.68 ? 0.80 : 1.0;
            if (_Diagnostic>0.5)
            {
                return half4(band.xxx,1);
            }
            half3 lighting=lerp(half3(0.40,0.25,0.18),half3(1.0,0.96,0.86)*band,sun.shadowAttenuation);
            return half4(c*lighting,1);
        }
        float3 _LightDirection;
        V Shadow(A i)
        {
            V o=Vert(i);
            o.p=TransformWorldToHClip(ApplyShadowBias(o.w,normalize(o.n),_LightDirection));
            o.p=ApplyShadowClamping(o.p);
            return o;
        }
        half4 Depth(V i):SV_Target { return 0; }
        ENDHLSL
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }
            HLSLPROGRAM
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
            #pragma vertex Shadow
            #pragma fragment Depth
            ENDHLSL
        }
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode"="DepthOnly" }
            ColorMask R
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Depth
            ENDHLSL
        }
    }
}
