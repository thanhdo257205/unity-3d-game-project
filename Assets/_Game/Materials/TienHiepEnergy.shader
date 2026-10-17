Shader "PhamKiemLuc/TienHiepEnergy"
{
    Properties
    {
        [HDR] _BaseColor("Tint",Color)=(1,1,1,1)
        _Intensity("Radiance",Float)=2
        _Mode("Ribbon / ward / core / mist",Float)=0
        _HitPosition("Impact",Vector)=(0,0,0,0)
        _HitTime("Impact time",Float)=-100
    }
    SubShader
    {
        Tags {"RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent"}
        Pass
        {
            Blend SrcAlpha One
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct Attributes {float4 positionOS:POSITION;float3 normalOS:NORMAL;float2 uv:TEXCOORD0;half4 color:COLOR;};
            struct Varyings {float4 positionCS:SV_POSITION;float3 positionWS:TEXCOORD0;float3 normalWS:TEXCOORD1;float2 uv:TEXCOORD2;half4 color:COLOR;};
            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;float _Intensity;float _Mode;float4 _HitPosition;float _HitTime;
            CBUFFER_END
            float hash(float2 p){p=frac(p*float2(123.34,345.45));p+=dot(p,p+34.345);return frac(p.x*p.y);}
            float noise(float2 p)
            {
                float2 i=floor(p),f=frac(p);f=f*f*(3-2*f);
                return lerp(lerp(hash(i),hash(i+float2(1,0)),f.x),lerp(hash(i+float2(0,1)),hash(i+1),f.x),f.y);
            }
            float fbm(float2 p){return noise(p)*0.57+noise(p*2.07+4.7)*0.28+noise(p*4.13-3.2)*0.15;}
            Varyings vert(Attributes v)
            {
                Varyings o;o.positionCS=TransformObjectToHClip(v.positionOS.xyz);o.positionWS=TransformObjectToWorld(v.positionOS.xyz);
                o.normalWS=TransformObjectToWorldNormal(v.normalOS);o.uv=v.uv;o.color=v.color*_BaseColor;return o;
            }
            half4 frag(Varyings i):SV_Target
            {
                float time=_Time.y;
                float flow=fbm(float2(i.uv.x*9-time*0.7,i.uv.y*6-time*1.2));
                float alpha=0;float light=1;
                if(_Mode>0.5 && _Mode<1.5)
                {
                    float rim=pow(1-saturate(abs(dot(SafeNormalize(i.normalWS),GetWorldSpaceNormalizeViewDir(i.positionWS)))),1.8);
                    float wisps=smoothstep(0.42,0.78,flow+0.12*sin(i.uv.y*22-time*3));
                    float ends=smoothstep(0,0.10,i.uv.y)*(1-smoothstep(0.72,1,i.uv.y));
                    float age=time-_HitTime;
                    float wave=age>=0 && age<1.1?exp(-pow(distance(i.positionWS,_HitPosition.xyz)-age*7,2)*9)*exp(-age*3):0;
                    alpha=(0.035+rim*0.22+wisps*0.18+wave*0.35)*ends;
                    light=0.65+wisps*0.9+wave*1.1;
                }
                else if(_Mode>1.5 && _Mode<2.5)
                {
                    float rim=pow(1-saturate(abs(dot(SafeNormalize(i.normalWS),GetWorldSpaceNormalizeViewDir(i.positionWS)))),2);
                    alpha=(0.15+rim*0.4)*smoothstep(0.14,0.6,flow);light=0.8+rim;
                }
                else if(_Mode>2.5)
                {
                    float2 p=(i.uv-0.5)*2;float r=length(p);
                    alpha=exp(-dot(p,p)*2.7)*saturate(1-r)*smoothstep(0.1,0.65,flow);light=0.7+flow*0.4;
                }
                else
                {
                    float across=pow(saturate(1-abs(i.uv.y*2-1)),1.6);
                    alpha=across*smoothstep(0.17,0.7,flow);light=0.75+flow*0.6;
                }
                return half4(i.color.rgb*_Intensity*light,saturate(alpha*i.color.a));
            }
            ENDHLSL
        }
    }
}
