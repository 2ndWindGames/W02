// 물고기 파동 — 기획서 §6-1
//
// ⚠️ 정점 변위가 아니라 UV 왜곡이다.
//    Unity 의 Sprite(Full Rect)는 정점이 4개(삼각형 2장)뿐이라 정점을 밀어도 아무 일도 안 난다.
//    그래서 정점 대신 "텍스처를 샘플할 좌표"를 민다. SpriteRenderer / Sorting / 배칭을 그대로 쓴다.
//
// ⚠️ 스프라이트 요건 두 가지
//    1) 위아래 투명 여백을 몸통 높이의 30% 이상 — 여백이 없으면 꼬리가 경계에서 잘린다
//    2) 아틀라스에 넣지 말 것 (Packing Tag 비우기) — UV 가 [0,1] 밖으로 나가면 옆 스프라이트를 샘플한다
//       아틀라스를 꼭 써야 하면 스프라이트의 UV rect 를 프로퍼티로 넘겨 클램프해야 한다.
//
// 개체별 위상: per-instance 데이터 대신 오브젝트의 월드 원점을 시드로 쓴다.
//   물고기마다 위치가 다르니 자동으로 박자가 다르고, 추가 데이터가 0이라 배칭이 안 깨진다.

Shader "Fishing/FishWave"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color      ("Tint", Color) = (1,1,1,1)
        _Amp        ("Wave Amplitude (UV)", Range(0, 0.4)) = 0.12
        _Freq       ("Wave Frequency", Range(0, 20)) = 5.2
        _SpeedBoost ("Speed Boost", Range(0, 3)) = 1.0
        _Falloff    ("Tail Falloff Power", Range(1, 4)) = 2.0
        _BodyPhase  ("Phase Shift Along Body", Range(0, 8)) = 2.4
        _SeedScale  ("Per-Instance Seed Scale", Range(0, 4)) = 1.0
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "Queue"          = "Transparent"
            "RenderType"     = "Transparent"
            "IgnoreProjector"= "True"
            "PreviewType"    = "Plane"
        }

        Cull Off
        Lighting Off
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            Name "FishWaveUnlit"
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
                float4 color      : COLOR;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
                float4 color      : COLOR;
                float2 originWS   : TEXCOORD1;
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float4 _Color;
                float  _Amp;
                float  _Freq;
                float  _SpeedBoost;
                float  _Falloff;
                float  _BodyPhase;
                float  _SeedScale;
            CBUFFER_END

            Varyings vert (Attributes IN)
            {
                Varyings OUT;
                OUT.positionCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv         = TRANSFORM_TEX(IN.uv, _MainTex);
                OUT.color      = IN.color * _Color;
                // 오브젝트의 월드 원점 — 개체별 위상 시드
                OUT.originWS   = float2(UNITY_MATRIX_M._m03, UNITY_MATRIX_M._m13);
                return OUT;
            }

            half4 frag (Varyings IN) : SV_Target
            {
                // 스프라이트는 머리가 오른쪽이므로 u = 1 - uv.x  (머리 0 → 꼬리 1)
                float u = saturate(1.0 - IN.uv.x);

                // 위상: 시간 + 월드 위치 시드. 위치가 다르면 박자가 다르다.
                float phase = _Time.y * _Freq * _SpeedBoost
                            + dot(IN.originWS, float2(1.7, 2.3)) * _SeedScale;

                // 진폭은 꼬리로 갈수록 커진다 — 몸통은 거의 안 흔들리고 꼬리만 흔들린다
                float wave = _Amp * pow(u, _Falloff) * sin(phase - u * _BodyPhase);

                float2 uv = IN.uv + float2(0.0, wave);

                half4 c = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv);

                // UV 가 스프라이트 밖으로 나가면 잘라낸다 (아틀라스 오염 방지)
                float inside = step(0.0, uv.y) * step(uv.y, 1.0);
                c *= inside;

                c *= IN.color;
                c.rgb *= c.a;              // 스프라이트 기본과 동일한 프리멀티플라이 처리
                return c;
            }
            ENDHLSL
        }
    }

    Fallback "Universal Render Pipeline/2D/Sprite-Unlit-Default"
}
