// Lông vũ của Phong Lôi Sí: shader URP đổ sáng đầy đủ kiểu Lit (PBR, bóng, ánh sáng phụ) nhưng đọc MÀU TỪ VERTEX COLOR
// (màu gradient của cánh được bake sẵn vào đỉnh) nhân với _BaseColor, hình lông (alpha cắt) lấy từ texture alpha (_BaseMap.a),
// và có EMISSION XANH LỤC Ở MÉP LÔNG (dải alpha ngay trên ngưỡng cắt) sáng lên theo "Độ sáng mép" (script cánh đặt theo nhịp vỗ).
// URP Lit chuẩn không đọc vertex color nên cần shader riêng này. Hai mặt, không đổ bóng.
Shader "PhamKiemLuc/LongVuLit"
{
    Properties
    {
        _BaseMap("Ảnh hình lông (alpha)", 2D) = "white" {}
        _BaseColor("Màu nhân", Color) = (1, 1, 1, 1)
        _VertexColorStrength("Dùng màu đỉnh (0-1)", Range(0, 1)) = 1
        _Brighten("Làm sáng màu đỉnh", Float) = 1.6
        _Cutoff("Ngưỡng alpha", Range(0, 1)) = 0.5
        _Metallic("Kim loại", Range(0, 1)) = 0.15
        _Smoothness("Độ bóng", Range(0, 1)) = 0.45
        [HDR] _EdgeEmission("Màu emission mép lông", Color) = (0.25, 1.6, 0.7, 1)
        _EdgeWidth("Bề rộng mép sáng", Range(0.01, 0.5)) = 0.14
        _EdgeIntensity("Độ sáng mép (nhân)", Float) = 1
    }

    SubShader
    {
        Tags { "RenderType" = "TransparentCutout" "Queue" = "AlphaTest" "RenderPipeline" = "UniversalPipeline" }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            Cull Off
            AlphaToMask On

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile _ _LIGHT_LAYERS
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                float4 _BaseColor;
                float _VertexColorStrength;
                float _Brighten;
                float _Cutoff;
                float _Metallic;
                float _Smoothness;
                float4 _EdgeEmission;
                float _EdgeWidth;
                float _EdgeIntensity;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                float3 normalWS : TEXCOORD2;
                float4 color : COLOR;
                half fogFactor : TEXCOORD3;
            };

            Varyings vert(Attributes input)
            {
                Varyings o;
                VertexPositionInputs vp = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs vn = GetVertexNormalInputs(input.normalOS);
                o.positionCS = vp.positionCS;
                o.positionWS = vp.positionWS;
                o.normalWS = vn.normalWS;
                o.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                o.color = input.color;
                o.fogFactor = ComputeFogFactor(vp.positionCS.z);
                return o;
            }

            half4 frag(Varyings i, FRONT_FACE_TYPE facing : FRONT_FACE_SEMANTIC) : SV_Target
            {
                half4 tex = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv);
                half a = tex.a;
                clip(a - _Cutoff);

                half3 vcol = lerp(half3(1, 1, 1), i.color.rgb, _VertexColorStrength) * _Brighten;

                SurfaceData s = (SurfaceData)0;
                s.albedo = vcol * _BaseColor.rgb;
                s.metallic = _Metallic;
                s.smoothness = _Smoothness;
                s.occlusion = 1;
                s.alpha = 1;
                s.normalTS = half3(0, 0, 1);
                // Mép lông: dải alpha ngay trên ngưỡng cắt phát sáng
                half edge = 1.0 - saturate((a - _Cutoff) / max(_EdgeWidth, 0.001));
                s.emission = _EdgeEmission.rgb * (edge * edge * _EdgeIntensity);

                InputData d = (InputData)0;
                d.positionWS = i.positionWS;
                d.positionCS = i.positionCS;
                half3 n = normalize(i.normalWS);
                d.normalWS = IS_FRONT_VFACE(facing, true, false) ? n : -n;
                d.viewDirectionWS = GetWorldSpaceNormalizeViewDir(i.positionWS);
                d.shadowCoord = TransformWorldToShadowCoord(i.positionWS);
                d.fogCoord = i.fogFactor;
                d.vertexLighting = half3(0, 0, 0);
                d.bakedGI = SampleSH(d.normalWS);
                d.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(i.positionCS);
                d.shadowMask = half4(1, 1, 1, 1);

                half4 col = UniversalFragmentPBR(d, s);
                col.rgb += s.emission;
                col.rgb = MixFog(col.rgb, d.fogCoord);
                return half4(col.rgb, 1);
            }
            ENDHLSL
        }
    }
}
