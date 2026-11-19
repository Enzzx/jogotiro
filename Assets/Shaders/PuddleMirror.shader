// Poca com reflexo do tipo espelho (plano), compativel com URP.
// O reflexo vem de uma RenderTexture gerada pelo script PuddleMirror.cs.
Shader "Custom/PuddleMirror"
{
    Properties
    {
        [NoScaleOffset] _ReflectionTex ("Reflexo (RT)", 2D) = "black" {}
        [NoScaleOffset] _SelfTex ("Reflexo do Player (RT)", 2D) = "clear" {}
        _SelfStrength ("Forca do Reflexo do Player", Range(0, 1)) = 0.95
        _WaterColor ("Cor da Agua", Color) = (0.03, 0.045, 0.05, 1)
        _Opacity ("Opacidade", Range(0, 1)) = 0.95
        _ReflectionStrength ("Forca do Reflexo", Range(0, 1)) = 0.95
        _Darkening ("Escurecimento do Reflexo", Range(0, 1)) = 0.12
        _RippleScale ("Escala das Ondas", Float) = 7
        _RippleStrength ("Forca das Ondas", Range(0, 0.1)) = 0.012
        _EdgeSoftness ("Suavidade da Borda", Range(0.02, 0.5)) = 0.16
        _EdgeWobble ("Irregularidade da Borda", Range(0, 0.3)) = 0.12
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent-10"
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector" = "True"
        }

        Pass
        {
            Name "PuddleMirror"
            Tags { "LightMode" = "UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest LEqual
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_ReflectionTex);
            SAMPLER(sampler_ReflectionTex);
            TEXTURE2D(_SelfTex);
            SAMPLER(sampler_SelfTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _WaterColor;
                float _Opacity;
                float _ReflectionStrength;
                float _Darkening;
                float _RippleScale;
                float _RippleStrength;
                float _EdgeSoftness;
                float _EdgeWobble;
                float _SelfStrength;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                float4 screenPos : TEXCOORD2;
                float fogCoord : TEXCOORD3;
            };

            Varyings vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                VertexPositionInputs positions = GetVertexPositionInputs(input.positionOS.xyz);

                output.positionCS = positions.positionCS;
                output.positionWS = positions.positionWS;
                output.uv = input.uv;
                output.screenPos = ComputeScreenPos(positions.positionCS);
                output.fogCoord = ComputeFogFactor(positions.positionCS.z);
                return output;
            }

            float2 RippleOffset(float2 worldXZ, float time)
            {
                float2 offset;
                offset.x = sin(worldXZ.x * _RippleScale + time * 1.6) * 0.6
                         + sin(worldXZ.y * _RippleScale * 1.7 - time * 1.1) * 0.4;
                offset.y = sin(worldXZ.y * _RippleScale * 1.3 + time * 1.35) * 0.6
                         + sin(worldXZ.x * _RippleScale * 2.1 - time * 0.85) * 0.4;
                return offset;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float2 screenUV = input.screenPos.xy / input.screenPos.w;
                float2 ripple = RippleOffset(input.positionWS.xz, _Time.y) * _RippleStrength;
                float3 reflection = SAMPLE_TEXTURE2D(_ReflectionTex, sampler_ReflectionTex, screenUV + ripple).rgb;

                // Mascara radial com ondulacao: a poca nao fica um circulo perfeito.
                float2 centered = input.uv * 2.0 - 1.0;
                float radius = length(centered);
                float angle = atan2(centered.y, centered.x);
                float wobble = sin(angle * 3.0 + 0.6) * 0.060
                             + sin(angle * 5.0 - 1.2) * 0.035
                             + sin(angle * 8.0 + 2.1) * 0.020;

                float edge = radius + wobble * _EdgeWobble * 3.0;
                float alpha = saturate(1.0 - smoothstep(1.0 - _EdgeSoftness, 1.0, edge)) * _Opacity;

                if (alpha <= 0.002)
                    discard;

                // Visto de cima aparece a agua escura; na rasante o reflexo domina.
                float3 viewDirection = normalize(_WorldSpaceCameraPos - input.positionWS);
                float facing = saturate(viewDirection.y);
                float fresnel = lerp(0.55, 1.0, pow(1.0 - facing, 2.0));

                float3 tint = reflection * (1.0 - _Darkening);
                float3 color = lerp(_WaterColor.rgb, tint, _ReflectionStrength * fresnel);

                // Reflexo do proprio player (copia espelhada renderizada com a camera principal).
                float4 selfSample = SAMPLE_TEXTURE2D(_SelfTex, sampler_SelfTex, screenUV);
                float selfMask = saturate(selfSample.a * _SelfStrength * fresnel);
                color = lerp(color, selfSample.rgb * 1.05, selfMask);

                color = MixFog(color, input.fogCoord);

                return half4(color, alpha);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
