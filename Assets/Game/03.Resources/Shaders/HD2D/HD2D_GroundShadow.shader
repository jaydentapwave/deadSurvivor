Shader "HD2D/HD2D_GroundShadow"
{
    Properties { [PerRendererData] _MainTex("Silhouette",2D)="white" {} _BankMask("Support height mask",2D)="black" {} _UseBankMask("Use bank mask",Float)=0 }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Geometry-5" }
        Pass
        {
            Cull Off ZWrite Off ZTest Always Blend DstColor Zero
            Stencil { Ref 1 ReadMask 1 WriteMask 1 Comp NotEqual Pass Replace }
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
            TEXTURE2D(_BankMask); SAMPLER(sampler_BankMask);
            float4 _Contact;
            float _UseBankMask;
            struct A { float3 p:POSITION; float2 uv:TEXCOORD0; };
            struct V { float4 p:SV_POSITION; float2 uv:TEXCOORD0; float3 world:TEXCOORD1; };
            V Vert(A i)
            {
                V o; o.world=TransformObjectToWorld(i.p); o.p=TransformWorldToHClip(o.world); o.uv=i.uv; return o;
            }
            half4 Frag(V i):SV_Target
            {
                if (i.uv.x>=0) clip(SAMPLE_TEXTURE2D(_MainTex,sampler_PointClamp,i.uv).a-.5);
                if (_UseBankMask>.5)
                {
                    float bank=SAMPLE_TEXTURE2D(_BankMask,sampler_PointClamp,(i.world.xz+36)/72).r;
                    clip(_Contact.w>.1 ? bank-.5 : .5-bank);
                }
                if (_Contact.z>0) clip(_Contact.z-distance(i.world.xz,_Contact.xy));
                return half4(.44,.43,.39,1);
            }
            ENDHLSL
        }
    }
}
