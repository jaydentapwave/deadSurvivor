Shader "HD2D/Round2/BrownHull"
{
    Properties { _Color("Brown outline",Color)=(0.204,0.078,0.055,1) _Width("World width, one art texel",Float)=0.020833333 }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry-1" }
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            Cull Front ZWrite On
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
            half4 _Color;
            float _Width;
            CBUFFER_END
            float4 Vert(float3 p:POSITION,float3 n:NORMAL):SV_POSITION
            {
                float3 w=TransformObjectToWorld(p);
                float3 normal=TransformObjectToWorldNormal(n);
                // Extrude in the camera plane: 1/48m = one source sprite pixel in projection.
                float3 side=UNITY_MATRIX_I_V._m00_m10_m20;
                float3 up=UNITY_MATRIX_I_V._m01_m11_m21;
                float2 outward=normalize(float2(dot(normal,side),dot(normal,up))+float2(0.00001,0.00001));
                w+=(side*outward.x+up*outward.y)*_Width;
                return TransformWorldToHClip(w);
            }
            half4 Frag():SV_Target { return _Color; }
            ENDHLSL
        }
    }
}
