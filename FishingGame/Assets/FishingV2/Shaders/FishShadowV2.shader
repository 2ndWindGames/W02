Shader "FishingV2/FishShadow"
{
    Properties
    {
        _ShadowColor ("Shadow Color", Color) = (0.01, 0.08, 0.10, 0.42)
        _Softness ("Shadow Softness", Range(0, 1)) = 0.25
        _Depth ("Visual Depth", Range(0, 1)) = 0.5
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
        }
        Blend SrcAlpha OneMinusSrcAlpha
        Cull Off
        ZWrite Off

        Pass
        {
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float3 normalOS : TEXCOORD0;
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _ShadowColor;
                float _Softness;
                float _Depth;
            CBUFFER_END

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionHCS = TransformObjectToHClip(input.positionOS.xyz);
                output.normalOS = input.normalOS;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                // The projected mesh supplies the silhouette; soften its side-facing edge
                // slightly and let the enlarged low-alpha companion provide the remaining
                // penumbra. This keeps the prototype texture-free and depth-driven.
                half sideEdge = saturate(abs(normalize(input.normalOS).y));
                half edgeSoftness = saturate(sideEdge * (0.32h + _Softness * 0.68h));
                half edgeFade = lerp(1.0h, 0.46h, edgeSoftness);
                return half4(_ShadowColor.rgb, _ShadowColor.a * edgeFade);
            }
            ENDHLSL
        }
    }
}
