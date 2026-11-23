// Camera-attached far landscape strip (mountains, sea, haze). Opaque and depth-writing so nearer
// ground and forest cover its lower edge. The painting scrolls with a small share of the camera's
// world x so it reads as very far away instead of a fixed overlay.
Shader "DeadSurvivor/DQ/Far backdrop"
{
    Properties
    {
        _MainTex("Painted far landscape (wraps horizontally)",2D)="white" {}
        _Rows("Image v at card bottom (x) and top (y)",Vector)=(0,1,0,0)
        _Tile("Card metres per image width",Float)=24
        _Parallax("Share of camera x the far land moves",Float)=.1
        _Tint("Colour multiplier",Color)=(1,1,1,1)
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry+50" }
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }
            Cull Off ZWrite On ZTest LEqual
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
            CBUFFER_START(UnityPerMaterial)
            float4 _MainTex_ST; float4 _Rows; float _Tile; float _Parallax; half4 _Tint;
            CBUFFER_END
            struct A { float3 p:POSITION; float2 uv:TEXCOORD0; };
            struct V { float4 p:SV_POSITION; float2 uv:TEXCOORD0; };
            V Vert(A i)
            {
                V o; o.p=TransformObjectToHClip(i.p);
                // Quad local x in [-.5,.5] times object scale x gives card metres.
                float width=length(float3(UNITY_MATRIX_M._m00,UNITY_MATRIX_M._m10,UNITY_MATRIX_M._m20));
                float metres=i.p.x*width-_WorldSpaceCameraPos.x*_Parallax;
                o.uv=float2(metres/_Tile,lerp(_Rows.x,_Rows.y,i.uv.y));
                return o;
            }
            half4 Frag(V i):SV_Target
            {
                return half4(SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex,i.uv).rgb*_Tint.rgb,1);
            }
            ENDHLSL
        }
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode"="DepthOnly" }
            ColorMask R
            Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct A { float3 p:POSITION; };
            float4 Vert(A i):SV_POSITION { return TransformObjectToHClip(i.p); }
            half4 Frag():SV_Target { return 0; }
            ENDHLSL
        }
    }
}
