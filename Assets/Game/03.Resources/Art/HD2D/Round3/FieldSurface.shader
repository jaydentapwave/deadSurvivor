Shader "HD2D/Round3/FieldSurface"
{
    Properties
    {
        _Ground("Original field",2D)="white" {}
        _Water("Original water",2D)="white" {}
        _Pattern("Point grain",2D)="white" {}
        _Category("0 floor, 1 stone, 2 water, 3 ledge, 4 wood",Float)=0
        _Diagnostic("Lighting factor only",Float)=0
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" }
        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
        TEXTURE2D(_Ground); SAMPLER(sampler_Ground);
        TEXTURE2D(_Water); SAMPLER(sampler_Water);
        TEXTURE2D(_Pattern); SAMPLER(sampler_Pattern);
        CBUFFER_START(UnityPerMaterial)
        float _Category;
        float _Diagnostic;
        CBUFFER_END
        struct A { float3 p:POSITION; float3 n:NORMAL; };
        struct V { float4 p:SV_POSITION; float3 w:TEXCOORD0; float3 n:TEXCOORD1; };
        V Vert(A i)
        {
            V o; o.w=TransformObjectToWorld(i.p); o.n=TransformObjectToWorldNormal(i.n); o.p=TransformWorldToHClip(o.w); return o;
        }
        half4 Frag(V i):SV_Target
        {
            float2 cells=floor(float2(i.w.x,dot(i.w,float3(0,.8660254,.5)))*48+.00001);
            float grain=SAMPLE_TEXTURE2D(_Pattern,sampler_Pattern,(cells+.5)/128).r;
            float2 floorUV=i.w.xz/42.6666667+.5;
            half3 grass=LinearToSRGB(SAMPLE_TEXTURE2D(_Ground,sampler_Ground,floorUV).rgb);
            Light sun=GetMainLight(TransformWorldToShadowCoord(i.w));
            float d=saturate(dot(normalize(i.n),sun.direction));
            float band=d<.25 ? .60 : d<.68 ? .82 : 1;
            if (_Diagnostic>1.5) return _Category>2.5 && _Category<3.5 ? (i.n.y>.7 ? half4(1,0,0,1) : i.w.y>.405 ? half4(0,1,0,1) : half4(0,0,1,1)) : half4(0,0,0,1);
            if (_Diagnostic>.5) return half4(SRGBToLinear(band.xxx),1);
            half3 c=grass;
            if (_Category>.5 && _Category<1.5)
            {
                float rank=(d<.25 ? 0 : d<.68 ? 1 : 2)+(grain>.8 ? 1 : grain<.1 ? -1 : 0);
                c=rank<.5 ? half3(105,104,80)/255 : rank<1.5 ? half3(168,164,127)/255 : half3(216,211,169)/255;
                // Thin mortar marks are painted into the grain, not extra geometry.
                float2 joint=fmod(cells+float2(floor(cells.y/7)*5,0),float2(13,7));
                if (joint.y<1 && grain<.15) c=half3(69,64,45)/255;
            }
            else if (_Category>1.5 && _Category<2.5)
            {
                c=LinearToSRGB(SAMPLE_TEXTURE2D(_Water,sampler_Water,floorUV).rgb);
            }
            else if (_Category>2.5 && _Category<3.5)
            {
                if (i.n.y>.7) c=grass;
                else if (i.w.y>.405) c=saturate(grass*1.3);
                else c=(grain<.15 ? half3(82,79,53) : grain>.8 ? half3(59,57,41) : half3(65,61,40))/255;
            }
            else if (_Category>3.5)
            {
                float rank=(d<.25 ? 0 : d<.68 ? 1 : 2);
                float stripe=fmod(cells.x+floor(cells.y/9),7);
                if (stripe<1) rank=max(0,rank-1);
                c=rank<.5 ? half3(66,52,32)/255 : rank<1.5 ? half3(111,94,62)/255 : half3(168,165,133)/255;
            }
            float shadow=_Category>2.5 && _Category<3.5 && i.n.y<.7 ? 1 : sun.shadowAttenuation;
            c*=lerp(half3(.44,.43,.39),half3(1,1,1),shadow);
            return half4(SRGBToLinear(c),1);
        }
        float3 _LightDirection;
        V Shadow(A i)
        {
            V o=Vert(i);o.p=TransformWorldToHClip(ApplyShadowBias(o.w,normalize(o.n),_LightDirection));o.p=ApplyShadowClamping(o.p);return o;
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
