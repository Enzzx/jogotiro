// Shader de cachoeira para URP (Unity 2021.3+ / Unity 6).
// 100% procedural: não precisa de nenhuma textura.
// Usa o vertex color do FBX: R = espuma, G = velocidade do fluxo, B = profundidade, A = opacidade/fade.
Shader "Custom/URP_Waterfall"
{
    Properties
    {
        _ShallowColor ("Cor clara (rasa)", Color) = (0.62, 0.86, 0.92, 1)
        _DeepColor    ("Cor escura (funda)", Color) = (0.05, 0.32, 0.42, 1)
        _FoamColor    ("Cor da espuma", Color) = (0.96, 0.99, 1.0, 1)

        _Opacity      ("Opacidade base", Range(0,1)) = 0.55
        _Speed        ("Velocidade do fluxo", Range(0,8)) = 2.2
        _Tiling       ("Repetição ao longo da queda", Range(0.5,10)) = 3.0
        _StreakDensity("Densidade das listras", Range(2,40)) = 14

        _NormalStrength("Força do relevo", Range(0,3)) = 1.2
        _Specular     ("Brilho especular", Range(0,3)) = 1.0
        _Gloss        ("Nitidez do brilho", Range(8,256)) = 96
        _FresnelPower ("Fresnel", Range(0.5,8)) = 3.0

        _FoamAmount   ("Quantidade de espuma", Range(0,2)) = 1.0
        _FoamThreshold("Limiar da espuma", Range(0,1)) = 0.55

        [Toggle(_REFRACTION_ON)] _Refraction ("Refração (precisa de Opaque Texture)", Float) = 0
        _Distortion   ("Distorção da refração", Range(0,0.2)) = 0.04
    }

    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" }

        Pass
        {
            Name "Forward"
            Tags { "LightMode"="UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma shader_feature_local _REFRACTION_ON
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #if defined(_REFRACTION_ON)
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareOpaqueTexture.hlsl"
            #endif

            CBUFFER_START(UnityPerMaterial)
                half4  _ShallowColor;
                half4  _DeepColor;
                half4  _FoamColor;
                half   _Opacity;
                half   _Speed;
                half   _Tiling;
                half   _StreakDensity;
                half   _NormalStrength;
                half   _Specular;
                half   _Gloss;
                half   _FresnelPower;
                half   _FoamAmount;
                half   _FoamThreshold;
                half   _Distortion;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 uv         : TEXCOORD0;
                half4  color      : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS   : TEXCOORD1;
                float2 uv         : TEXCOORD2;
                half4  color      : COLOR;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            // ---------- ruído procedural ----------
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
                float2 u = f * f * (3.0 - 2.0 * f);
                float a = hash21(i);
                float b = hash21(i + float2(1, 0));
                float c = hash21(i + float2(0, 1));
                float d = hash21(i + float2(1, 1));
                return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y);
            }

            float fbm(float2 p)
            {
                float v = 0.0;
                float a = 0.5;
                [unroll] for (int i = 0; i < 3; i++)
                {
                    v += a * vnoise(p);
                    p = p * 2.03 + 11.7;
                    a *= 0.5;
                }
                return v / 0.875;
            }

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);

                VertexPositionInputs pos = GetVertexPositionInputs(IN.positionOS.xyz);
                OUT.positionCS = pos.positionCS;
                OUT.positionWS = pos.positionWS;
                OUT.normalWS   = TransformObjectToWorldNormal(IN.normalOS);
                OUT.uv         = IN.uv;
                OUT.color      = IN.color;
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float3 N = normalize(IN.normalWS);
                float3 V = GetWorldSpaceNormalizeViewDir(IN.positionWS);
                if (dot(N, V) < 0) N = -N; // superfície de dois lados

                half foamMask = IN.color.r;
                half flow     = max(IN.color.g, 0.05h);
                half depth    = IN.color.b;
                half edgeFade = IN.color.a;

                // ---------- fluxo: listras esticadas ao longo da queda (V) ----------
                float t = _Time.y * _Speed * flow;
                float2 p1 = float2(IN.uv.x * _StreakDensity,        IN.uv.y * _Tiling        - t);
                float2 p2 = float2(IN.uv.x * _StreakDensity * 1.9 + 17.3, IN.uv.y * _Tiling * 1.3 - t * 1.37);

                float n1 = fbm(p1);
                float n2 = fbm(p2);
                float streak = n1 * 0.6 + n2 * 0.4;

                // ---------- normal perturbada pelo ruído ----------
                const float e = 0.03;
                float dx = fbm(p1 + float2(e, 0)) - n1;
                float dy = fbm(p1 + float2(0, e)) - n1;

                float3 T = (abs(N.y) > 0.99) ? float3(1, 0, 0) : normalize(cross(float3(0, 1, 0), N));
                float3 B = cross(N, T);
                float3 nWS = normalize(N + (T * dx + B * dy) * (_NormalStrength * 8.0));

                // ---------- cor base ----------
                half3 baseCol = lerp(_DeepColor.rgb, _ShallowColor.rgb, saturate(streak * 1.4 - 0.1));
                baseCol = lerp(_ShallowColor.rgb, baseCol, saturate(depth * 1.2)); // bordas/filetes finos mais claros

                // ---------- iluminação ----------
                Light mainLight = GetMainLight();
                half ndl = saturate(dot(nWS, mainLight.direction)) * 0.5h + 0.5h;
                half3 ambient = SampleSH(nWS);
                half3 lit = baseCol * (mainLight.color * ndl + ambient);

                float3 H = normalize(mainLight.direction + V);
                half spec = pow(saturate(dot(nWS, H)), _Gloss) * _Specular;
                lit += mainLight.color * spec;

                half fres = pow(1.0h - saturate(dot(nWS, V)), _FresnelPower);
                lit += fres * ambient * 0.5h;

                // ---------- espuma ----------
                half foam = saturate((streak * 1.2 + foamMask * _FoamAmount - _FoamThreshold) * 4.0);
                foam *= saturate(foamMask * _FoamAmount + 0.15h);
                lit = lerp(lit, _FoamColor.rgb * (mainLight.color * 0.6h + ambient + 0.35h), foam);

                // ---------- alpha ----------
                half alpha = lerp(_Opacity, 1.0h, foam);
                alpha = saturate(alpha + fres * 0.25h) * edgeFade;

                #if defined(_REFRACTION_ON)
                    float2 suv = GetNormalizedScreenSpaceUV(IN.positionCS);
                    suv += (nWS.xz) * _Distortion;
                    half3 scene = SampleSceneColor(suv);
                    lit = lerp(scene * lerp(1.0h, baseCol, 0.5h), lit, saturate(alpha));
                    alpha = edgeFade;
                #endif

                return half4(lit, alpha);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
