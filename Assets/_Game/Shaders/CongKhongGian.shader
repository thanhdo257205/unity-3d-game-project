// Cổng không gian của Phá Không (Phong Lôi Sí) theo phim: một XOÁY TRÒN vuông góc hướng bay, nhìn được cả hai mặt, rìa tan dần vào không khí (không khung, không nét bao).
// Shader này vẽ MỘT LỚP của cổng (chọn bằng _Layer) trên một tấm quad; script CongKhongGian đặt ba tấm lệch nhau dọc trục cổng để cổng có chiều sâu (nhìn chéo thấy như cái phễu):
//  _Layer = 0  VÒNG GIỮA (ở z = 0): màu xanh lơ sáng pha trắng, ĐẶC (không nhìn xuyên được), _Arms cánh xoáy bị nhiễu xé thành nhiều sợi mảnh dày mỏng không đều,
//              tọa độ xoắn theo bán kính, quay theo _Angle, nhiễu RidgedPerlin + CellularNoise01 trôi vào tâm; kèm cảnh phía sau bị xoáy méo nhẹ NGAY TRONG phạm vi vòng lửa.
//  _Layer = 1  LÕI (lùi ra sau): lỗ xanh đen mép đa giác gãy góc như mảnh kính vỡ, viền lỗ sáng trắng xanh, bên trong lấp lánh mờ.
//  _Layer = 2  VÒNG LỬA (nhô ra trước): lửa vàng kim ngả cam, mép lởm chởm, bập bùng, CỘNG SÁNG THUẦN (không làm tối nền) nên tan dần vào không khí mà không có nét bao sẫm;
//              kèm điểm sáng trắng lúc mở/đóng (_Flash).
// Nửa tấm = 1 / _PortalFrac bán kính cổng, nên phần rìa tấm luôn có độ đậm bằng 0 (không lộ cạnh thẳng). Trộn "premultiplied" (One OneMinusSrcAlpha).
Shader "PhamKiemLuc/CongKhongGian"
{
    Properties
    {
        _NoiseA("Nhiễu sợi (RidgedPerlin)", 2D) = "gray" {}
        _NoiseB("Nhiễu tế bào (CellularNoise01)", 2D) = "gray" {}
        _Layer("Lớp vẽ: 0 vòng giữa, 1 lõi, 2 vòng lửa, 3 méo cảnh sau", Range(0, 3)) = 0
        _Open("Độ nở của xoáy (0-1; 0 = một điểm)", Range(0, 1)) = 1
        _Fire("Độ hiện của vòng lửa ngoài (0-1)", Range(0, 1)) = 1
        _Flash("Điểm sáng trắng ở tâm (0-1)", Range(0, 1)) = 0
        _Angle("Góc quay hiện tại (radian)", Float) = 0
        _PortalFrac("Bán kính cổng / nửa tấm quad", Float) = 0.7273
        _Arms("Số cánh xoáy", Range(3, 9)) = 6
        _Twist("Độ xoắn cánh theo bán kính (radian)", Float) = 3.4
        _Inward("Tốc độ nhiễu trôi vào tâm", Float) = 0.35
        _CoreRadius("Bán kính lõi / bán kính cổng", Range(0.1, 0.5)) = 0.28
        _CoreJag("Độ gãy góc của mép lõi", Range(0, 0.6)) = 0.3
        [HDR] _FireA("Lửa: cam vàng", Color) = (3.4, 1.3, 0.25, 1)
        [HDR] _FireB("Lửa: vàng kim sáng", Color) = (3.6, 2.7, 0.9, 1)
        [HDR] _CloudFill("Mây: nền xanh lơ giữa các sợi", Color) = (0.05, 0.42, 0.95, 1)
        [HDR] _CloudBlue("Mây: xanh lơ sáng", Color) = (0.3, 1.0, 1.9, 1)
        [HDR] _CloudWhite("Mây: trắng ngả xanh", Color) = (1.5, 1.8, 2.0, 1)
        [HDR] _RimColor("Viền lỗ: trắng xanh", Color) = (2.2, 2.8, 3.4, 1)
        _Intensity("Độ sáng chung", Float) = 1
        _Distort("Độ xoáy méo cảnh sau trong vòng lửa (radian)", Float) = 0.06
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" }

        Pass
        {
            Name "CongKhongGian"
            Tags { "LightMode" = "UniversalForward" }
            Blend One OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareOpaqueTexture.hlsl"

            TEXTURE2D(_NoiseA); SAMPLER(sampler_NoiseA);
            TEXTURE2D(_NoiseB); SAMPLER(sampler_NoiseB);

            CBUFFER_START(UnityPerMaterial)
                float _Layer, _Open, _Fire, _Flash, _Angle, _PortalFrac, _Arms, _Twist, _Inward, _CoreRadius, _CoreJag, _Intensity, _Distort;
                float4 _FireA, _FireB, _CloudFill, _CloudBlue, _CloudWhite, _RimColor;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float4 screenPos : TEXCOORD1;
                float4 centerPos : TEXCOORD2;
            };

            Varyings vert(Attributes input)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                o.uv = input.uv;
                o.screenPos = ComputeScreenPos(o.positionCS);
                // Tâm cổng trên màn hình lấy theo tâm của chính tấm này (các tấm chỉ lệch nhau dọc trục nên gần như trùng)
                o.centerPos = ComputeScreenPos(TransformObjectToHClip(float3(0, 0, 0)));
                return o;
            }

            float hash11(float n) { return frac(sin(n * 127.1) * 43758.5453); }

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

            // Xếp chồng một lớp (premultiplied) lên lớp đã có
            float4 over(float4 top, float4 bottom)
            {
                return float4(top.rgb + bottom.rgb * (1.0 - top.a), top.a + bottom.a * (1.0 - top.a));
            }

            half4 frag(Varyings i) : SV_Target
            {
                float t = _Time.y;
                float2 p = (i.uv - 0.5) * 2.0 / _PortalFrac;       // 1 = bán kính ngoài của cổng
                float rn = length(p);
                float edgeFade = 1.0 - smoothstep(1.16, 1.32, rn);   // về đúng 0 trước mép tấm
                if (edgeFade <= 0.0001) return half4(0, 0, 0, 0);

                float s = max(_Open, 0.001);
                float2 q = p / s;                                    // xoáy nở dần từ tâm
                float r = length(q);
                float2 dir = q / max(r, 1e-4);
                float ang = atan2(q.y, q.x);
                float4 acc = float4(0, 0, 0, 0);

                // Mép lõi: đa giác 9 đỉnh gãy góc, xoay chậm theo xoáy
                float k = ((ang + _Angle * 0.3) / 6.2831853 + 1.0) * 9.0;
                float i0 = floor(k);
                float fk = frac(k);
                float h0 = hash11(fmod(i0, 9.0) + 3.1);
                float h1 = hash11(fmod(i0 + 1.0, 9.0) + 3.1);
                float jag = lerp(h0, h1, fk);                        // nội suy tuyến tính: cạnh thẳng, góc nhọn
                float Rc = _CoreRadius * (1.0 + _CoreJag * (jag - 0.5) * 2.0);

                if (_Layer > 2.5)
                {
                    // ===== LỚP MÉO CẢNH SAU (luôn vẽ trước các lớp khác ở cả hai mặt) =====
                    // Cảnh phía sau bị xoáy méo nhẹ, CHỈ trong phạm vi vòng lửa (r 0.7-1.05), không kéo vệt vật thể bên ngoài cổng
                    float bandD = smoothstep(0.70, 0.85, r) * (1.0 - smoothstep(0.95, 1.08, r)) * saturate(s * 1.5);
                    if (bandD > 0.001)
                    {
                        float2 uvP = i.screenPos.xy / i.screenPos.w;
                        float2 uvC = i.centerPos.xy / i.centerPos.w;
                        float asp = _ScreenParams.x / _ScreenParams.y;
                        float2 d = uvP - uvC;
                        d.x *= asp;
                        float e = _Distort * bandD * (0.55 + 0.9 * vnoise(dir * 3.0 + t * 0.5));
                        float cs = cos(e), sn = sin(e);
                        float2 dr = float2(d.x * cs - d.y * sn, d.x * sn + d.y * cs);
                        dr.x /= asp;
                        float3 bg = SampleSceneColor(uvC + dr);
                        float a0 = bandD * 0.85;
                        acc = float4(bg * a0, a0);
                    }
                }
                else if (_Layer < 0.5)
                {
                    // ===== VÒNG GIỮA =====
                    float phi = _Angle - _Twist * r;                 // xoắn theo bán kính
                    float cph = cos(phi), sph = sin(phi);
                    float2 qs = float2(q.x * cph - q.y * sph, q.x * sph + q.y * cph);
                    float angS = atan2(qs.y, qs.x);
                    float2 c = qs * 1.1 + normalize(qs + 1e-5) * (t * _Inward);   // nhiễu trôi vào tâm
                    float ridge = SAMPLE_TEXTURE2D_LOD(_NoiseA, sampler_NoiseA, c * 0.45, 1).r;
                    float cell = SAMPLE_TEXTURE2D_LOD(_NoiseB, sampler_NoiseB, c * 0.8 + 0.37, 1).r;
                    float warp = SAMPLE_TEXTURE2D_LOD(_NoiseB, sampler_NoiseB, c * 0.3 + 0.11, 1).r - 0.5;
                    float angW = angS + warp * 0.9;                  // cánh xoáy bị nhiễu bẻ cong không đều
                    float arm = 0.5 + 0.5 * cos(_Arms * angW);
                    arm = pow(arm, 1.25);
                    // Sợi mảnh chạy dọc theo cánh xoáy: đổi chậm theo góc, đổi nhanh theo bán kính (số lần lặp theo góc là số nguyên nên không có đường nối)
                    float aU = angW / 6.2831853;
                    float strandA = SAMPLE_TEXTURE2D_LOD(_NoiseA, sampler_NoiseA, float2(aU * 5.0, r * 11.0 + t * _Inward * 2.2), 1).r;
                    float strandB = SAMPLE_TEXTURE2D_LOD(_NoiseA, sampler_NoiseA, float2(aU * 9.0 + 0.31, r * 27.0 + t * _Inward * 3.4), 1).r;
                    float strands = strandA * 0.55 + strandB * 0.45;
                    float cloud = saturate(arm * 1.2 - (1.0 - strands) * 0.8 + (strands - 0.5) * 0.4 + (cell - 0.5) * 0.15);
                    cloud *= 0.55 + 0.9 * ridge;
                    cloud = saturate(cloud);

                    float midMask = smoothstep(Rc, Rc + 0.1, r) * (1.0 - smoothstep(0.70, 0.86, r));   // hết trước khi vòng lửa bắt đầu tan
                    float boost = 1.0 + 1.3 * exp(-max(r - Rc, 0.0) * 5.5);
                    float3 cloudCol = lerp(_CloudBlue.rgb, _CloudWhite.rgb, saturate(cloud * 0.9 + (boost - 1.0) * 0.3));
                    float3 body = lerp(_CloudFill.rgb, cloudCol * boost, cloud) * _Intensity;
                    float a1 = midMask * 0.97;                       // đặc: không nhìn rõ vật phía sau
                    acc = over(float4(body * a1, a1), acc);
                }
                else if (_Layer < 1.5)
                {
                    // ===== LÕI =====
                    float coreMask = 1.0 - smoothstep(Rc - 0.012, Rc + 0.012, r);
                    float2 cellId = floor(q * 22.0);
                    float2 inCell = frac(q * 22.0) - 0.5;
                    float starRound = 1.0 - smoothstep(0.05, 0.3, length(inCell));
                    float star = step(0.985, hash21(cellId)) * starRound * (0.4 + 0.6 * sin(t * 5.0 + hash21(cellId + 3.1) * 40.0));
                    float3 coreRgb = float3(0.004, 0.011, 0.028) + star * float3(0.4, 0.7, 1.1) * 0.45 * (1.0 - r / max(Rc, 0.01));
                    acc = float4(coreRgb * coreMask, coreMask);
                    float rimOut = exp(-max(r - Rc, 0.0) * 30.0) * step(Rc, r);
                    float rimIn = exp(-max(Rc - r, 0.0) * 60.0) * step(r, Rc) * 0.6;
                    float3 rim = _RimColor.rgb * (rimOut + rimIn) * _Intensity * saturate(s * 2.0);
                    acc = over(float4(rim, saturate(dot(rim, 0.33) * 0.25)), acc);
                }
                else
                {
                    // ===== VÒNG LỬA: cộng sáng thuần, không làm tối nền =====
                    float fireK = _Fire;
                    float nF = vnoise(dir * 4.0 + float2(t * 1.3, -t * 0.9));
                    float spike = pow(vnoise(dir * 13.0 + t * 2.7), 3.0);
                    float flick = vnoise(dir * 7.0 - t * 4.0);
                    float edgeR = 0.9 + fireK * (0.07 * (nF - 0.5) * 2.0 + 0.11 * spike);
                    // Một đường cong giảm đều từ trong ra ngoài (không có chỗ lõm giữa thân lửa và vầng tan) nên không có nét sẫm quanh mép; mép lởm chởm do edgeR đổi theo góc
                    float fall = max(r - (edgeR - 0.12), 0.0) * 9.0;
                    float body = smoothstep(0.66, 0.80, r) * exp(-pow(fall, 1.6));
                    float outer = exp(-max(r - edgeR, 0.0) * 6.0) * 0.14 * smoothstep(0.8, 0.95, r);      // vầng mờ tan dần ra ngoài
                    float turb = SAMPLE_TEXTURE2D_LOD(_NoiseA, sampler_NoiseA, q * 0.55 + dir * t * 0.6 + 0.2, 1).r;
                    float inten = (body + outer) * (0.5 + 0.95 * turb) * (0.7 + 0.6 * flick) * fireK;
                    float3 fc = lerp(_FireA.rgb, _FireB.rgb, saturate(turb * 1.3 - 0.1 + body * 0.2));
                    acc = float4(fc * inten * _Intensity, 0.0);      // alpha = 0: chỉ cộng sáng
                }

                acc *= edgeFade;

                if (_Layer > 1.5)
                {
                    // Điểm sáng trắng lúc mở / đóng
                    float fl = _Flash * exp(-dot(p, p) * 40.0);
                    acc.rgb += float3(4.0, 4.3, 4.8) * fl;
                }
                return half4(acc);
            }
            ENDHLSL
        }
    }
}
