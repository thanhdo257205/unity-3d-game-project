Shader "PhamKiemLuc/KimLienGold"
{
    Properties
    {
        _BaseColor("Antique gold",Color)=(0.55,0.23,0.035,1)
        [HDR] _GlowColor("Living veins",Color)=(2,0.85,0.12,1)
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" }
        Pass
        {
            Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct Attributes { float4 positionOS:POSITION; float3 normalOS:NORMAL; float2 uv:TEXCOORD0; };
            struct Varyings { float4 positionCS:SV_POSITION; float3 normalWS:TEXCOORD0; float3 positionWS:TEXCOORD1; float2 uv:TEXCOORD2; float3 positionOS:TEXCOORD3; };
            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half4 _GlowColor;
            CBUFFER_END
            Varyings vert(Attributes v)
            {
                Varyings o; o.positionCS=TransformObjectToHClip(v.positionOS.xyz);
                o.positionWS=TransformObjectToWorld(v.positionOS.xyz);
                o.normalWS=TransformObjectToWorldNormal(v.normalOS);o.uv=v.uv;o.positionOS=v.positionOS.xyz;return o;
            }
            half4 frag(Varyings i):SV_Target
            {
                float3 normal=SafeNormalize(i.normalWS);
                float3 view=GetWorldSpaceNormalizeViewDir(i.positionWS);float3 light=normalize(float3(0.4,0.8,-0.3));
                float diffuse=0.3+0.8*abs(dot(normal,light));
                float rim=pow(1-saturate(abs(dot(normal,view))),3);
                float specular=pow(saturate(abs(dot(reflect(-light,normal),view))),28)*0.4;
                float across=i.uv.x*2-1;
                float midrib=exp(-abs(across)*32)*sin(i.uv.y*PI);
                float veins=pow(saturate(cos((across*6+sin(i.uv.y*8)*0.18)*PI)),22)*sin(i.uv.y*PI);
                float core=exp(-dot(i.positionOS.xz,i.positionOS.xz)*95);
                float pulse=0.9+0.1*sin(i.uv.y*12-_Time.y*4);
                float3 color=_BaseColor.rgb*diffuse+specular*float3(0.9,0.6,0.25);
                color+=_GlowColor.rgb*(rim*0.28+midrib*0.32+veins*0.09+core*0.8)*pulse;
                return half4(color,1);
            }
            ENDHLSL
        }
    }
}
