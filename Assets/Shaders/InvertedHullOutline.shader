Shader "ComicVFX/InvertedHullOutline"
{
    Properties
    {
        _OutlineColor ("Outline Color", Color) = (0.05, 0.05, 0.08, 1.0)
        _OutlineWidth ("Outline Width", Range(0.0, 0.05)) = 0.008
        _ZOffset ("Z Offset", Range(-1, 1)) = -0.001
    }
    SubShader
    {
        Tags 
        { 
            "RenderType" = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Geometry+10"
        }

        Pass
        {
            Name "InvertedHullOutlinePass"
            Cull Front
            ZWrite On
            ZTest LEqual

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS   : POSITION;
                float3 normalOS     : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS   : SV_POSITION;
                float4 color        : COLOR;
            };

            CBUFFER_START(UnityPerMaterial)
                half4 _OutlineColor;
                float _OutlineWidth;
                float _ZOffset;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output;

                // Transforma posicao e normal para World Space para eliminar distorcoes de escala do objeto
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 normalWS = normalize(TransformObjectToWorldNormal(input.normalOS));

                // Extrusao em World Space (independente de escala do prefab ou do modelo FBX)
                float3 extrudedPosWS = positionWS + normalWS * _OutlineWidth;
                
                output.positionCS = TransformWorldToHClip(extrudedPosWS);

                #if defined(UNITY_REVERSED_Z)
                    output.positionCS.z -= _ZOffset * output.positionCS.w;
                #else
                    output.positionCS.z += _ZOffset * output.positionCS.w;
                #endif

                output.color = _OutlineColor;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                return input.color;
            }
            ENDHLSL
        }
    }
}
