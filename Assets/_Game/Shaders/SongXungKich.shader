// Sóng xung kích của Phá Không: một VÒNG SÁNG nở ra từ tâm tấm (cộng sáng), vàng kim ở mép ngoài, xanh lơ ở mép trong, mờ dần về 0 trước mép tấm.
// Dùng trên một tấm quad quay về camera (UV 0-1); _T (0-1) là tiến độ nở: vòng ở bán kính _T (1 = mép tấm), mỏng dần và nhạt dần khi nở.
// Tính thẳng trong shader (không dùng ảnh) nên không lộ cạnh thẳng.
Shader "PhamKiemLuc/SongXungKich"
{
    Properties
    {
        _T("Tiến độ nở (0-1)", Range(0, 1)) = 0
        _Alpha("Độ đậm chung", Range(0, 1)) = 1
        [HDR] _ColorOuter("Màu mép ngoài", Color) = (3.2, 2.2, 0.6, 1)
        [HDR] _ColorInner("Màu mép trong", Color) = (0.6, 1.8, 3.0, 1)
        _Thickness("Độ dày vòng lúc đầu (tỉ lệ bán kính)", Range(0.02, 0.5)) = 0.16
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent+10" "RenderPipeline" = "UniversalPipeline" }

        Pass
        {
            Name "SongXungKich"
            Blend One One
            ZWrite Off
            ZTest LEqual
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float _T, _Alpha, _Thickness;
                float4 _ColorOuter, _ColorInner;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };

            Varyings vert(Attributes i)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(i.positionOS.xyz);
                o.uv = i.uv;
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float r = length(i.uv - 0.5) * 2.0;                 // 0 tâm, 1 giữa cạnh tấm
                float t = saturate(_T);
                float ease = 1.0 - pow(1.0 - t, 2.0);                // nở nhanh lúc đầu, chậm dần
                float ringR = ease * 0.92;
                float th = _Thickness * (1.0 - 0.6 * t) * max(ringR, 0.05) + 0.01;
                float d = (r - ringR) / th;                          // < 0 phía trong vòng, > 0 phía ngoài
                float ring = exp(-d * d * 1.6);                      // đỉnh sáng ở đúng bán kính vòng
                float inner = smoothstep(0.0, 1.0, saturate(-d)) * exp(-(-d) * 0.9) * 0.35;   // vệt mờ phía trong vòng
                float3 col = lerp(_ColorOuter.rgb, _ColorInner.rgb, saturate(0.5 - d * 0.5));
                float fade = (1.0 - t) * (1.0 - t * 0.3);            // nhạt dần khi nở
                float edge = 1.0 - smoothstep(0.82, 1.0, r);        // về đúng 0 trước mép tấm
                float w = (ring + inner) * fade * edge * _Alpha;
                return half4(col * w, 1.0);
            }
            ENDHLSL
        }
    }
}
