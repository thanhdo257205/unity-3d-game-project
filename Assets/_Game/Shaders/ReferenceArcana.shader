Shader "PhamKiemLuc/ReferenceArcana"
{
    Properties { _Tint("Tint", Color)=(.6,.15,1,.7) _Fade("Fade",Float)=1 _Phase("Phase",Float)=0 _Mode("Sun / Mirror / Shell",Float)=0 }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct A { float4 vertex:POSITION; float3 normal:NORMAL; float2 uv:TEXCOORD0; };
            struct V { float4 position:SV_POSITION; float3 world:TEXCOORD0; float3 normal:TEXCOORD1; float2 uv:TEXCOORD2; };
            CBUFFER_START(UnityPerMaterial)
            half4 _Tint; float _Fade; float _Phase; float _Mode;
            CBUFFER_END
            V vert(A i) { V o;o.position=TransformObjectToHClip(i.vertex.xyz);o.world=TransformObjectToWorld(i.vertex.xyz);o.normal=TransformObjectToWorldNormal(i.normal);o.uv=i.uv;return o; }
            half4 frag(V i):SV_Target
            {
                float2 p=i.uv*2-1;float r=length(p);float a=atan2(p.y,p.x);float t=_Phase;
                float noise=sin(a*29+t*2+sin(a*11-t*3))*sin(a*13-t*1.7);
                if(_Mode<.5)
                {
                    float edge=exp(-pow((r-.88)*18,2));
                    float flare=exp(-pow((r-(.90+noise*.045))*24,2));
                    float stars=pow(saturate(sin(a*73+t*2)),16)*smoothstep(.65,.98,r);
                    float clouds=sin(p.x*19+sin(p.y*13+t*.7))*sin(p.y*23+sin(p.x*17-t));
                    float grains=frac(sin(dot(floor(p*115),float2(127.1,311.7)))*43758.5453);
                    float star=step(.996,grains)*(1-smoothstep(.2,.9,r))*.6;
                    float crescent=smoothstep(1.6,2.6,t)*smoothstep(-.3,.4,p.x)*(1-smoothstep(.7,.9,r));
                    half3 black=half3(.012,.004,.025)+half3(.08,.018,.11)*saturate(clouds)*.4;
                    black=lerp(black,_Tint.rgb*(.28+saturate(clouds)*.65),crescent);
                    half3 color=lerp(black,_Tint.rgb*.7,saturate(edge*.7+flare*.8+stars*.18));
                    color+=star*half3(.7,.4,.9);
                    return half4(color, saturate((1-smoothstep(.93,1,r))*.96)*_Fade);
                }
                if(_Mode<1.5)
                {
                    float rim=exp(-pow((r-.84)*35,2));
                    float rune=pow(saturate(sin(r*56-t*2+sin(a*12)*.3)),12)*.35;
                    float spiral=pow(saturate(sin(a*7+r*19-t)),18)*.45;
                    float alpha=(.13+rim*.65+rune+spiral)*(1-smoothstep(.92,1,r));
                    return half4(_Tint.rgb*(.5+rim*.6),alpha*_Tint.a*_Fade);
                }
                float rim=pow(1-saturate(abs(dot(normalize(i.normal),GetWorldSpaceNormalizeViewDir(i.world)))),2);
                float weave=pow(saturate(sin(i.uv.x*95+sin(i.uv.y*37+t))),18)*.2;
                return half4(_Tint.rgb*(.55+rim*.4),(_Tint.a*.12+rim*_Tint.a*.55+weave*_Tint.a)*_Fade);
            }
            ENDHLSL
        }
    }
}
