Shader "HD2D/Round8/FieldSurface"
{
    Properties
    {
        _Ground("Original field",2D)="white" {}
        _Water("Original water",2D)="white" {}
        _Pattern("Point minerals",2D)="white" {}
        _Soil("Point soil and stones",2D)="white" {}
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
        TEXTURE2D(_Soil); SAMPLER(sampler_Soil);
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
            float3 cellNormal=normalize(i.n);
            float d=saturate(dot(cellNormal,sun.direction));
            float band=d<.25 ? .60 : d<.68 ? .82 : 1;
            if (_Diagnostic>1.5) return _Category>2.5 && _Category<3.5 ? (i.n.y>.7 ? half4(1,0,0,1) : i.w.y>.31 ? half4(0,1,0,1) : half4(0,0,1,1)) : half4(0,0,0,1);
            if (_Diagnostic>.5) return half4(SRGBToLinear(band.xxx),1);
            half3 c=grass;
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
                float2 soilUV=(floor(float2(i.w.x+i.w.z,i.w.y)*48)+.5)/128;
                half3 soil=LinearToSRGB(SAMPLE_TEXTURE2D(_Soil,sampler_Soil,soilUV).rgb);
                float finger=fmod(abs(cells.x+floor(cells.x/5)*3),9)<3 ? .055 : .018;
                if (i.n.y>.7) c=grass;
                else if (i.w.y>.36-finger) c=grass;
                else if (i.w.y>.36-finger-1.0/48) c=saturate(grass*1.2);
                else
                {
                    float row=floor(i.w.y*24);
                    float bandSide=row<3 ? .7 : row<6 ? .88 : 1.05;
                    c=soil*bandSide;
                }
            }
            else if (_Category>3.5)
            {
                float rank=(d<.25 ? 0 : d<.68 ? 1 : 2);
                float stripe=fmod(cells.x+floor(cells.y/9),7);
                if (stripe<1) rank=max(0,rank-1);
                c=rank<.5 ? half3(66,52,32)/255 : rank<1.5 ? half3(111,94,62)/255 : half3(168,165,133)/255;
            }
            float shadow=(_Category>.5 && _Category<1.5) ? 1 : _Category>2.5 && _Category<3.5 && i.n.y<.7 ? 1 : step(.5,sun.shadowAttenuation);
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
            Name "DepthNormals"
            Tags { "LightMode"="DepthNormalsOnly" }
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Normals
            half4 Normals(V i):SV_Target {return half4(normalize(i.n),0);}
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
