// Cổng không gian của Phá Không (Phong Lôi Sí): hình bầu dục đứng, viền là vòng sét vàng trắng xoáy,
// bên trong là màn xoáy xanh thẫm có ánh sao, làm méo hình ảnh phía sau (đọc ảnh nền từ Opaque Texture của URP).
// Dùng trên một tấm quad (UV 0-1). _Open (0-1): cổng mở ra từ tâm / co lại thành một điểm.
Shader "PhamKiemLuc/CongKhongGian"
{
    Properties
    {
        _Open("Mở (0-1)", Range(0, 1)) = 1
        _Intensity("Độ sáng viền sét", Float) = 1
        [HDR] _RingColor("Màu viền sét", Color) = (3.2, 2.6, 0.9, 1)
        _DeepColor("Màu xoáy sâu", Color) = (0.01, 0.04, 0.16, 1)
        [HDR] _GlowColor("Màu xoáy sáng", Color) = (0.08, 0.45, 0.95, 1)
        _Distort("Độ méo hình nền", Float) = 0.05
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" }

        Pass
        {
            Name "CongKhongGian"
            Tags { "LightMode" = "UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareOpaqueTexture.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float4 screenPos : TEXCOORD1;
            };

            CBUFFER_START(UnityPerMaterial)
                float _Open;
                float _Intensity;
                float4 _RingColor;
                float4 _DeepColor;
                float4 _GlowColor;
                float _Distort;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                o.uv = input.uv;
                o.screenPos = ComputeScreenPos(o.positionCS);
                return o;
            }

            float hash21(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            float vnoise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                f = f * f * (3.0 - 2.0 * f);
                float a = hash21(i);
                float b = hash21(i + float2(1, 0));
                float c = hash21(i + float2(0, 1));
                float d = hash21(i + float2(1, 1));
                return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
            }

            float fbm(float2 p)
            {
                float v = 0.0;
                float a = 0.5;
                for (int k = 0; k < 4; k++)
                {
                    v += a * vnoise(p);
                    p *= 2.03;
                    a *= 0.5;
                }
                return v;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float open = saturate(_Open);
                float2 p = (i.uv - 0.5) * 2.0;
                p /= max(open, 0.001);                       // cổng lớn dần từ tâm
                float r = length(p);
                float ang = atan2(p.y, p.x);
                float t = _Time.y;

                // Màn xoáy bên trong
                float sw = ang + r * 3.2 - t * 2.4;
                float2 sp = float2(cos(sw), sin(sw)) * r;
                float n = fbm(sp * 2.4 + t * 0.35);
                float n2 = fbm(sp * 2.4 + 7.3 - t * 0.2);
                float3 swirl = lerp(_DeepColor.rgb, _GlowColor.rgb, n * n * (1.2 - r * 0.7));

                // Ánh sao nhấp nháy
                float2 cell = floor(p * 26.0);
                float star = step(0.975, hash21(cell)) * (0.5 + 0.5 * sin(t * 6.0 + hash21(cell + 3.1) * 40.0));
                swirl += star * float3(1.6, 1.8, 2.2);

                // Làm méo hình ảnh phía sau
                float2 suv = i.screenPos.xy / max(i.screenPos.w, 0.0001);
                float2 offs = float2(n - 0.5, n2 - 0.5) * _Distort * (1.0 - saturate(r));
                float3 bg = SampleSceneColor(suv + offs);
                float3 inner = lerp(bg * 0.45, swirl, 0.72);

                // Viền: vòng sét vàng trắng xoáy
                float ringIn = 0.8;
                float ringOut = 0.95;
                float band = smoothstep(ringIn - 0.05, ringIn + 0.03, r) * (1.0 - smoothstep(ringOut - 0.03, ringOut, r));
                float e = fbm(float2(ang * 3.0 + t * 7.0, t * 3.0 + r * 5.0));
                float bolt = smoothstep(0.35, 0.8, e);
                float flick = 0.45 + 1.1 * bolt;
                float3 ring = _RingColor.rgb * band * flick * _Intensity;

                // Quầng sáng mờ ngoài viền
                float halo = exp(-max(r - ringOut, 0.0) * 18.0) * step(ringOut, r) * 0.6;
                float3 col = inner * (1.0 - band) + ring + _RingColor.rgb * halo * 0.25 * _Intensity;

                float a = (1.0 - smoothstep(0.985, 1.0, r)) * saturate(open * 5.0);
                a = max(a * (1.0 - halo * 0.0), 0.0);
                return half4(col, a);
            }
            ENDHLSL
        }
    }
}
