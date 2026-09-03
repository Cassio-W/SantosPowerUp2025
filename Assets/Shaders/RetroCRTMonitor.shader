Shader "Custom/RetroCRTMonitor"
{
    Properties
    {
        [MainTexture] _BaseMap ("Base UI Texture (RenderTexture)", 2D) = "white" {}
        _ColorTint ("CRT Phosphor Tint", Color) = (1, 1, 1, 1)
        
        [Header(CRT Curvature)]
        _Curvature ("Screen Curvature (Bulge)", Range(0.0, 0.25)) = 0.06
        _CornerSmoothness ("Corner Smoothness", Range(0.001, 0.1)) = 0.02
        
        [Header(Scanlines)]
        _ScanlineIntensity ("Scanline Intensity", Range(0.0, 1.0)) = 0.28
        _ScanlineCount ("Scanline Count", Float) = 540.0
        _ScanlineSpeed ("Scanline Scroll Speed", Float) = 0.4
        
        [Header(Phosphor and Mask)]
        _ShadowMaskIntensity ("Phosphor Mask Intensity", Range(0.0, 1.0)) = 0.2
        _MaskScale ("Phosphor Mask Scale", Float) = 960.0
        
        [Header(Chromatic Aberration)]
        _ChromaticAberration ("Chromatic Aberration", Range(0.0, 0.03)) = 0.005
        
        [Header(Static Noise and Grain)]
        _NoiseIntensity ("Static Noise Intensity", Range(0.0, 0.5)) = 0.06
        _NoiseSpeed ("Noise Speed", Float) = 18.0
        
        [Header(Glitch and Distortion)]
        _GlitchIntensity ("Glitch Intensity", Range(0.0, 1.0)) = 0.1
        _GlitchFrequency ("Glitch Frequency", Float) = 6.0
        _GlitchBurst ("Dynamic Glitch Burst (Scriptable)", Range(0.0, 2.0)) = 0.0
        
        [Header(Vignette and Glow)]
        _VignetteIntensity ("Vignette Intensity", Range(0.0, 1.5)) = 0.4
        _Brightness ("Brightness Multiplier", Range(0.5, 2.5)) = 1.15
        _Contrast ("Contrast Boost", Range(0.8, 1.8)) = 1.1
    }

    SubShader
    {
        Tags 
        { 
            "RenderType" = "Opaque" 
            "RenderPipeline" = "UniversalPipeline" 
            "Queue" = "Geometry"
        }

        Pass
        {
            Name "Unlit"
            Cull Off
            ZWrite On
            ZTest LEqual

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                float4 _ColorTint;
                float _Curvature;
                float _CornerSmoothness;
                float _ScanlineIntensity;
                float _ScanlineCount;
                float _ScanlineSpeed;
                float _ShadowMaskIntensity;
                float _MaskScale;
                float _ChromaticAberration;
                float _NoiseIntensity;
                float _NoiseSpeed;
                float _GlitchIntensity;
                float _GlitchFrequency;
                float _GlitchBurst;
                float _VignetteIntensity;
                float _Brightness;
                float _Contrast;
            CBUFFER_END

            // Pseudo-random generator
            float Hash(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            // CRT Barrel distortion
            float2 CurveUV(float2 uv, float curve)
            {
                uv = uv * 2.0 - 1.0;
                float2 offset = abs(uv.yx) / float2(6.0, 4.0);
                uv = uv + uv * offset * offset * (curve * 10.0);
                uv = uv * 0.5 + 0.5;
                return uv;
            }

            // Rounded border mask
            float ScreenBorderMask(float2 uv, float smoothness)
            {
                float2 border = smoothstep(float2(0.0, 0.0), float2(smoothness, smoothness), uv) *
                               smoothstep(float2(0.0, 0.0), float2(smoothness, smoothness), 1.0 - uv);
                return saturate(border.x * border.y);
            }

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float time = _Time.y;
                float totalGlitch = saturate(_GlitchIntensity + _GlitchBurst);

                // 1. Distorção de Curvatura CRT
                float2 uv = CurveUV(input.uv, _Curvature);

                // Fora dos limites da tela curvílinea
                float borderMask = ScreenBorderMask(uv, _CornerSmoothness);
                if (uv.x < 0.0 || uv.x > 1.0 || uv.y < 0.0 || uv.y > 1.0)
                {
                    return half4(0.02, 0.03, 0.03, 1.0); // Borda da carcaça do monitor
                }

                // 2. Glitch horizontal (Tearing / Linhas de interferência)
                if (totalGlitch > 0.01)
                {
                    float glitchTime = floor(time * _GlitchFrequency);
                    float slice = floor(uv.y * 30.0 + glitchTime);
                    float sliceNoise = Hash(float2(slice, glitchTime));
                    
                    if (sliceNoise > (0.92 - totalGlitch * 0.45))
                    {
                        float displacement = (Hash(float2(sliceNoise, time)) - 0.5) * 0.08 * totalGlitch;
                        uv.x += displacement;
                    }
                    
                    // Jitter vertical sutil
                    float vJitter = (Hash(float2(glitchTime, 1.0)) - 0.5) * 0.004 * totalGlitch;
                    uv.y += vJitter;
                }

                // 3. Aberração Cromática
                float2 centerVec = uv - 0.5;
                float distFromCenter = dot(centerVec, centerVec);
                float chromaOffset = _ChromaticAberration + (distFromCenter * _ChromaticAberration * 2.0) + (totalGlitch * 0.012);

                float2 uvR = uv + centerVec * chromaOffset;
                float2 uvG = uv;
                float2 uvB = uv - centerVec * chromaOffset;

                half r = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uvR).r;
                half g = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uvG).g;
                half b = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uvB).b;
                half a = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uv).a;

                half3 col = half3(r, g, b);

                // 4. Scanlines (Linhas horizontais de varredura)
                float scanlinePos = uv.y * _ScanlineCount + time * _ScanlineSpeed * 10.0;
                float scanline = sin(scanlinePos * 3.14159);
                scanline = scanline * 0.5 + 0.5;
                col *= lerp(1.0, scanline, _ScanlineIntensity);

                // 5. Máscara de Fósforo (RGB Subpixel Shadow Mask)
                float maskX = frac(uv.x * _MaskScale);
                half3 phosphorMask = half3(
                    smoothstep(0.0, 0.33, maskX) - smoothstep(0.33, 0.66, maskX),
                    smoothstep(0.33, 0.66, maskX) - smoothstep(0.66, 1.0, maskX),
                    smoothstep(0.66, 1.0, maskX) + (1.0 - smoothstep(0.0, 0.33, maskX))
                );
                col = lerp(col, col * (phosphorMask * 1.5 + 0.5), _ShadowMaskIntensity);

                // 6. Ruído Analógico / Estática
                float noise = (Hash(uv * 100.0 + frac(time * _NoiseSpeed)) - 0.5) * (_NoiseIntensity + totalGlitch * 0.15);
                col += noise;

                // 7. Vinheta CRT (Escurecimento das bordas)
                float vignette = uv.x * uv.y * (1.0 - uv.x) * (1.0 - uv.y);
                vignette = saturate(pow(16.0 * vignette, _VignetteIntensity * 0.5));
                col *= vignette;

                // 8. Tintura de Fósforo, Brilho e Contraste
                col = (col - 0.5) * _Contrast + 0.5;
                col *= _ColorTint.rgb * _Brightness;
                col *= borderMask;

                return half4(saturate(col), a);
            }
            ENDHLSL
        }
    }
    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
