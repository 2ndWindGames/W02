Shader "FishingV2/WaterSurface"
{
    Properties
    {
        _DeepColor ("Base Water / Deep", Color) = (0.018, 0.085, 0.125, 1)
        _ShallowColor ("Base Water / Shallow", Color) = (0.085, 0.280, 0.335, 1)
        _ClearWaterColor ("Water / Clear Cyan Teal", Color) = (0.015, 0.300, 0.420, 1)
        _DeepWaterColor ("Water / Deep Blue Navy", Color) = (0.002, 0.015, 0.060, 1)
        _BottomBleedColor ("Water / Substrate Bleed", Color) = (0.065, 0.095, 0.055, 1)
        _ClearColorStrength ("Water / Clear Color Strength", Range(0, 1)) = 0.52
        _DepthColorStrength ("Water / Depth Color Strength", Range(0, 1)) = 0.52
        _BottomColorBleed ("Water / Bottom Color Bleed", Range(0, 1)) = 0.09
        _BottomDarkColor ("Bottom / Dark", Color) = (0.050, 0.165, 0.125, 1)
        _BottomLightColor ("Bottom / Light", Color) = (0.285, 0.405, 0.230, 1)
        _BottomVisibility ("Bottom Visibility", Range(0, 1.5)) = 0.70
        _BottomGrainStrength ("Bottom Grain Strength", Range(0, 1)) = 0.06
        _BottomVariationScale ("Bottom Variation Scale", Range(0.25, 3)) = 1.12
        _BottomTexture ("Bottom / Organic Substrate", 2D) = "gray" {}
        _UnderwaterSceneTex ("Underwater Scene / Coherent Optical Layer", 2D) = "black" {}
        _UnderwaterComposite ("Underwater Composite Mode", Range(0, 1)) = 1
        _UnderwaterUvScale ("Underwater Scene / UV Scale", Vector) = (1, 1, 0, 0)
        _UnderwaterUvOffset ("Underwater Scene / UV Offset", Vector) = (0, 0, 0, 0)
        // 착수는 배열이다 - 본 착수 하나와 크라운이 뿌린 2차 착수 여럿이 동시에 산다.
        // xy = 착수 지점(쿼드 UV), z = 0..1 진행도(음수면 꺼짐), w = 세기
        // _ImpactDetail[i].x = 1이면 함몰/크라운/제트까지, 0이면 파문만 (게임플레이용)
        _CastBubbles ("Bubbles / Cast Entry", Vector) = (-1, 0, 0, 0)
        _CrossBubbles ("Bubbles / Surface Crossing", Vector) = (-1, 0, 0, 0)
        // x = 물속 기포가 렌즈를 지나쳐 나가는 속도, y = 통과 기포가 퍼지는 속도
        _BubbleTuning ("Bubbles / Speeds", Vector) = (1.45, 2.85, 0, 0)
        // x = 0..1 진행도(음수면 꺼짐), y = 세기
        _LensDroplets ("Surface / Lens Droplets", Vector) = (-1, 0, 0, 0)
        // 물방울이 튀어나오는 자리(뷰포트 UV). 착수 순간의 찌 위치를 화면에 고정한다.
        _LensOrigin ("Surface / Lens Droplet Origin", Vector) = (0.5, 0.5, 0, 0)
        // clear zone과 edge fog는 화면 비네트다. 쿼드 UV는 월드에 고정되어 있어서, 카메라가
        // 뒤로 빠지면 화면 대부분이 fog 바깥으로 밀려나 전체가 어두워진다. 1 / CameraHeight를
        // 넣어 비네트를 뷰에 붙인다. gameplay에서는 1이라 기존과 동일하다.
        _VignetteScale ("Water / Vignette View Scale", Float) = 1
        _BottomTextureScale ("Bottom / Texture Scale", Range(0.25, 3)) = 1.55
        _BottomTextureBlend ("Bottom / Texture Blend", Range(0, 1)) = 0.66
        _BottomTextureContrast ("Bottom / Texture Contrast", Range(0, 2)) = 1.08
        _BottomSecondarySampleStrength ("Bottom / Secondary Sample", Range(0, 1)) = 0.42
        _OpticalDistortionStrength ("Optics / Surface Refraction", Range(0, 1)) = 0.42
        _OpticalDistortionScale ("Optics / Refraction Scale", Range(0.25, 2.5)) = 1.08
        _OpticalDistortionSpeed ("Optics / Refraction Speed", Range(0, 1)) = 0.18
        // Profile-driven UV coefficient. 0.0035 is the approved gameplay value; the
        // presentation profile pushes the same shared field to a visible undulation.
        _RefractionCoefficient ("Optics / Refraction Coefficient (UV)", Range(0, 0.04)) = 0.0035
        _OpticalTime ("Optics / Unscaled Time", Float) = 0
        // 누적 위상. 세션이 speed * dt를 적분해서 넣는다. 절대 시각에 speed를 곱하면
        // speed가 바뀌는 순간 위상이 수백 라디안씩 건너뛰어 화면이 스크럽된다.
        _SurfacePhase ("Optics / Accumulated Surface Phase", Float) = 0
        _OpticalCausticFloorBias ("Optics / Caustic Floor Bias", Range(0, 1)) = 0.78
        _LargeCausticColor ("Large Caustic Color", Color) = (0.045, 0.115, 0.105, 1)
        _LargeCausticStrength ("Large Caustic Strength", Range(0, 3)) = 0.32
        _LargeCausticScale ("Large Caustic Scale", Range(0.25, 3)) = 1.35
        _LargeCausticSpeed ("Large Caustic Speed", Range(0, 1)) = 0.06
        _MidCausticColor ("Mid Caustic Color", Color) = (0.075, 0.185, 0.155, 1)
        _MidCausticStrength ("Mid Caustic Strength", Range(0, 3)) = 0.28
        _MidCausticScale ("Mid Caustic Scale", Range(0.25, 3)) = 1.08
        _MidCausticSpeed ("Mid Caustic Speed", Range(0, 1)) = 0.18
        _MicroSurfaceColor ("Surface Micro Color", Color) = (0.022, 0.060, 0.058, 1)
        _MicroSurfaceStrength ("Surface Micro Strength", Range(0, 3)) = 0.14
        _SurfaceRippleColor ("Surface Ripple / Highlight Color", Color) = (0.040, 0.160, 0.180, 1)
        _SurfaceRippleStrength ("Surface Ripple / Highlight Strength", Range(0, 3)) = 0.85
        // Presentation surface block. Every value here is neutral (0, or 1 where it is a
        // multiplier) in the gameplay profile, so the approved water is unchanged.
        _SurfaceShapeStrength ("Surface / Visible Ripple Shape", Range(0, 2)) = 0
        // 착수 지점 주변에만 더해지는 물결. 전역 펄스로 올리면 잔잔하던 수면이 통째로
        // 파도치는 것으로 보인다 - 물결은 찌가 떨어진 자리에서 시작해 퍼져야 한다.
        _SurfaceHighlightStrength ("Surface / Highlight Multiplier", Range(0, 4)) = 1
        _SurfaceSpecularStrength ("Surface / Stylized Specular", Range(0, 2)) = 0
        _SurfaceSpecularColor ("Surface / Specular Color", Color) = (0.72, 0.88, 0.94, 1)
        _SurfaceReflectionStrength ("Surface / Reflection Hint", Range(0, 2)) = 0
        _SurfaceReflectionColor ("Surface / Reflection Sky Color", Color) = (0.42, 0.72, 0.86, 1)
        _AbsorptionStrength ("Water / Absorption Multiplier", Range(0, 3)) = 1
        _UnderwaterClarity ("Water / Underwater Clarity", Range(0, 1)) = 1
        _ClearZoneStrength ("Clear Observation Zone", Range(0, 2)) = 0.56
        _ClearZoneRadius ("Clear Zone Radius", Range(0.15, 2.5)) = 0.76
        _EdgeFogStrength ("Edge Depth / Fog", Range(0, 2)) = 0.42
        _EdgeFogRadius ("Edge Fog Radius", Range(0.15, 1.2)) = 0.44
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "Queue" = "Geometry"
            "RenderPipeline" = "UniversalPipeline"
        }
        // The prototype camera views the water from +Z. Keep the background robust while
        // comparing scene coordinate conventions in the editor.
        Cull Off

        Pass
        {
            Name "WaterLayers"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            // 동시에 살아 있는 착수 수. 본 착수 1 + 크라운이 뿌린 2차 착수 5.
            #define FISHING_IMPACT_COUNT 6

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _DeepColor;
                float4 _ShallowColor;
                float4 _ClearWaterColor;
                float4 _DeepWaterColor;
                float4 _BottomBleedColor;
                float _ClearColorStrength;
                float _DepthColorStrength;
                float _BottomColorBleed;
                float4 _BottomDarkColor;
                float4 _BottomLightColor;
                float _BottomVisibility;
                float _BottomGrainStrength;
                float _BottomVariationScale;
                float _BottomTextureScale;
                float _BottomTextureBlend;
                float _BottomTextureContrast;
                float _BottomSecondarySampleStrength;
                float _UnderwaterComposite;
                float4 _UnderwaterUvScale;
                float4 _UnderwaterUvOffset;
                float4 _Impacts[FISHING_IMPACT_COUNT];
                float4 _ImpactDetail[FISHING_IMPACT_COUNT];
                float4 _CastBubbles;
                float4 _CrossBubbles;
                float4 _BubbleTuning;
                float4 _LensDroplets;
                float4 _LensOrigin;
                float _VignetteScale;
                float _OpticalDistortionStrength;
                float _OpticalDistortionScale;
                float _OpticalDistortionSpeed;
                float _RefractionCoefficient;
                float _OpticalTime;
                float _SurfacePhase;
                float _OpticalCausticFloorBias;
                float4 _LargeCausticColor;
                float _LargeCausticStrength;
                float _LargeCausticScale;
                float _LargeCausticSpeed;
                float4 _MidCausticColor;
                float _MidCausticStrength;
                float _MidCausticScale;
                float _MidCausticSpeed;
                float4 _MicroSurfaceColor;
                float _MicroSurfaceStrength;
                float4 _SurfaceRippleColor;
                float _SurfaceRippleStrength;
                float _SurfaceShapeStrength;
                float _SurfaceHighlightStrength;
                float _SurfaceSpecularStrength;
                float4 _SurfaceSpecularColor;
                float _SurfaceReflectionStrength;
                float4 _SurfaceReflectionColor;
                float _AbsorptionStrength;
                float _UnderwaterClarity;
                float _ClearZoneStrength;
                float _ClearZoneRadius;
                float _EdgeFogStrength;
                float _EdgeFogRadius;
            CBUFFER_END

            TEXTURE2D(_BottomTexture);
            SAMPLER(sampler_BottomTexture);
            TEXTURE2D(_UnderwaterSceneTex);
            SAMPLER(sampler_UnderwaterSceneTex);

            #include "Assets/FishingV2/Shaders/WaterOpticsField.hlsl"

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionHCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                return output;
            }

            float Hash21(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            float ValueNoise(float2 p)
            {
                float2 cell = floor(p);
                float2 local = frac(p);
                local = local * local * (3.0 - 2.0 * local);
                float a = Hash21(cell);
                float b = Hash21(cell + float2(1.0, 0.0));
                float c = Hash21(cell + float2(0.0, 1.0));
                float d = Hash21(cell + float2(1.0, 1.0));
                return lerp(lerp(a, b, local.x), lerp(c, d, local.x), local.y);
            }

            // 굴절/수면광은 전부 누적 위상을 쓴다. time 인자는 코스틱 레이어 전용이다.
            float2 OpticalDisplacement(float2 uv, float shape)
            {
                return WaterOpticsRefraction(
                    uv,
                    _SurfacePhase,
                    _OpticalDistortionScale,
                    _OpticalDistortionStrength,
                    _RefractionCoefficient,
                    shape);
            }

            float OpticalLightField(float2 uv, float shape)
            {
                return WaterOpticsLight(uv, _SurfacePhase, _OpticalDistortionScale, shape);
            }

            // A small finite-difference estimate links the soft floor-light variation to the
            // same low-frequency surface field used for scene refraction. It is intentionally
            // broad and low contrast; it should read as light concentration on the substrate,
            // not as a second animated line texture.
            // 착수 물보라. 퍼져나가는 하나의 링이고, 변위와 밝기를 같이 만든다. 링이 굴절
            // 변위에 더해지므로 아래의 수중 이미지도 같이 밀린다 - 물보라는 수면에서 일어난
            // 사건이지 화면 위에 얹은 장식이 아니다.
            // 이 프래그먼트의 물결 세기. 착수는 여기 손대지 않는다.
            //
            // 예전에는 착수가 이 값을 국소적으로 올렸는데, 이 값은 결국 바람이 미는 방향성
            // 파형(WaterOpticsSwell)으로 들어간다. 즉 "찌가 떨어진 자리에 바람이 잠깐 세게
            // 분다"는 뜻이 되어 물리적으로 틀렸다. 착수는 방사형 사건이라 자기 필드를 갖는다.
            float EffectiveShape(float2 uv)
            {
                return max(_SurfaceShapeStrength, 0.0);
            }

            // 착수 한 건의 광학 반응.
            //
            // 물체가 수면을 때리면 순서가 있다 - 구덩이가 파이고(함몰), 테두리가 갈라지며
            // 솟고(크라운), 구덩이가 메워지며 가운데가 튀어오르고(제트), 파문이 퍼진다.
            // 파문만 있으면 "원이 커진다"로 보이지 "물이 튀었다"로는 안 보인다.
            float2 SplashOptics(float2 uv, float4 impact, float detail, float ringScaleBase, out float light, out float shade)
            {
                light = 0.0;
                shade = 0.0;
                float age = impact.z;
                if (age < 0.0 || age > 1.0 || impact.w <= 0.0001)
                {
                    return float2(0.0, 0.0);
                }

                float strength = impact.w;
                // 1.92는 물 쿼드의 월드 가로세로비. 보정하지 않으면 파문이 타원으로 퍼진다.
                float2 delta = (uv - impact.xy) * float2(1.92, 1.0);
                float radius = length(delta);
                float2 direction = radius > 0.0001 ? delta / radius : float2(0.0, 0.0);
                float theta = atan2(delta.y, delta.x);
                float2 displacement = float2(0.0, 0.0);

                // ④ 파문. 밝은 고리가 아니라 파도다.
                //
                // 예전에는 밝기 띠 하나를 반경 방향으로 밀었는데, 그건 빛나는 굴렁쇠지 물결이
                // 아니다. 물결이 물결로 보이는 이유는 높이장이 있고 그 기울기 때문에 한쪽 면이
                // 빛을 받고 반대쪽 면이 그늘지기 때문이다. 그래서 높이의 기울기를 해석적으로
                // 구해서 주변 잔물결과 똑같은 조명 계산에 넣는다.
                //
                //   h(r) = amp * env(trail) * cos(k * trail),  trail = front - r
                //   dh/dr = amp * env * (2*trail/w^2 * cos(k*trail) + k * sin(k*trail))
                {
                    // 파문은 멀리까지 간다. 예전에는 화면 폭의 20%쯤에서 멈춰서 "퍼지다 만"
                    // 것으로 보였다. 실제 파문은 계속 나아가면서 옅어져 사라진다.
                    float front = (0.020 + age * 0.88) * ringScaleBase;
                    float trail = front - radius;
                    // 파면 앞은 급하게 끊기고 뒤로는 길게 끌린다 — 아직 안 지나간 물은 잔잔하다.
                    // 나아갈수록 뒤꼬리가 넓어진다(분산).
                    float width = (0.055 + age * 0.135) * ringScaleBase;
                    float w = trail >= 0.0 ? width : width * 0.28;
                    float envelope = exp(-(trail / w) * (trail / w));

                    // 파장도 같이 길어져야 한다(분산). 고정이면 파열이 퍼질수록 마루가
                    // 계속 늘어나 스무 개 넘는 미세 격자가 되고, 여러 착수가 겹치면
                    // 물결이 아니라 모아레로 보인다. 마루 서너 개를 유지한다.
                    float wavelength = max((0.021 + age * 0.062) * ringScaleBase, 0.005);
                    float k = 6.2831853 / wavelength;
                    // 퍼질수록 에너지가 흩어져 마루가 낮아진다.
                    // 시간이 아니라 거리로 옅어지는 것이 요점이다. 원이 퍼지면 같은 에너지가
                    // 둘레 전체에 흩어지므로 마루가 급격히 낮아진다.
                    float amp = strength * 0.0062 * (1.0 - age * 0.55) / (0.30 + radius * 5.5);

                    float c = cos(k * trail);
                    float sn = sin(k * trail);
                    float slope = amp * envelope * (2.0 * trail / (w * w) * c + k * sn);

                    // 굴절: 주변 물결과 같은 방식으로 기울기를 UV 변위로 바꾼다.
                    displacement += direction * slope * 0.0090;

                    // 조명: 광원 쪽으로 기운 면은 밝고 반대쪽은 그늘진다. 이 부호가 있어야
                    // 파문이 굴곡으로 읽힌다.
                    float alongLight = dot(direction, normalize(float2(-0.52, 0.86))) * slope;
                    float lit = smoothstep(0.10, 0.62, alongLight);
                    float shaded = smoothstep(0.10, 0.62, -alongLight);
                    light += lit * strength * 0.48;
                    shade += shaded * strength * 0.30;
                }

                if (detail <= 0.001)
                {
                    return displacement;
                }

                // ① 함몰. 수면이 아래로 꺼진다 - 안쪽으로 당기는 변위와 중심부 그늘.
                // 이게 없으면 착수가 밝기만 더하는 사건이 되어 무게가 없다.
                float cavity = 1.0 - smoothstep(0.0, 0.34, age);
                if (cavity > 0.001)
                {
                    float cavityRadius = 0.030 + age * 0.078;
                    float inside = 1.0 - smoothstep(cavityRadius * 0.20, cavityRadius, radius);
                    displacement += direction * inside * cavity * strength * detail * 0.052;
                    shade += inside * cavity * strength * detail * 1.85;
                }

                // ② 크라운. 갈라져야 크라운이지만, 규칙적인 뾰족점 몇 개면 눈꽃 결정이 된다.
                // 점을 늘리고 진폭을 낮추고 띠를 넓히고, 서로 배수가 아닌 두 파수를 겹쳐서
                // 되풀이되는 별 모양이 안 생기게 한다.
                float crownT = saturate((age - 0.04) / 0.30);
                if (crownT > 0.001 && crownT < 0.999)
                {
                    float crownLife = sin(crownT * 3.14159265);
                    float crownRadius = 0.034 + crownT * 0.056;
                    float seed = impact.x * 37.0 + impact.y * 19.0;
                    float spikes = 1.0
                        + 0.135 * sin(theta * 13.0 + seed)
                        + 0.075 * sin(theta * 7.0 - seed * 1.7);
                    float band = exp(-pow((radius - crownRadius * spikes) / 0.026, 2.0));
                    light += band * crownLife * strength * detail * 0.92;
                    displacement -= direction * band * crownLife * strength * detail * 0.026;
                }

                // ③ 제트. 구덩이가 메워지며 가운데가 위로 솟는다. 함몰 다음에 와야 순서가 산다.
                float jetT = saturate((age - 0.22) / 0.34);
                if (jetT > 0.001 && jetT < 0.999)
                {
                    float jetLife = sin(jetT * 3.14159265);
                    float core = exp(-pow(radius / 0.030, 2.0));
                    light += core * jetLife * strength * detail * 1.15;
                    displacement += direction * core * jetLife * strength * detail * 0.020;
                }

                return displacement;
            }

            // 화면에 살아 있는 모든 착수(본 착수 + 크라운이 뿌린 2차 착수)를 합산한다.
            float2 ImpactField(float2 uv, out float light, out float shade)
            {
                light = 0.0;
                shade = 0.0;
                float2 total = float2(0.0, 0.0);

                [unroll]
                for (int i = 0; i < FISHING_IMPACT_COUNT; i++)
                {
                    float impactLight, impactShade;
                    total += SplashOptics(uv, _Impacts[i], _ImpactDetail[i].x, max(_ImpactDetail[i].y, 0.05), impactLight, impactShade);
                    light += impactLight;
                    shade = max(shade, impactShade);
                }

                return total;
            }

            // 기포는 두 가지 서로 다른 현상이다. 하나의 함수에 스위치를 달면 둘 다 어정쩡해진다.
            //
            //  (1) 찌가 끌고 들어간 공기 — 깊은 곳에서 수면으로 떠오른다.
            //  (2) 카메라가 수면을 뚫을 때 렌즈를 스쳐 지나가는 기포 — 전진 시차다.

            // 기포 한 개를 그린다. 가장자리가 밝고(전반사) 가운데는 비어 보인다.
            float BubbleSprite(float2 uv, float2 center, float size, float sharpness)
            {
                float2 delta = (uv - center) * float2(1.92, 1.0);
                float radius = length(delta);
                if (radius > size)
                {
                    return 0.0;
                }

                float k = radius / max(size, 1e-5);
                // 깊을수록 흐릿하다. sharpness가 낮으면 테두리가 뭉개져 배경에 녹는다.
                float inner = lerp(0.05, 0.34, sharpness);
                float rim = smoothstep(inner, 0.92, k) * (1.0 - smoothstep(0.92, 1.0, k));
                float core = (1.0 - k * k) * lerp(0.42, 0.22, sharpness);
                return rim * lerp(0.45, 1.0, sharpness) + core;
            }

            // (1) 떠오르는 기포.
            //
            // 탑다운에서 "떠오른다"는 화면 위로 가는 게 아니라 카메라 쪽으로 다가오는 것이다.
            // 그래서 깊이를 명시적으로 들고, 깊을수록 작고 흐리고 어둡게, 수면에 가까울수록
            // 크고 선명하고 밝게 그린 뒤 터뜨린다. 상승은 가속하고, 오르면서 좌우로 지그재그로
            // 흔들린다 — 실제 기포가 곧게 오르지 않는 이유다.
            //
            // passesCamera가 1이면 수면이 카메라 뒤에 있다는 뜻이다. 물속에서 찌를 다시
            // 던지면 기포는 렌즈를 지나쳐 화면 밖으로 나간다 — 터질 수면이 화면에 없다.
            // 밖에서 볼 때(0)는 수면이 앞에 있으므로 닿아서 터지고 작은 파문을 남긴다.
            float RisingBubbles(float2 uv, float4 spec, float2 origin)
            {
                float age = spec.x;
                float strength = spec.y;
                float seed = spec.z;
                float passesCamera = spec.w;
                if (age < 0.0 || age > 1.0 || strength <= 0.0001)
                {
                    return 0.0;
                }

                float total = 0.0;

                [unroll]
                for (int i = 0; i < 14; i++)
                {
                    float f = (float)(i + 1);
                    float h0 = frac(sin(f * 12.9898 + seed * 78.233) * 43758.5453);
                    float h1 = frac(sin(f * 39.346 + seed * 11.135) * 24634.6345);
                    float h2 = frac(sin(f * 71.207 + seed * 51.049) * 31871.2214);

                    float birth = h0 * 0.42;
                    float span = 0.38 + h1 * 0.52;
                    float local = (age - birth) / span;
                    if (local < 0.0 || local >= 1.0)
                    {
                        continue;
                    }

                    // 상승은 가속한다 — 처음엔 물에 붙잡혀 있다가 부력이 이긴다.
                    float rise = local * local * (3.0 - 2.0 * local);
                    // 0 = 깊음, 1 = 수면. 이 값 하나가 크기·선명도·밝기를 전부 정한다.
                    float depth = rise;

                    // 찌가 끌고 들어간 자리에서 조금 흩어진 채로 시작한다.
                    float angle = h1 * 6.2831853;
                    float scatter = 0.012 + h2 * 0.038;
                    float2 position = origin + float2(cos(angle) / 1.92, sin(angle)) * scatter;
                    // 오르면서 지그재그. 곧게 오르면 화면에 박힌 점이 커지는 것으로 보인다.
                    position.x += sin(rise * 9.0 + h0 * 6.28) * 0.016 * (0.35 + h2) / 1.92;
                    position.y += sin(rise * 7.4 + h1 * 6.28) * 0.011 * (0.35 + h0);

                    float baseSize = 0.0060 + h2 * 0.0125;
                    if (passesCamera > 0.5)
                    {
                        // 렌즈를 지나쳐 나간다. 다가올수록 급격히 커지고, 시차로 화면
                        // 중심에서 바깥으로 밀리며, 터지지 않고 프레임을 벗어난다.
                        float approach = 0.30 * exp(max(_BubbleTuning.x, 0.05) * local);
                        float2 fromCenter = position - float2(0.5, 0.5);
                        position = float2(0.5, 0.5) + fromCenter * (0.55 + approach);
                        float size = baseSize * (0.45 + approach * 1.15);
                        float leave = 1.0 - smoothstep(0.80, 1.0, local);
                        total = max(total, BubbleSprite(uv, position, size, 0.90) * leave * strength);
                        continue;
                    }

                    float size = baseSize * (0.35 + depth * 1.85);
                    // 수면에 닿으면 터진다. 터진 자리에 아주 작은 파문이 남는다 — 기포가
                    // 그냥 사라지면 어디에도 닿지 않은 것이 된다.
                    float pop = 1.0 - smoothstep(0.86, 1.0, local);
                    total = max(total, BubbleSprite(uv, position, size, depth) * pop * strength * (0.35 + depth * 0.65));

                    float burst = smoothstep(0.86, 0.94, local) * (1.0 - smoothstep(0.94, 1.0, local));
                    if (burst > 0.001)
                    {
                        float burstT = saturate((local - 0.86) / 0.14);
                        float ringRadius = size * (1.0 + burstT * 2.4);
                        float2 d = (uv - position) * float2(1.92, 1.0);
                        float rr = length(d);
                        float band = exp(-pow((rr - ringRadius) / max(size * 0.55, 1e-4), 2.0));
                        total = max(total, band * burst * strength * 0.55);
                    }
                }

                return total;
            }

            // (2) 렌즈를 스쳐 지나가는 기포.
            //
            // 앞으로 나아갈 때의 시차는 화면 중심을 기준으로 한 방사형 확대다. 중앙의 기포는
            // 거의 안 움직이고, 가장자리 기포는 빠르게 화면 밖으로 날아 나간다. 예전에는
            // 전부 중앙에서 조금씩만 퍼지게 해놔서 화면을 벗어나지도 못했다.
            float RushingBubbles(float2 uv, float4 spec)
            {
                float age = spec.x;
                float strength = spec.y;
                float seed = spec.z;
                if (age < 0.0 || age > 1.0 || strength <= 0.0001)
                {
                    return 0.0;
                }

                const float2 center = float2(0.5, 0.5);
                // 전체 기포장이 카메라 쪽으로 다가오며 확대된다.
                float zoom = 0.30 * exp(max(_BubbleTuning.y, 0.05) * age);
                // 스쳐 지나가는 순간이 가장 커지는 순간이다. 예전에는 커지기 시작할 때
                // 페이드아웃이 겹쳐서 정작 보여야 할 구간에 사라지고 있었다.
                float fade = 1.0 - smoothstep(0.78, 1.0, age);
                float total = 0.0;

                [unroll]
                for (int i = 0; i < 20; i++)
                {
                    float f = (float)(i + 1);
                    float h0 = frac(sin(f * 23.771 + seed * 61.219) * 51237.4471);
                    float h1 = frac(sin(f * 57.113 + seed * 17.907) * 29341.7733);
                    float h2 = frac(sin(f * 91.463 + seed * 43.551) * 38117.5519);

                    // 화면 전체에 흩어진 시작 위치. 중앙에 몰지 않는다.
                    float2 seedPos = float2(h0, h1);
                    float2 offset = seedPos - center;
                    // 시차: 중심에서 먼 것일수록 빠르게 바깥으로 나간다.
                    float2 position = center + offset * zoom;
                    if (abs(position.x - 0.5) > 0.72 || abs(position.y - 0.5) > 0.72)
                    {
                        continue;
                    }

                    // 다가오는 만큼 커진다.
                    float size = (0.0075 + h2 * 0.0130) * zoom;
                    total = max(total, BubbleSprite(uv, position, size, 0.85) * fade * strength);
                }

                return total;
            }

            // 물방울이 맺히는 자리는 화면 전체다.            // 물방울이 맺히는 자리는 화면 전체다. 렌즈에 튄 물이지 씬 안의 물체가 아니라,
            // 찌 근처에만 모이면 오히려 어색하다. 찌 위치는 "어디서 날아왔는가"만 정한다.
            // xy = 맺히는 자리(뷰포트 UV), z = 반지름 배수, w = 생기는 시각
            static const float4 kDroplet[9] =
            {
                float4(0.115, 0.820, 1.10, 0.000),
                float4(0.330, 0.945, 0.62, 0.145),
                float4(0.520, 0.755, 0.95, 0.045),
                float4(0.765, 0.900, 0.50, 0.290),
                float4(0.910, 0.665, 1.05, 0.020),
                float4(0.640, 0.470, 0.72, 0.205),
                float4(0.245, 0.520, 1.18, 0.080),
                float4(0.470, 0.215, 0.55, 0.380),
                float4(0.855, 0.320, 0.84, 0.125)
            };

            // 개체마다 다른 시간표.
            //
            // 이게 없으면 전부 같은 순간에 죽는다 - 앞의 표에 등장 지연이 있어도 진행도를
            // 남은 시간으로 정규화하는 순간 종료 시각이 하나로 합쳐지고, 그러면 아홉 개가
            // 한꺼번에 사라져서 연출 전체가 인위적으로 보인다.
            // x = 수명, y = 맺히는 속도, z = 흘러내리기 시작(수명 비율), w = 흘러내리는 양
            static const float4 kDropletLife[9] =
            {
                float4(0.86, 0.13, 0.18, 0.62),
                float4(0.52, 0.07, 0.40, 0.20),
                float4(0.74, 0.22, 0.12, 0.48),
                float4(0.41, 0.05, 0.55, 0.14),
                float4(0.95, 0.17, 0.26, 0.71),
                float4(0.63, 0.11, 0.33, 0.30),
                float4(0.79, 0.28, 0.09, 0.58),
                float4(0.38, 0.06, 0.62, 0.11),
                float4(0.68, 0.15, 0.22, 0.40)
            };

            // 렌즈에 튄 물방울. 화면 고정 레이어이고 수면 통과 구간에만 산다.
            float2 LensDropletField(float2 screenUV, out float spec, out float meniscus)
            {
                spec = 0.0;
                meniscus = 0.0;
                float age = _LensDroplets.x;
                float strength = _LensDroplets.y;
                float seed = _LensDroplets.z;
                if (age < 0.0 || strength <= 0.0001)
                {
                    return float2(0.0, 0.0);
                }

                float2 origin = _LensOrigin.xy;
                float2 total = float2(0.0, 0.0);
                const float2 toLight = normalize(float2(-0.42, 0.86));

                [unroll]
                for (int i = 0; i < 9; i++)
                {
                    float4 entry = kDroplet[i];
                    float4 timing = kDropletLife[i];
                    // 자기 수명으로 나눈다. 남은 시간으로 나누면 종료 시각이 하나로 합쳐진다.
                    float local = (age - entry.w) / max(timing.x, 0.05);
                    if (local < 0.0 || local >= 1.0)
                    {
                        continue;
                    }

                    float h = frac(sin((float)(i + 1) * 12.9898 + seed * 78.233) * 43758.5453);
                    float h2 = frac(sin((float)(i + 1) * 41.113 + seed * 27.611) * 24634.6345);
                    float h3 = frac(sin((float)(i + 1) * 73.507 + seed * 19.377) * 31871.2214);
                    float h4 = frac(sin((float)(i + 1) * 97.421 + seed * 51.049) * 17453.9931);

                    // 스플래시마다 표 전체를 옮기고 뒤집는다.
                    float2 landing = entry.xy;
                    landing.x = frac(landing.x + seed);
                    if (frac(seed * 3.137) > 0.5)
                    {
                        landing.x = 1.0 - landing.x;
                    }

                    // 도착: 찌 쪽에서 바깥으로 짧게 날아와 맺힌다. 앞 24%가 비행 구간이다.
                    float2 away = landing - origin;
                    away = normalize(away + float2(0.0001, 0.0001));
                    float flight = saturate(local / 0.24);
                    float reach = 1.0 - exp(-3.8 * flight);
                    float2 position = landing - away * (0.10 + h2 * 0.16) * (1.0 - reach);

                    // 흘러내림이 이 효과의 끝을 담당한다. 제자리에서 페이드로 지우면
                    // 물이 마른 게 아니라 레이어를 끈 것처럼 보인다. 버티다 미끄러지는 것도,
                    // 곧바로 흘러내리는 것도 있어야 한다.
                    float slideT = saturate((local - timing.z) / max(1.0 - timing.z, 0.05));
                    float slide = slideT * slideT * (0.08 + h * 0.26 + timing.w * 0.62);
                    position.y -= slide;

                    // 날아오는 동안 커지고(렌즈에 가까워진다), 흘러내리며 작아진다(물이 빠진다).
                    float arrive = smoothstep(0.0, max(timing.y, 0.02), local);
                    float drain = 1.0 - slideT * (0.28 + timing.w * 0.58);
                    float radius = 0.041 * entry.z * arrive * drain;
                    if (radius < 0.0006)
                    {
                        continue;
                    }

                    float2 raw = screenUV - position;
                    // 화면 비율 보정 후 모양을 만든다.
                    float2 shaped = float2(raw.x * 2.15, raw.y);
                    // 흐르는 물방울은 위로 꼬리가 남는 눈물방울이다. 꼬리는 중력 방향이라
                    // 개체 축 회전보다 먼저 걸어야 한다.
                    if (shaped.y > 0.0)
                    {
                        shaped.y /= 1.0 + slide * 3.6;
                    }

                    // 개체마다 다른 축 방향과 축비. 전부 같은 방향의 같은 타원이면
                    // 물방울이 아니라 같은 도형을 여러 번 찍은 것으로 보인다.
                    float axis = h2 * 6.2831853;
                    float axisCos = cos(axis);
                    float axisSin = sin(axis);
                    shaped = float2(
                        shaped.x * axisCos + shaped.y * axisSin,
                        -shaped.x * axisSin + shaped.y * axisCos);
                    shaped.y /= 1.0 + h3 * 0.55;

                    float distance = length(shaped);
                    // 윤곽을 각도로 흔든다. 매끈한 원/타원은 렌즈에 맺힌 물이 아니라 도형이다.
                    // 서로 배수가 아닌 세 파수를 겹쳐 되풀이되는 요철이 안 생기게 한다.
                    float theta = atan2(shaped.y, shaped.x);
                    // 진폭이 크면 볼록함이 깨져 아메바가 된다. 실제 물방울은 불규칙하되
                    // 볼록하다 — 표면장력이 오목한 윤곽을 허용하지 않는다.
                    float wobble = 1.0
                        + 0.115 * sin(theta * 3.0 + h * 6.283)
                        + 0.075 * sin(theta * 2.0 - h3 * 6.283)
                        + 0.035 * sin(theta * 5.0 + h4 * 6.283);
                    float edge = radius * max(wobble, 0.45);
                    if (distance > edge)
                    {
                        continue;
                    }

                    float k = distance / max(edge, 1e-5);
                    float2 lensDirection = raw / max(length(raw), 1e-5);
                    // 사라지는 속도도 제각각이다. 어떤 건 툭 끊기고 어떤 건 길게 남는다.
                    float fade = saturate((1.0 - local) / (0.10 + h2 * 0.26));
                    float visible = arrive * fade * strength;

                    float bend = k * k * k;
                    total += lensDirection * (-bend) * radius * 5.20 * visible;

                    float lobe = saturate(dot(lensDirection, toLight));
                    // 메니스커스는 광원 반대쪽에 몰린다. 테두리를 한 바퀴 균일하게 두르면
                    // 물방울이 아니라 비눗방울 윤곽선이 된다.
                    float shade = 0.30 + 0.70 * (1.0 - lobe);
                    // 띠를 넓히고 세기를 낮춘다. 좁고 진하면 윤곽선을 그린 것이 된다.
                    meniscus = max(meniscus,
                        smoothstep(0.58, 0.93, k) * (1.0 - smoothstep(0.93, 1.0, k)) * shade * visible * 0.80);
                    spec = max(spec,
                        pow(lobe, 9.0) * smoothstep(0.34, 0.70, k) * (1.0 - smoothstep(0.76, 0.94, k)) * visible);
                }

                return total;
            }

            float OpticalConcentration(float2 uv, float shape)
            {
                return WaterOpticsCurvature(uv, _SurfacePhase, _OpticalDistortionScale, shape);
            }

            // Clarity is a sampling property of the underwater image, not a colour grade. A
            // small ring blur is enough to say "you are looking at this through a surface"
            // during the above-water and dive states, and it collapses to the single original
            // tap the moment clarity returns to 1.
            float4 SampleUnderwaterScene(float2 uv, float margin)
            {
                float4 center = SAMPLE_TEXTURE2D(_UnderwaterSceneTex, sampler_UnderwaterSceneTex, uv);
                float blur = saturate(1.0 - _UnderwaterClarity);
                if (blur <= 0.001)
                {
                    return center;
                }

                float radius = blur * 0.0085;
                float2 offsetX = float2(radius, 0.0);
                float2 offsetY = float2(0.0, radius);
                float2 low = margin.xx;
                float2 high = (1.0 - margin).xx;
                float4 accumulated = center * 0.36;
                accumulated += SAMPLE_TEXTURE2D(_UnderwaterSceneTex, sampler_UnderwaterSceneTex, clamp(uv + offsetX, low, high)) * 0.16;
                accumulated += SAMPLE_TEXTURE2D(_UnderwaterSceneTex, sampler_UnderwaterSceneTex, clamp(uv - offsetX, low, high)) * 0.16;
                accumulated += SAMPLE_TEXTURE2D(_UnderwaterSceneTex, sampler_UnderwaterSceneTex, clamp(uv + offsetY, low, high)) * 0.16;
                accumulated += SAMPLE_TEXTURE2D(_UnderwaterSceneTex, sampler_UnderwaterSceneTex, clamp(uv - offsetY, low, high)) * 0.16;
                return accumulated;
            }

            // Continuous, slow substrate field. Unlike the moving caustic, this field stays
            // attached to the pond so the viewer can feel a floor below the water.
            float BottomFloorValue(float2 uv, float time)
            {
                float scale = max(_BottomVariationScale, 0.05);
                float2 p = (uv - 0.5) * float2(2.9, 1.64) / scale;
                p += float2(sin(time * 0.010) * 0.035, cos(time * 0.008) * 0.025);
                float2 warp = float2(
                    ValueNoise(p * 0.42 + 17.0),
                    ValueNoise(p * 0.51 - 23.0));
                p += (warp - 0.5) * 1.15;
                float macro = ValueNoise(p * 0.82 + 31.0);
                float broad = ValueNoise(p * 1.47 + float2(7.0, -11.0));
                float middle = ValueNoise(p * 2.65 + 53.0);
                return saturate(macro * 0.52 + broad * 0.30 + middle * 0.18);
            }

            float BottomFloorGrain(float2 uv, float time)
            {
                float2 p = (uv - 0.5) * float2(19.0, 10.8);
                // Keep the grain anchored to the floor. Only an almost imperceptible drift
                // prevents a completely static image without turning material breakup into
                // another moving caustic layer.
                p += float2(time * 0.004, -time * 0.003);
                float grainA = ValueNoise(p + 101.0);
                float grainB = ValueNoise(p * 1.63 - 47.0);
                return saturate(grainA * 0.62 + grainB * 0.38);
            }

            float2 RotateSubstrateUV(float2 uv, float angle)
            {
                float s = sin(angle);
                float c = cos(angle);
                uv -= 0.5;
                return float2(uv.x * c - uv.y * s, uv.x * s + uv.y * c) + 0.5;
            }

            // The texture carries the material identity; the procedural fields only modulate
            // it. Pond-local UVs are fixed to the static WaterSurfaceV2 quad, so the substrate
            // does not swim with time or camera motion.
            float3 BottomSubstrateMaterial(float2 uv, float time, out float substrateValue)
            {
                float macro = BottomFloorValue(uv, time);
                float scale = max(_BottomTextureScale, 0.05);
                float2 primaryUV = frac(uv * scale);
                float3 primary = SAMPLE_TEXTURE2D(_BottomTexture, sampler_BottomTexture, primaryUV).rgb;

                float2 secondaryUV = RotateSubstrateUV(uv, 0.47);
                secondaryUV = frac(secondaryUV * (scale * 1.73) + float2(0.17, 0.31));
                float3 secondary = SAMPLE_TEXTURE2D(_BottomTexture, sampler_BottomTexture, secondaryUV).rgb;

                float secondaryBlend = saturate(_BottomSecondarySampleStrength * (0.38 + macro * 0.62));
                float3 textureMaterial = lerp(primary, secondary, secondaryBlend);
                float textureLuminance = dot(textureMaterial, float3(0.2126, 0.7152, 0.0722));
                float shapedLuminance = saturate((textureLuminance - 0.5) * _BottomTextureContrast + 0.5);
                textureMaterial += (shapedLuminance - textureLuminance) * float3(0.70, 0.82, 0.66);

                float3 proceduralTone = lerp(_BottomDarkColor.rgb, _BottomLightColor.rgb, macro);
                textureMaterial = lerp(proceduralTone, textureMaterial, saturate(_BottomTextureBlend));
                float macroModulation = lerp(0.84, 1.12, saturate(macro * 0.78 + shapedLuminance * 0.22));
                textureMaterial *= macroModulation;

                float grain = BottomFloorGrain(uv, time);
                textureMaterial += (grain - 0.5) * _BottomGrainStrength * 0.045;
                substrateValue = shapedLuminance;
                return saturate(textureMaterial);
            }

            float2 DomainWarp(float2 p, float time)
            {
                float warpX = ValueNoise(p * 0.48 + float2(time * 0.12, -time * 0.08));
                float warpY = ValueNoise(p * 0.62 + float2(-time * 0.09, time * 0.13) + 17.0);
                return p + (float2(warpX, warpY) - 0.5) * 1.35;
            }

            // Broad, low-frequency illumination. This is deliberately soft and secondary;
            // it changes the light field without becoming the only visible water pattern.
            float LargeCaustic(float2 uv, float time)
            {
                float scale = max(_LargeCausticScale, 0.05);
                float2 p = (uv - 0.5) * float2(3.25, 2.05) / scale;
                p += float2(time * _LargeCausticSpeed, -time * _LargeCausticSpeed * 0.72);
                float broad = ValueNoise(p * 0.92 + 4.0);
                float secondary = ValueNoise(p * 1.55 - float2(time * 0.06, time * 0.04) + 21.0);
                return smoothstep(0.38, 0.74, broad * 0.72 + secondary * 0.28);
            }

            // Mid-frequency floor light. The organic field comparison is retained as breakup,
            // but a broad carrier dominates so this reads as illumination on the substrate
            // instead of animated ink lines in the foreground.
            float MidCaustic(float2 uv, float time)
            {
                float scale = max(_MidCausticScale, 0.05);
                float2 p = (uv - 0.5) * float2(8.0, 4.5) / scale;
                p += float2(time * _MidCausticSpeed, -time * _MidCausticSpeed * 0.63);
                float2 q = lerp(p, DomainWarp(p, time * 0.42), 0.34);
                q += float2(sin(q.y * 0.70 + time * 0.52) * 0.13,
                    sin(q.x * 0.56 - time * 0.38) * 0.10);

                float fieldA = ValueNoise(q * 0.72 + float2(time * 0.08, -time * 0.06));
                float fieldB = ValueNoise(q * 1.06 - float2(time * 0.11, time * 0.07) + 13.0);
                float fieldC = ValueNoise(q * 1.62 + float2(-time * 0.14, time * 0.10) + 29.0);
                float fieldD = ValueNoise(q * 2.18 - float2(time * 0.18, -time * 0.13) + 47.0);

                float filamentA = 1.0 - smoothstep(0.030, 0.145, abs(fieldA - fieldB));
                float filamentB = 1.0 - smoothstep(0.022, 0.118, abs(fieldC - fieldD));
                float filamentC = 1.0 - smoothstep(0.040, 0.175, abs(fieldA - fieldC));
                float breakup = ValueNoise(q * 0.56 + float2(time * 0.16, -time * 0.10));
                float segmentMask = smoothstep(0.30, 0.78, breakup);
                float core = max(filamentA * 0.54, max(filamentB * 0.42, filamentC * 0.22));
                float soft = max(filamentA * 0.12, max(filamentB * 0.09, filamentC * 0.05));
                float broadCarrier = smoothstep(0.30, 0.78,
                    fieldA * 0.45 + fieldB * 0.25 + fieldC * 0.18 + fieldD * 0.12);
                float organicBreakup = saturate((core + soft) * (0.20 + 0.80 * segmentMask));
                return saturate(broadCarrier * 0.82 + organicBreakup * 0.08);
            }

            // High-frequency surface motion is kept below the threshold of a readable
            // object; its job is only to stop the plane from feeling frozen.
            float MicroSurface(float2 uv, float time)
            {
                float2 p = (uv - 0.5) * float2(32.0, 18.0);
                p += float2(time * 0.64, -time * 0.46);
                float a = sin(p.x * 1.15 + sin(p.y * 0.80 + time * 0.9) * 0.45);
                float b = sin(p.y * 1.38 + sin(p.x * 0.62 - time * 0.72) * 0.42);
                return smoothstep(0.64, 0.96, 0.5 + 0.5 * a * b);
            }

            half4 CompositeUnderwaterScene(float2 uv, float time)
            {
                float2 centered = uv - 0.5;
                float2 aspectPoint = centered * float2(1.56, 1.0);
                float radial = length(aspectPoint) * max(_VignetteScale, 0.05);
                float materialTime = _Time.y;
                float organic = ValueNoise(uv * 3.1 + float2(materialTime * 0.018, -materialTime * 0.014));
                float clearRadius = max(_ClearZoneRadius, 0.15);
                float clearZone = 1.0 - smoothstep(clearRadius * 0.22, clearRadius, radial + (organic - 0.5) * 0.045);
                clearZone = saturate(clearZone) * saturate(_ClearZoneStrength);

                float vertical = smoothstep(0.02, 0.98, uv.y);
                float edgeStart = max(0.12, _EdgeFogRadius);
                float edgeFog = smoothstep(edgeStart, 0.92, radial);
                float depth01 = saturate(1.0 - vertical);
                // 프로파일 물결 + 착수 국소 교란. 아래의 굴절·수면광·코스틱이 전부 이 값을
                // 공유해야 "찌가 떨어진 자리가 흔들린다"가 하나의 사건으로 읽힌다.
                float shape = EffectiveShape(uv);
                // The RT is anchored to the camera view; the quad uv is anchored to the world.
                // The session sends the exact affine map between the two, taken straight from
                // the camera's own projection of the quad corners, so this handles the
                // presentation zoom, the framing shift, and the camera's 180-degree yaw (which
                // puts world +X on the left of the screen) in one step instead of assuming the
                // two spaces happen to line up.
                float2 sceneUV = _UnderwaterUvOffset.xy + uv * _UnderwaterUvScale.xy;
                // Refraction is computed in quad uv, so it has to be carried into RT uv by the
                // same map - otherwise a mirrored axis would displace the scene the wrong way.
                float impactLight, impactShade;
                float2 impact = ImpactField(uv, impactLight, impactShade);
                float dropletSpec, dropletMeniscus;
                // 물방울은 화면 고정이라 월드 UV가 아니라 sceneUV(=뷰포트 좌표)를 쓴다.
                float2 droplet = LensDropletField(sceneUV, dropletSpec, dropletMeniscus);
                float2 displacement = (OpticalDisplacement(uv, shape) + impact) * _UnderwaterUvScale.xy + droplet;

                // 변위를 UV가 아니라 "남은 여유"로 제한한다.
                //
                // saturate로 UV를 잘라내면 RT 밖을 가리킨 샘플이 전부 마지막 픽셀 줄을 읽어
                // 화면 끝에 물고기가 늘어난 막대가 생긴다. 실제로 그렇게 보였다 — 굴절만으로도
                // 화면폭의 3%, 착수 파문은 6%, 렌즈 물방울은 25%까지 밀기 때문이다.
                //
                // 대신 각 픽셀에서 RT 가장자리까지 남은 거리만큼만 밀면 클램프 자체가 일어나지
                // 않는다. 가장자리에서는 효과가 잦아들 뿐 번지지 않는다. RT를 화면보다 넓게
                // 찍으므로(SyncUnderwaterCamera의 오버스캔) 화면 끝에도 여유가 남아 있다.
                const float sceneUvMargin = 0.002;
                float2 room = max(min(sceneUV, 1.0 - sceneUV) - sceneUvMargin, 0.0);
                float2 opticalUV = sceneUV + clamp(displacement, -room, room);
                float4 scene = SampleUnderwaterScene(opticalUV, sceneUvMargin);

                // The RT already contains the continuous bottom, fish, shadow, and
                // environment. Apply only a restrained volume tint here so every foreground
                // element receives the same optical absorption after the shared refraction.
                float absorption = saturate((edgeFog * 0.30 + (1.0 - clearZone) * 0.08 + depth01 * 0.04) * _AbsorptionStrength);
                float3 absorptionTint = lerp(float3(0.97, 0.995, 1.03), float3(0.76, 0.89, 0.98), absorption);
                float opticalLight = OpticalLightField(uv, shape);
                float3 sceneColor = scene.rgb * absorptionTint;
                sceneColor *= lerp(0.97, 1.025, opticalLight);
                // This is the visible surface layer. It is a broad normal/light response from
                // the shared height field, so the entire RT scene remains optically coherent
                // while refraction stays at the existing restrained strength.
                float surfaceRipple = WaterOpticsSurfaceHighlight(
                    uv,
                    _SurfacePhase,
                    _OpticalDistortionScale,
                    shape);
                float surfaceEnergy = smoothstep(0.24, 0.54, surfaceRipple);
                sceneColor *= 1.0 + (surfaceEnergy - 0.42) * 0.13 * _SurfaceHighlightStrength;
                sceneColor += _SurfaceRippleColor.rgb * surfaceEnergy * _SurfaceRippleStrength * 0.42;

                // ---- Presentation surface block -------------------------------------------
                // All four terms below are gated by profile strengths that are 0 in the
                // gameplay profile, so this block is inert during play and only becomes the
                // subject of the frame while the water itself is the hero.
                if (shape > 0.0001)
                {
                    // Visible ripple shape: the lit flank brightens and the far flank darkens
                    // around the same crest, which is what turns "abstract colour variation"
                    // into a surface whose form can be read.
                    float ridge = WaterOpticsRidgeBand(uv, _SurfacePhase, _OpticalDistortionScale, shape);
                    // Contrast first, tint second. A crest read as a brightness step keeps the
                    // scene below visible through it; a crest read as added colour paints over
                    // the water and the fish with it.
                    //
                    // The two flanks are not symmetric. A lit flank can take a large gain, but
                    // the far flank of a real crest still transmits the water below it - drop
                    // it as hard as you lift the other and the troughs turn into solid ribbons
                    // that hide the fish.
                    float ridgeGain = ridge > 0.0 ? 0.27 : 0.15;
                    sceneColor *= 1.0 + ridge * ridgeGain * shape;
                    sceneColor += _SurfaceRippleColor.rgb * saturate(ridge) * _SurfaceRippleStrength * 0.24 * shape;
                }

                if (_SurfaceSpecularStrength > 0.0001)
                {
                    float specular = WaterOpticsSpecular(uv, _SurfacePhase, _OpticalDistortionScale, shape);
                    sceneColor += _SurfaceSpecularColor.rgb * specular * _SurfaceSpecularStrength * 0.42;
                }

                if (_SurfaceReflectionStrength > 0.0001)
                {
                    // The reflection hint has to take something away as well as add: a surface
                    // that returns sky necessarily hides part of what is beneath it. That
                    // subtraction is the actual cue that a boundary exists.
                    //
                    // It cannot be driven by fresnel alone. Looking straight down at calm water
                    // the fresnel term is genuinely almost zero, so a purely fresnel-driven
                    // reflection disappears exactly in the state that needs it most. What the
                    // eye actually reads from above is broad drifting sky glare, so the bulk of
                    // this comes from the low-frequency surface lobes; fresnel only adds the
                    // extra return on whatever crest flanks exist.
                    float fresnel = WaterOpticsFresnel(uv, _SurfacePhase, _OpticalDistortionScale, shape);
                    // opticalLight is the broad surface field centred on 0.5, so a smoothstep
                    // across it gives soft patches that actually cover part of the screen.
                    // surfaceRipple was the wrong source: on a near-flat surface its whole
                    // range sits around 0.15, below any threshold that would band it.
                    float glare = smoothstep(0.40, 0.72, opticalLight);
                    float reflection = saturate((0.14 + glare * 0.74 + fresnel * 0.38) * _SurfaceReflectionStrength);
                    sceneColor = lerp(sceneColor, _SurfaceReflectionColor.rgb, saturate(reflection * 0.42));
                    sceneColor += _SurfaceReflectionColor.rgb * glare * _SurfaceReflectionStrength * 0.10;
                }
                // ---------------------------------------------------------------------------

                // 착수는 프로파일과 무관하게 항상 보인다. gameplay 중에도 일어나는 사건이다.
                // 그늘(함몰)을 먼저 빼고 빛(크라운·제트·파문)을 더한다 - 순서가 반대면
                // 구덩이가 안 파이고 전체가 밝아지기만 한다.
                if (impactShade > 0.0001)
                {
                    sceneColor *= 1.0 - saturate(impactShade) * 0.46;
                }
                if (impactLight > 0.0001)
                {
                    sceneColor *= 1.0 + impactLight * 0.26;
                    // 링 색만 더하면 어두운 청록이라 화면에서 사라진다. 하늘빛을 섞어 올린다.
                    sceneColor += lerp(_SurfaceRippleColor.rgb, _SurfaceReflectionColor.rgb, 0.60)
                        * impactLight * 0.80;
                }

                // 기포. 찌가 끌고 들어간 공기(떠오름)와 렌즈를 스치는 기포(전진 시차).
                float bubbles = RisingBubbles(uv, _CastBubbles, _Impacts[0].xy);
                bubbles = max(bubbles, RushingBubbles(uv, _CrossBubbles));
                if (bubbles > 0.0001)
                {
                    sceneColor += float3(0.62, 0.86, 0.95) * bubbles * 0.95;
                }

                if (dropletMeniscus > 0.0001 || dropletSpec > 0.0001)
                {
                    sceneColor *= 1.0 - dropletMeniscus * 0.34;
                    sceneColor += _SurfaceReflectionColor.rgb * dropletSpec * 1.05;
                }

                float sceneAlpha = saturate(scene.a);
                float3 fallback = lerp(_DeepColor.rgb, _DeepWaterColor.rgb, saturate(edgeFog * 0.55 + depth01 * 0.12));
                return half4(saturate(lerp(fallback, sceneColor, sceneAlpha)), 1.0);
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float opticsTime = _OpticalTime;
                float materialTime = _Time.y;
                if (_UnderwaterComposite > 0.5)
                {
                    return CompositeUnderwaterScene(input.uv, opticsTime);
                }

                float2 centered = input.uv - 0.5;
                float2 aspectPoint = centered * float2(1.56, 1.0);
                float radial = length(aspectPoint) * max(_VignetteScale, 0.05);
                float organic = ValueNoise(input.uv * 3.1 + float2(materialTime * 0.018, -materialTime * 0.014));
                float clearRadius = max(_ClearZoneRadius, 0.15);
                float clearZone = 1.0 - smoothstep(clearRadius * 0.22, clearRadius, radial + (organic - 0.5) * 0.045);
                clearZone = saturate(clearZone) * saturate(_ClearZoneStrength);

                float vertical = smoothstep(0.02, 0.98, input.uv.y);
                float3 color = lerp(_DeepColor.rgb, _ShallowColor.rgb, vertical * 0.78 + 0.08);

                float edgeStart = max(0.12, _EdgeFogRadius);
                float edgeFog = smoothstep(edgeStart, 0.92, radial);
                float depth01 = saturate(1.0 - vertical);

                // The substrate exists across the entire pond, but shallow/clear regions reveal
                // more of it while deeper water and the perimeter keep it restrained.
                float floorDepthVisibility = lerp(0.55, 1.0, smoothstep(0.03, 0.95, vertical));
                float bottomVisibility = saturate(_BottomVisibility * (0.30 + clearZone * 0.70));
                bottomVisibility *= floorDepthVisibility;
                bottomVisibility *= 1.0 - saturate(edgeFog) * 0.58;
                float substrateValue = 0.5;
                float bottomShape = EffectiveShape(input.uv);
                float2 opticalUV = saturate(input.uv + OpticalDisplacement(input.uv, bottomShape));
                float3 bottomColor = BottomSubstrateMaterial(opticalUV, materialTime, substrateValue);
                // Blend a continuous floor through the water, rather than adding isolated
                // decals. Clear water reveals more of it; edge murk hides it again.
                color = lerp(color, bottomColor, saturate(bottomVisibility * 0.82));

                // Only a small amount of the substrate palette bleeds into the water volume;
                // the floor remains material-colored while the water stays predominantly teal.
                float3 bottomBleed = lerp(_BottomBleedColor.rgb, bottomColor, 0.16 + substrateValue * 0.12);
                color = lerp(color, bottomBleed, saturate(bottomVisibility * _BottomColorBleed));

                // Reapply broad water-volume color after the floor is visible. This keeps the
                // substrate material readable while clear areas remain cyan-teal and deep
                // perimeter water shifts toward blue/navy instead of becoming uniformly olive.
                float clearColorInfluence = saturate(clearZone * _ClearColorStrength);
                color = lerp(color, _ClearWaterColor.rgb, clearColorInfluence);
                float deepColorInfluence = saturate((1.0 - clearZone) * 0.36 + edgeFog * 0.64);
                deepColorInfluence *= _DepthColorStrength * (0.54 + depth01 * 0.46);
                color = lerp(color, _DeepWaterColor.rgb, saturate(deepColorInfluence));

                // Clear water is not a white spotlight: it is slightly less saturated,
                // a touch brighter, and less fogged so the center reads as observable depth.
                float baseLuminance = dot(color, float3(0.2126, 0.7152, 0.0722));
                float3 clearerBase = lerp(color, baseLuminance.xxx, 0.09);
                clearerBase = (clearerBase - 0.5) * (1.0 + clearZone * 0.12) + 0.5;
                clearerBase *= 1.0 + clearZone * 0.07;
                color = lerp(color, clearerBase, clearZone);

                edgeFog *= _EdgeFogStrength * (1.0 - clearZone * 0.24);
                color *= lerp(float3(1.0, 1.0, 1.0), float3(0.74, 0.87, 0.92), saturate(edgeFog));

                float layerVisibility = lerp(0.76, 1.12, clearZone);
                float large = LargeCaustic(opticalUV, opticsTime);
                float mid = MidCaustic(opticalUV, opticsTime);
                float micro = MicroSurface(opticalUV, opticsTime);

                float floorReceiver = saturate(0.45 + bottomVisibility * _OpticalCausticFloorBias);
                float causticVisibility = layerVisibility * lerp(0.52, 1.0, floorReceiver);
                float opticalLight = OpticalLightField(input.uv, bottomShape);
                float3 largeFloorLight = lerp(_LargeCausticColor.rgb, bottomColor * 1.16, 0.38 + floorReceiver * 0.24);
                float3 midFloorLight = lerp(_MidCausticColor.rgb, bottomColor * 1.24, 0.48 + floorReceiver * 0.28);
                float opticalConcentration = OpticalConcentration(opticalUV, bottomShape);
                // Keep floor illumination tied to the same surface slope that is visible above;
                // this makes transmitted light and surface highlight move as one water surface.
                float surfaceLight = WaterOpticsSurfaceHighlight(
                    opticalUV,
                    _SurfacePhase,
                    _OpticalDistortionScale,
                    bottomShape);
                causticVisibility *= lerp(0.88, 1.12, opticalConcentration);
                float curvatureLight = smoothstep(0.24, 0.76, opticalConcentration);
                // The stronger the visible surface, the more of the floor light comes directly
                // from that surface's slope. This is what keeps "wave -> transmitted light ->
                // bottom" legible as one causal chain in the presentation state.
                float broadFloorLight = saturate(
                    0.50
                    + (large - 0.50) * 0.72
                    + (opticalConcentration - 0.50) * 1.10
                    + (surfaceLight - 0.42) * (0.24 + 0.55 * bottomShape));
                float softFloorLight = saturate(mid * 0.58 + large * 0.10 + curvatureLight * 0.32);

                // Convert the receiver signal into a restrained floor-light energy change as
                // well as the tinted light contribution below. This is what makes the caustic
                // read as broad illumination on the bottom rather than a colored overlay.
                float floorLightShift = (broadFloorLight - 0.50) * _LargeCausticStrength * 0.82 * causticVisibility;
                color *= 1.0 + floorLightShift;

                // A soft projected-light carrier makes the surface slope visible even when the
                // substrate itself is dark. It is bottom-only, curvature-driven, and has no
                // line-art edge, so the result reads as moving illumination rather than ink.
                float floorLightMask = saturate(0.34 + curvatureLight * 0.54 + large * 0.12);
                float3 projectedFloorLight = lerp(_LargeCausticColor.rgb * 0.82, _MidCausticColor.rgb * 1.08, curvatureLight);
                color += projectedFloorLight * floorLightMask * _LargeCausticStrength * 0.18 * causticVisibility;

                // Caustic is projected floor light: it shares the optical UV and receives the
                // substrate palette instead of reading as a foreground line-art overlay.
                color += largeFloorLight * broadFloorLight * _LargeCausticStrength * 0.78 * causticVisibility;
                color += midFloorLight * softFloorLight * _MidCausticStrength * 0.25 * causticVisibility;
                color += _MicroSurfaceColor.rgb * micro * _MicroSurfaceStrength * 0.20 * lerp(0.72, 1.0, opticalLight);

                return half4(saturate(color), 1.0);
            }
            ENDHLSL
        }
    }
}
