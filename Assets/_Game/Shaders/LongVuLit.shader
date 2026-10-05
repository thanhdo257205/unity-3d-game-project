// Lông vũ của Phong Lôi Sí: shader URP đổ sáng đầy đủ kiểu Lit (PBR, bóng, ánh sáng phụ) nhưng đọc MÀU TỪ VERTEX COLOR
// (màu gradient của cánh được bake sẵn vào đỉnh) nhân với _BaseColor, hình lông (alpha cắt) lấy từ texture alpha (_BaseMap.a),
// và có EMISSION XANH LỤC Ở MÉP LÔNG: dải sáng liền, mảnh, mượt dọc hai mép và đầu mỗi chiếc lông. Độ gần mép được bake sẵn vào
// KÊNH ALPHA của vertex color (0 ở giữa lông, 1 ở mép/đầu lông), nội suy mượt qua mặt lông; "Độ sáng mép" do script cánh đặt theo nhịp vỗ.
// URP Lit chuẩn không đọc vertex color nên cần shader riêng này. Hai mặt, không đổ bóng.
Shader "PhamKiemLuc/LongVuLit"
{
    Properties
    {
        _BaseMap("Ảnh hình lông (alpha)", 2D) = "white" {}
        _BaseColor("Màu nhân", Color) = (1, 1, 1, 1)
        _VertexColorStrength("Dùng màu đỉnh (0-1)", Range(0, 1)) = 1
        _Brighten("Làm sáng màu đỉnh", Float) = 1.6
        _HueLock("Ép màu đỉnh về một tông (0-1)", Range(0, 1)) = 1
        _HueTarget("Tông màu ép về (0-1, 0.52 = xanh lơ ngọc)", Range(0, 1)) = 0.52
        _MinSaturation("Độ đậm màu tối thiểu khi ép tông", Range(0, 1)) = 0.55
        _Cutoff("Ngưỡng alpha", Range(0, 1)) = 0.5
        _Metallic("Kim loại", Range(0, 1)) = 0.15
        _Smoothness("Độ bóng", Range(0, 1)) = 0.45
        [HDR] _EdgeEmission("Màu emission mép lông", Color) = (0.2, 1.1, 0.5, 1)
        _EdgePower("Độ mảnh của dải mép (cao = mảnh)", Range(1, 24)) = 14
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
                float _HueLock;
                float _HueTarget;
                float _MinSaturation;
                float _Cutoff;
                float _Metallic;
                float _Smoothness;
                float4 _EdgeEmission;
                float _EdgePower;
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

                half3 vc = i.color.rgb;
                // Hai cánh có màu đỉnh khác nhau (một cánh xanh đồng nhất, một cánh vàng-xanh-cam): ép cùng một tông (giữ độ sáng/đậm tương đối)
                half3 hsv = RgbToHsv(vc);
                hsv.x = _HueTarget;
                hsv.y = max(hsv.y, _MinSaturation);
                vc = lerp(vc, HsvToRgb(hsv), _HueLock);
                half3 vcol = lerp(half3(1, 1, 1), vc, _VertexColorStrength) * _Brighten;

                SurfaceData s = (SurfaceData)0;
                s.albedo = vcol * _BaseColor.rgb;
                s.metallic = _Metallic;
                s.smoothness = _Smoothness;
                s.occlusion = 1;
                s.alpha = 1;
                s.normalTS = half3(0, 0, 1);
                // Mép lông: dải alpha ngay trên ngưỡng cắt phát sáng
                half edge = pow(saturate(i.color.a), _EdgePower);
                s.emission = _EdgeEmission.rgb * (edge * _EdgeIntensity);

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
