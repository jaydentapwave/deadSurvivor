Shader "DeadSurvivor/DQ/Soft foot shadow"
{
    Properties
    {
        _MainTex("Soft coverage",2D)="white"{}
        _BankMask("Support classes",2D)="black"{}
        _FieldRect("Mask world rectangle",Vector)=(-36,-36,72,72)
        _ShadowTint("Full coverage tint",Color)=(.32,.40,.34,1)
        _Diagnostic("Receiver diagnosis only",Float)=0
        _Opacity("Coverage",Range(0,1))=.6
        [HideInInspector] _SrcBlend("Source blend",Float)=2
        [HideInInspector] _DstBlend("Destination blend",Float)=0
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent-10" }
        Pass
        {
            Cull Off ZWrite Off ZTest LEqual Blend [_SrcBlend] [_DstBlend]
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
            TEXTURE2D(_BankMask); SAMPLER(sampler_BankMask);
            CBUFFER_START(UnityPerMaterial)
            float4 _FieldRect, _ShadowTint;
            float _Opacity, _Diagnostic;
            CBUFFER_END
            struct A { float3 p:POSITION; float2 uv:TEXCOORD0; float4 color:COLOR; };
            struct V { float4 p:SV_POSITION; float2 uv:TEXCOORD0; float3 world:TEXCOORD1; float support:TEXCOORD2; };
            V Vert(A a)
            {
                V o; o.world=TransformObjectToWorld(a.p);o.p=TransformWorldToHClip(o.world);o.uv=a.uv;o.support=a.color.r;return o;
            }
            float4 Frag(V i):SV_Target
            {
                float2 screenUV=GetNormalizedScreenSpaceUV(i.p);
                float raw=SampleSceneDepth(screenUV);
                #if !UNITY_REVERSED_Z
                raw=lerp(UNITY_NEAR_CLIP_VALUE,1,raw);
                #endif
                float3 receiver=ComputeWorldSpacePosition(screenUV,raw,UNITY_MATRIX_I_VP);
                float bank=SAMPLE_TEXTURE2D(_BankMask,sampler_PointClamp,(i.world.xz-_FieldRect.xy)/_FieldRect.zw).r;
                float coverage=SAMPLE_TEXTURE2D(_MainTex,sampler_LinearClamp,i.uv).a*_Opacity;
                // Temporary verification material only: actual Main pre-transparent depth sample.
                if (_Diagnostic>3.5 && _Diagnostic<4.5)
                {
                    clip(coverage-.01);
                    return float4(distance(receiver,i.world),_CameraDepthTexture_TexelSize.z,32+abs(bank-i.support),1);
                }
                if (_Diagnostic<.5 || _Diagnostic>2.5) clip(.04-distance(receiver,i.world));
                if (_Diagnostic<1.5 || _Diagnostic>2.5) clip(i.support>.5 ? bank-.5 : .5-bank);
                return half4(lerp(1,_ShadowTint.rgb,saturate(coverage)),1);
            }
            ENDHLSL
        }
    }
}
