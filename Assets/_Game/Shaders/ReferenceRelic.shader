Shader "PhamKiemLuc/ReferenceRelic"
{
    Properties { [HDR] _BaseColor("Relic metal",Color)=(.72,.4,.09,1) }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry" }
        Pass
        {
            Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct A { float4 vertex:POSITION; float3 normal:NORMAL; };
            struct V { float4 position:SV_POSITION; float3 normal:TEXCOORD0; float3 world:TEXCOORD1; };
            CBUFFER_START(UnityPerMaterial)
            half4 _BaseColor;
            CBUFFER_END
            V vert(A i) { V o;o.position=TransformObjectToHClip(i.vertex.xyz);o.normal=TransformObjectToWorldNormal(i.normal);o.world=TransformObjectToWorld(i.vertex.xyz);return o; }
            half4 frag(V i):SV_Target
            {
                float3 n=normalize(i.normal),v=GetWorldSpaceNormalizeViewDir(i.world);
                float face=abs(dot(n,normalize(float3(.3,.8,-.4))));
                float rim=pow(1-saturate(abs(dot(n,v))),2);
                float metal=pow(saturate(abs(dot(reflect(-normalize(float3(.3,.8,-.4)),n),v))),12);
                return half4(_BaseColor.rgb*(1.45+face*.65+rim*.5)+metal*_BaseColor.rgb*.3,1);
            }
            ENDHLSL
        }
    }
}
