// Adapted from Assets/Game/03.Resources/Art/HD2D/Round6/FootShadow.shader SHA256 d08cc809ef0284e4e7328ad11e3e2d5699b7784aa4a3cba79a25389dda9f53ca
Shader "HD2D/HD2D_ContactShadow"
{
    Properties { [PerRendererData] _MainTex("Silhouette",2D)="white" {} }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" }
        Pass
        {
            Cull Off ZWrite Off Blend DstColor Zero
            Stencil { Ref 1 ReadMask 1 WriteMask 1 Comp NotEqual Pass Replace }
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
            float4 _Contact;
            struct A { float3 p:POSITION; float2 uv:TEXCOORD0; };
            struct V { float4 p:SV_POSITION; float2 uv:TEXCOORD0; float3 world:TEXCOORD1; };
            V Vert(A i)
            {
                V o; o.world=TransformObjectToWorld(i.p); o.p=TransformWorldToHClip(o.world); o.uv=i.uv; return o;
            }
            half4 Frag(V i):SV_Target
            {
                if (i.uv.x>=0) clip(SAMPLE_TEXTURE2D(_MainTex,sampler_PointClamp,i.uv).a-.5);
                float raw=SampleSceneDepth(i.p.xy/_ScaledScreenParams.xy);
                #if UNITY_REVERSED_Z
                    raw=1-raw;
                #endif
                float receiver=lerp(_ProjectionParams.y,_ProjectionParams.z,raw);
                float shadowDepth=-TransformWorldToView(i.world).z;
                if (distance(i.world.xz,_Contact.xy)>.4) clip(.04-abs(receiver-shadowDepth));
                if (_Contact.z>0) clip(_Contact.z-distance(i.world.xz,_Contact.xy));
                return half4(.44,.43,.39,1);
            }
            ENDHLSL
        }
    }
}
