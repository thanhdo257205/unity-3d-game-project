// Quầng tròn mờ dần cho sấm sét (quầng sáng sau lưng, mảng tối mềm sau cánh). Hình tròn tính thẳng trong shader theo khoảng cách tới tâm
// (không dùng ảnh nên không bị nén/lượng tử hóa làm mép còn giá trị khác 0): độ đậm giảm mượt và về ĐÚNG 0 tại bán kính 0.5 của tấm, trước khi
// chạm mép tấm, nên không bao giờ thấy cạnh thẳng. Có mờ mềm theo độ sâu (soft particle) để chỗ tấm cắt mặt đất không để lại đường cắt thẳng.
// Hai kiểu trộn chọn bằng _Additive: 1 = cộng sáng (quầng sáng), 0 = trộn alpha (mảng tối). Vật liệu đặt _SrcBlend/_DstBlend tương ứng.
Shader "PhamKiemLuc/QuangTron"
{
    Properties
    {
        [HDR] _ColorInner("Màu ở tâm", Color) = (1, 0.78, 0.3, 1)
        [HDR] _ColorOuter("Màu ở rìa", Color) = (0.12, 0.6, 1, 1)
        _Alpha("Độ đậm chung (0-1)", Range(0, 1)) = 0.3
        _Power("Độ gắt của suy giảm (1 = mềm, cao = chụm ở tâm)", Range(0.5, 8)) = 1.6
        _SoftDistance("Mờ mềm khi sát vật khác (mét)", Range(0.01, 3)) = 0.6
        _Additive("1 = cộng sáng, 0 = trộn alpha", Range(0, 1)) = 1
        [HideInInspector] _SrcBlend("Src", Float) = 1
        [HideInInspector] _DstBlend("Dst", Float) = 1
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" }

        Pass
        {
            Name "QuangTron"
            Blend [_SrcBlend] [_DstBlend]
            ZWrite Off
            ZTest LEqual
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _ColorInner;
                half4 _ColorOuter;
                half _Alpha;
                half _Power;
                half _SoftDistance;
                half _Additive;
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
                // Khoảng cách tới tâm, 0 ở tâm và 1 ở giữa mỗi cạnh tấm
                float d = length(i.uv - 0.5) * 2.0;
                float f = saturate(1.0 - d);
                f = f * f * (3.0 - 2.0 * f);              // smoothstep: đạo hàm 0 ở rìa nên không có mép gắt
                f = pow(f, _Power);
                f *= step(d, 0.999);                      // chắc chắn bằng 0 từ rìa trở ra

                // Mờ mềm theo độ sâu: sát mặt đất/vật cứng thì mờ dần, không có đường cắt
                float2 screenUV = i.positionCS.xy / _ScaledScreenParams.xy;
                float sceneDepth = LinearEyeDepth(SampleSceneDepth(screenUV), _ZBufferParams);
                float soft = saturate((sceneDepth - i.positionCS.w) / max(_SoftDistance, 0.001));

                half3 col = lerp(_ColorInner.rgb, _ColorOuter.rgb, smoothstep(0.0, 0.8, d));
                half weight = f * soft * _Alpha;
                half3 rgb = lerp(col, col * weight, _Additive);   // cộng sáng: màu x độ đậm; trộn alpha: màu giữ nguyên, độ đậm ở alpha
                half a = lerp(weight, 1.0, _Additive);
                return half4(rgb, a);
            }
            ENDHLSL
        }
    }
}
