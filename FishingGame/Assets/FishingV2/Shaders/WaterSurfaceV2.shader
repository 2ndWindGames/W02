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
        _BottomTextureScale ("Bottom / Texture Scale", Range(0.25, 3)) = 1.55
        _BottomTextureBlend ("Bottom / Texture Blend", Range(0, 1)) = 0.66
        _BottomTextureContrast ("Bottom / Texture Contrast", Range(0, 2)) = 1.08
        _BottomSecondarySampleStrength ("Bottom / Secondary Sample", Range(0, 1)) = 0.42
        _OpticalDistortionStrength ("Optics / Surface Refraction", Range(0, 1)) = 0.42
        _OpticalDistortionScale ("Optics / Refraction Scale", Range(0.25, 2.5)) = 1.08
        _OpticalDistortionSpeed ("Optics / Refraction Speed", Range(0, 1)) = 0.18
        _OpticalTime ("Optics / Unscaled Time", Float) = 0
        _OpticalCausticFloorBias ("Optics / Caustic Floor Bias", Range(0, 1)) = 0.78
        _LargeCausticColor ("Large Caustic Color", Color) = (0.045, 0.115, 0.105, 1)
        _LargeCausticStrength ("Large Caustic Strength", Range(0, 2)) = 0.32
        _LargeCausticScale ("Large Caustic Scale", Range(0.25, 3)) = 1.35
        _LargeCausticSpeed ("Large Caustic Speed", Range(0, 1)) = 0.06
        _MidCausticColor ("Mid Caustic Color", Color) = (0.075, 0.185, 0.155, 1)
        _MidCausticStrength ("Mid Caustic Strength", Range(0, 2)) = 0.28
        _MidCausticScale ("Mid Caustic Scale", Range(0.25, 3)) = 1.08
        _MidCausticSpeed ("Mid Caustic Speed", Range(0, 1)) = 0.18
        _MicroSurfaceColor ("Surface Micro Color", Color) = (0.022, 0.060, 0.058, 1)
        _MicroSurfaceStrength ("Surface Micro Strength", Range(0, 2)) = 0.14
        _SurfaceRippleColor ("Surface Ripple / Highlight Color", Color) = (0.040, 0.160, 0.180, 1)
        _SurfaceRippleStrength ("Surface Ripple / Highlight Strength", Range(0, 2)) = 0.85
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
                float _OpticalDistortionStrength;
                float _OpticalDistortionScale;
                float _OpticalDistortionSpeed;
                float _OpticalTime;
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

            float2 OpticalDisplacement(float2 uv, float time)
            {
                return WaterOpticsRefraction(
                    uv,
                    time,
                    _OpticalDistortionScale,
                    _OpticalDistortionSpeed,
                    _OpticalDistortionStrength);
            }

            float OpticalLightField(float2 uv, float time)
            {
                return WaterOpticsLight(uv, time, _OpticalDistortionScale, _OpticalDistortionSpeed);
            }

            // A small finite-difference estimate links the soft floor-light variation to the
            // same low-frequency surface field used for scene refraction. It is intentionally
            // broad and low contrast; it should read as light concentration on the substrate,
            // not as a second animated line texture.
            float OpticalConcentration(float2 uv, float time)
            {
                return WaterOpticsCurvature(uv, time, _OpticalDistortionScale, _OpticalDistortionSpeed);
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
                float radial = length(aspectPoint);
                float materialTime = _Time.y;
                float organic = ValueNoise(uv * 3.1 + float2(materialTime * 0.018, -materialTime * 0.014));
                float clearRadius = max(_ClearZoneRadius, 0.15);
                float clearZone = 1.0 - smoothstep(clearRadius * 0.22, clearRadius, radial + (organic - 0.5) * 0.045);
                clearZone = saturate(clearZone) * saturate(_ClearZoneStrength);

                float vertical = smoothstep(0.02, 0.98, uv.y);
                float edgeStart = max(0.12, _EdgeFogRadius);
                float edgeFog = smoothstep(edgeStart, 0.92, radial);
                float depth01 = saturate(1.0 - vertical);
                float2 opticalUV = saturate(uv + OpticalDisplacement(uv, time));
                // Keep the final scene sample one or two pixels inside the RT edge. The
                // overscanned bottom quad prevents most edge hits; this safe margin also keeps
                // a displaced sample from exposing a bright clamped border.
                const float sceneUvMargin = 0.002;
                opticalUV = opticalUV * (1.0 - sceneUvMargin * 2.0) + sceneUvMargin;
                float4 scene = SAMPLE_TEXTURE2D(_UnderwaterSceneTex, sampler_UnderwaterSceneTex, opticalUV);

                // The RT already contains the continuous bottom, fish, shadow, and
                // environment. Apply only a restrained volume tint here so every foreground
                // element receives the same optical absorption after the shared refraction.
                float absorption = saturate(edgeFog * 0.30 + (1.0 - clearZone) * 0.08 + depth01 * 0.04);
                float3 absorptionTint = lerp(float3(0.97, 0.995, 1.03), float3(0.76, 0.89, 0.98), absorption);
                float opticalLight = OpticalLightField(uv, time);
                float3 sceneColor = scene.rgb * absorptionTint;
                sceneColor *= lerp(0.97, 1.025, opticalLight);
                // This is the visible surface layer. It is a broad normal/light response from
                // the shared height field, so the entire RT scene remains optically coherent
                // while refraction stays at the existing restrained strength.
                float surfaceRipple = WaterOpticsSurfaceHighlight(
                    uv,
                    time,
                    _OpticalDistortionScale,
                    _OpticalDistortionSpeed);
                float surfaceEnergy = smoothstep(0.24, 0.54, surfaceRipple);
                sceneColor *= 1.0 + (surfaceEnergy - 0.42) * 0.13;
                sceneColor += _SurfaceRippleColor.rgb * surfaceEnergy * _SurfaceRippleStrength * 0.42;
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
                float radial = length(aspectPoint);
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
                float2 opticalUV = saturate(input.uv + OpticalDisplacement(input.uv, opticsTime));
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
                float opticalLight = OpticalLightField(input.uv, opticsTime);
                float3 largeFloorLight = lerp(_LargeCausticColor.rgb, bottomColor * 1.16, 0.38 + floorReceiver * 0.24);
                float3 midFloorLight = lerp(_MidCausticColor.rgb, bottomColor * 1.24, 0.48 + floorReceiver * 0.28);
                float opticalConcentration = OpticalConcentration(opticalUV, opticsTime);
                // Keep floor illumination tied to the same surface slope that is visible above;
                // this makes transmitted light and surface highlight move as one water surface.
                float surfaceLight = WaterOpticsSurfaceHighlight(
                    opticalUV,
                    opticsTime,
                    _OpticalDistortionScale,
                    _OpticalDistortionSpeed);
                causticVisibility *= lerp(0.88, 1.12, opticalConcentration);
                float curvatureLight = smoothstep(0.24, 0.76, opticalConcentration);
                float broadFloorLight = saturate(
                    0.50
                    + (large - 0.50) * 0.72
                    + (opticalConcentration - 0.50) * 1.10
                    + (surfaceLight - 0.42) * 0.24);
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
