Shader "FishingV2/FishSurface"
{
    Properties
    {
        _Amp ("Wave Amplitude", Float) = 0.16
        _Phase ("Wave Phase", Float) = 0
        _Turn ("Turn Bend", Float) = 0
        _Len ("Body Length", Float) = 1
        _Lag ("Wave Lag", Float) = 2.4
        _Exp ("Wave Exponent", Float) = 1.6
        _Hinge ("Wave Hinge", Float) = 0
        _Spread ("Element Phase Spread", Float) = 0
        _Jet ("Jet Charge", Range(0, 1)) = 0.5
        _Roll ("Bank", Float) = 0
        _Rip ("Fin Ripple", Float) = 0
        _RipW ("Fin Wave", Float) = 0
        _RipF ("Fin Rate", Float) = 2.2
        _TimeOffset ("Element Time Offset", Float) = 0
        _Drift ("Appendage Drift", Float) = 0
        _TurnLag ("Delayed Turn", Float) = 0
        _ArcBody ("Body Arc Participation", Float) = 1
        _ArmSwing ("Arm Inertia", Float) = 0
        _ArmTuck ("Arm Drag", Float) = 1
        _Pivot ("Swim Pivot", Float) = 0.36
        _RimStrength ("Rim Strength", Float) = 1
        _Depth ("Visual Depth", Range(0, 1)) = 0.5
        _DepthTintStrength ("Depth Tint Strength", Range(0, 2)) = 0.7
        _DepthDesaturation ("Depth Desaturation", Range(0, 1)) = 0.18
        _DepthBrightnessDrop ("Depth Brightness Drop", Range(0, 1)) = 0.2
        _DepthContrast ("Depth Contrast", Range(0, 1)) = 0.1
        _HeroContrast ("Hero Contrast", Range(0, 1)) = 0
        _OpticalTime ("Water Optical Time", Float) = 0
        _OpticalDistortionScale ("Water Optical Scale", Range(0.25, 2.5)) = 1.08
        _OpticalDistortionSpeed ("Water Optical Speed", Range(0, 1)) = 0.18
        _OpticalDistortionStrength ("Water Optical Strength", Range(0, 1)) = 0.42
        // Same shared surface field as WaterSurfaceV2. Passing the presentation shape here
        // keeps the fish light response on the same crests as the visible surface above them.
        _SurfaceShapeStrength ("Water Surface Shape", Range(0, 2)) = 0
        _WaterLightInfluence ("Water Light Influence", Range(0, 4)) = 1
        _SurfacePhase ("Water Surface Phase", Float) = 0
        // 랩 경계 근처에서 물고기를 지운다. 랩은 설계상 필요하지만 순간이동은 보이면 안 된다.
        _EdgeFade ("Wrap Edge Fade", Range(0, 1)) = 1
        _PondCenter ("Pond Center", Vector) = (0, 0, 0, 0)
        _PondSize ("Pond Size", Vector) = (16, 9, 0, 0)
        _LightDirection ("Light Direction", Vector) = (-0.36, 0.58, 0.73, 0)
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "Queue" = "Geometry"
            "RenderPipeline" = "UniversalPipeline"
        }
        Cull Off
        ZWrite On

        Pass
        {
            Name "FishForward"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float4 color : COLOR;
                float2 uv0 : TEXCOORD0;
                float2 uv1 : TEXCOORD1;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float3 normalWS : TEXCOORD0;
                float4 color : COLOR;
                float waterLight : TEXCOORD1;
            };

            CBUFFER_START(UnityPerMaterial)
                float _Amp;
                float _Phase;
                float _Turn;
                float _Len;
                float _Lag;
                float _Exp;
                float _Hinge;
                float _Spread;
                float _Jet;
                float _Roll;
                float _Rip;
                float _RipW;
                float _RipF;
                float _TimeOffset;
                float _Drift;
                float _TurnLag;
                float _ArcBody;
                float _ArmSwing;
                float _ArmTuck;
                float _Pivot;
                float _RimStrength;
                float _Depth;
                float _DepthTintStrength;
                float _DepthDesaturation;
                float _DepthBrightnessDrop;
                float _DepthContrast;
                float _HeroContrast;
                float _OpticalTime;
                float _OpticalDistortionScale;
                float _OpticalDistortionSpeed;
                float _OpticalDistortionStrength;
                float _SurfaceShapeStrength;
                float _WaterLightInfluence;
                float _SurfacePhase;
                float _EdgeFade;
                float4 _PondCenter;
                float4 _PondSize;
                float4 _LightDirection;
            CBUFFER_END

            #include "Assets/FishingV2/Shaders/WaterOpticsField.hlsl"

            float SwimAt(float u, float seed)
            {
                float body = max(u - _Hinge, 0.0);
                float phase = _Phase + seed * _Spread;
                float gain = 1.0 + (seed - 0.5) * 0.62 * step(0.001, seed);
                return _Amp * pow(body, _Exp) * sin(phase - u * _Lag) * gain;
            }

            float BendAt(float u, float seed)
            {
                float swim = SwimAt(u, seed) - SwimAt(_Pivot, 0.0);
                float trailWeight = smoothstep(0.98, 1.35, u);
                float delayedTurn = lerp(_Turn, _TurnLag, trailWeight);
                float arc = delayedTurn * (_ArcBody * pow(min(u, 1.0), 1.9) + max(u - 1.0, 0.0) * 0.5);

                float appendage = max(u - 1.0, 0.0);
                float isAppendage = step(0.001, seed);
                float drift = _Drift * isAppendage * appendage *
                    (sin((_Time.y + _TimeOffset) * 0.63 + seed * 17.0 - u * 3.4) +
                     0.55 * sin((_Time.y + _TimeOffset) * 1.07 + seed * 29.0 - u * 6.1));
                float swing = _ArmSwing * isAppendage * appendage * (0.72 + 0.56 * seed) *
                    (1.0 - 0.28 * sin(u * 4.1 + seed * 9.0));

                return (swim + arc + drift + swing) * _Len;
            }

            Varyings Vert(Attributes input)
            {
                Varyings output;
                float u = input.uv0.x;
                float seed = input.uv0.y;
                float ripple = input.uv1.x;
                float3 position = input.positionOS.xyz;
                float3 normal = input.normalOS;

                float mantle = 1.0 - smoothstep(0.86, 1.02, max(u, 0.0));
                float fill = (_Jet - 0.5) * mantle;
                position.yz *= 1.0 + fill * 0.86;
                position.x *= 1.0 - fill * 0.24;

                float cr = cos(_Roll);
                float sr = sin(_Roll);
                position.yz = float2(cr * position.y - sr * position.z, sr * position.y + cr * position.z);
                normal.yz = float2(cr * normal.y - sr * normal.z, sr * normal.y + cr * normal.z);

                if (seed > 0.001)
                {
                    position.y *= lerp(1.0, _ArmTuck, min(max(u - 1.0, 0.0), 1.0));
                }

                position.x += _Rip * ripple * sin((_Time.y + _TimeOffset) * _RipF - u * _RipW) * _Len;

                float bend = BendAt(u, seed);
                float nextBend = BendAt(u + 0.02, seed);
                float slope = -(nextBend - bend) / max(0.02 * _Len * 0.80, 0.0001);
                float bendAngle = atan(slope);
                float ca = cos(bendAngle);
                float sa = sin(bendAngle);
                normal = float3(normal.x * ca - normal.y * sa, normal.x * sa + normal.y * ca, normal.z);
                position.y += bend;

                VertexPositionInputs positionInputs = GetVertexPositionInputs(position);
                VertexNormalInputs normalInputs = GetVertexNormalInputs(normal);
                output.positionHCS = positionInputs.positionCS;
                output.normalWS = NormalizeNormalPerVertex(normalInputs.normalWS);
                output.color = input.color;
                float2 pondSize = max(_PondSize.xy, float2(0.1, 0.1));
                float2 waterUV = (positionInputs.positionWS.xy - _PondCenter.xy) / pondSize + 0.5;
                output.waterLight = WaterOpticsLight(
                    saturate(waterUV),
                    _SurfacePhase,
                    _OpticalDistortionScale,
                    _SurfaceShapeStrength);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                half3 normal = normalize(input.normalWS);
                half3 lightDirection = normalize(half3(_LightDirection.x, _LightDirection.y, _LightDirection.z));
                half ndl = dot(normal, lightDirection) * 0.5h + 0.5h;
                half band = ndl < 0.40h ? 0.82h : (ndl < 0.72h ? 0.96h : 1.08h);
                half3 color = input.color.rgb * band;

                // Keep the species palette intact while letting actual render Z communicate
                // depth: deeper fish absorb a little color, lose saturation, and sit darker
                // in the water instead of being uniformly stamped on top of the background.
                half depth = saturate(_Depth);
                half3 depthTinted = color * half3(0.84h, 0.95h, 1.08h) + half3(0.004h, 0.016h, 0.026h);
                color = lerp(color, depthTinted, saturate(depth * _DepthTintStrength));
                half luminance = dot(color, half3(0.2126h, 0.7152h, 0.0722h));
                color = lerp(color, luminance.xxx, saturate(depth * _DepthDesaturation));
                color *= 1.0h - depth * _DepthBrightnessDrop;
                color = (color - 0.5h) * (1.0h + depth * _DepthContrast) + 0.5h;

                // The shallow plane catches a restrained top light; this is intentionally a
                // value shift, not a glow or outline.
                color += saturate(ndl - 0.50h) * 0.10h * (1.0h - depth);
                if (_HeroContrast > 0.001h)
                {
                    half heroLuminance = dot(color, half3(0.2126h, 0.7152h, 0.0722h));
                    half3 heroColor = heroLuminance.xxx + (color - heroLuminance.xxx) * (1.0h + _HeroContrast * 1.8h);
                    color = lerp(color, heroColor, saturate(_HeroContrast));
                }

                // Shallow fish catch a small amount of the same surface-light field as the
                // floor. This is a value modulation only; the fish transform is untouched.
                half shallowWeight = 1.0h - depth;
                half fishLightModulation = (input.waterLight - 0.5h) * 2.0h * 0.060h * (half)_WaterLightInfluence * shallowWeight;
                color *= 1.0h + fishLightModulation;

                // 스크린 도어 페이드. 알파 블렌딩으로 바꾸면 물고기끼리 정렬 문제가 생기는데,
                // 이 페이드는 화면 맨 끝에서 1초 남짓 일어나므로 디더가 눈에 띄지 않는다.
                if (_EdgeFade < 0.996)
                {
                    const float bayer[16] =
                    {
                        0.0625, 0.5625, 0.1875, 0.6875,
                        0.8125, 0.3125, 0.9375, 0.4375,
                        0.2500, 0.7500, 0.1250, 0.6250,
                        1.0000, 0.5000, 0.8750, 0.3750
                    };
                    int2 pixel = int2(fmod(input.positionHCS.xy, 4.0));
                    clip(_EdgeFade - bayer[pixel.y * 4 + pixel.x]);
                }

                half rim = 1.0h - abs(normal.z);
                color = lerp(color, color * 0.34h, smoothstep(0.70h, 1.0h, rim) * _RimStrength);
                return half4(color, 1.0h);
            }
            ENDHLSL
        }
    }
}
