using UnityEngine;

namespace Fishing.V2
{
    public enum FishingV2PresentationVariant
    {
        CalmObservation = 0,
        CasualFishing = 1
    }

    /// <summary>
    /// Prototype-only presentation presets. Gameplay tuning remains in FishingV2TuningAsset;
    /// these values only define the visual comparison and depth/light contract.
    /// </summary>
    public struct FishingV2PresentationSettings
    {
        public FishingV2PresentationVariant Variant;
        public string DisplayName;

        public float TailLengthScale;
        public float TailSpreadScale;
        public float TailNotchScale;
        public float SquidArmLengthScale;
        public float SquidOuterArmScale;
        public float SquidArmSpreadScale;
        public float WaveAmplitudeMultiplier;
        public float RimStrength;
        public float BobberScale;

        public Color WaterDeepColor;
        public Color WaterShallowColor;
        public Color WaterClearColor;
        public Color WaterDeepNavyColor;
        public Color WaterBottomBleedColor;
        public float WaterClearColorStrength;
        public float WaterDepthColorStrength;
        public float WaterBottomColorBleed;
        public float WaterOpticalDistortionStrength;
        public float WaterOpticalDistortionScale;
        public float WaterOpticalDistortionSpeed;
        public float WaterOpticalCausticFloorBias;
        // Kept so existing scene YAML and comparison tooling remain loadable. The coherent
        // RT path applies refraction once to the whole underwater image instead of consuming
        // these values as per-object offsets/light modulation.
        public float WaterFishOpticalStrength;
        public float WaterShadowOpticalStrength;
        public float WaterBobberOpticalStrength;
        public Color WaterBottomDarkColor;
        public Color WaterBottomLightColor;
        public float WaterBottomVisibility;
        public float WaterBottomGrainStrength;
        public float WaterBottomVariationScale;
        public Texture2D WaterBottomTexture;
        public float WaterBottomTextureScale;
        public float WaterBottomTextureBlend;
        public float WaterBottomTextureContrast;
        public float WaterBottomSecondarySampleStrength;
        public Color WaterLargeCausticColor;
        public float WaterLargeCausticStrength;
        public float WaterLargeCausticScale;
        public float WaterLargeCausticSpeed;
        public Color WaterMidCausticColor;
        public float WaterMidCausticStrength;
        public float WaterMidCausticScale;
        public float WaterMidCausticSpeed;
        public Color WaterMicroSurfaceColor;
        public float WaterMicroSurfaceStrength;
        public float WaterClearZoneStrength;
        public float WaterClearZoneRadius;
        public float WaterEdgeFogStrength;
        public float WaterEdgeFogRadius;
        public Color AccentColor;
        public float EnvironmentSilhouetteStrength;

        public Vector3 LightDirection;
        public float FishSurfaceZ;
        public float FishBottomZ;
        public float FishDepthTintStrength;
        public float FishDepthDesaturation;
        public float FishDepthBrightnessDrop;
        public float FishDepthContrast;
        public float HeroContrastStrength;
        public float ShadowBottomZ;
        public float ShadowDepthInfluence;
        public float ShadowMinOpacity;
        public float ShadowMaxOpacity;
        public float ShadowMinScale;
        public float ShadowMaxScale;
        public float ShadowSoftness;

        public bool CompactHud;
        public bool ShowSpeciesCounters;

        public static FishingV2PresentationSettings For(FishingV2PresentationVariant variant)
        {
            if (variant == FishingV2PresentationVariant.CasualFishing)
            {
                return new FishingV2PresentationSettings
                {
                    Variant = variant,
                    DisplayName = "B · 캐주얼 낚시",
                    TailLengthScale = 1.00f,
                    TailSpreadScale = 1.00f,
                    TailNotchScale = 1.00f,
                    SquidArmLengthScale = 1.00f,
                    SquidOuterArmScale = 1.00f,
                    SquidArmSpreadScale = 1.00f,
                    WaveAmplitudeMultiplier = 1.00f,
                    RimStrength = 1.05f,
                    BobberScale = 0.30f,
                    WaterDeepColor = new Color(0.028f, 0.115f, 0.150f, 1f),
                    WaterShallowColor = new Color(0.115f, 0.320f, 0.365f, 1f),
                    WaterClearColor = new Color(0.020f, 0.340f, 0.460f, 1f),
                    WaterDeepNavyColor = new Color(0.002f, 0.015f, 0.065f, 1f),
                    WaterBottomBleedColor = new Color(0.080f, 0.120f, 0.070f, 1f),
                    WaterClearColorStrength = 0.58f,
                    WaterDepthColorStrength = 0.56f,
                    WaterBottomColorBleed = 0.13f,
                    WaterOpticalDistortionStrength = 0.72f,
                    WaterOpticalDistortionScale = 1.00f,
                    WaterOpticalDistortionSpeed = 0.22f,
                    WaterOpticalCausticFloorBias = 0.76f,
                    WaterFishOpticalStrength = 0.20f,
                    WaterShadowOpticalStrength = 0.52f,
                    WaterBobberOpticalStrength = 0.40f,
                    WaterBottomDarkColor = new Color(0.055f, 0.185f, 0.135f, 1f),
                    WaterBottomLightColor = new Color(0.320f, 0.450f, 0.250f, 1f),
                    WaterBottomVisibility = 0.80f,
                    WaterBottomGrainStrength = 0.07f,
                    WaterBottomVariationScale = 0.98f,
                    WaterBottomTextureScale = 1.70f,
                    WaterBottomTextureBlend = 0.70f,
                    WaterBottomTextureContrast = 1.10f,
                    WaterBottomSecondarySampleStrength = 0.45f,
                    WaterLargeCausticColor = new Color(0.170f, 0.360f, 0.280f, 1f),
                    WaterLargeCausticStrength = 0.72f,
                    WaterLargeCausticScale = 1.20f,
                    WaterLargeCausticSpeed = 0.085f,
                    WaterMidCausticColor = new Color(0.235f, 0.490f, 0.370f, 1f),
                    WaterMidCausticStrength = 0.38f,
                    WaterMidCausticScale = 1.00f,
                    WaterMidCausticSpeed = 0.24f,
                    WaterMicroSurfaceColor = new Color(0.045f, 0.120f, 0.100f, 1f),
                    WaterMicroSurfaceStrength = 0.16f,
                    WaterClearZoneStrength = 0.72f,
                    WaterClearZoneRadius = 0.78f,
                    WaterEdgeFogStrength = 0.22f,
                    WaterEdgeFogRadius = 0.48f,
                    AccentColor = new Color(1.00f, 0.56f, 0.16f, 1f),
                    EnvironmentSilhouetteStrength = 1.00f,
                    LightDirection = new Vector3(-0.36f, 0.58f, 0.73f),
                    FishSurfaceZ = 0.38f,
                    FishBottomZ = 0.12f,
                    FishDepthTintStrength = 0.68f,
                    FishDepthDesaturation = 0.16f,
                    FishDepthBrightnessDrop = 0.20f,
                    FishDepthContrast = 0.10f,
                    HeroContrastStrength = 0.10f,
                    ShadowBottomZ = -0.06f,
                    ShadowDepthInfluence = 0.92f,
                    ShadowMinOpacity = 0.20f,
                    ShadowMaxOpacity = 0.44f,
                    ShadowMinScale = 0.98f,
                    ShadowMaxScale = 1.14f,
                    ShadowSoftness = 0.28f,
                    CompactHud = false,
                    ShowSpeciesCounters = true
                };
            }

            return new FishingV2PresentationSettings
            {
                Variant = FishingV2PresentationVariant.CalmObservation,
                DisplayName = "A · 수중 관찰",
                // Compact/legacy-looking silhouette: shorter tail and the earlier, tighter
                // squid-leg read. The HTML-reference bend is also softened for observation.
                TailLengthScale = 0.84f,
                TailSpreadScale = 0.90f,
                TailNotchScale = 0.95f,
                SquidArmLengthScale = 0.78f,
                SquidOuterArmScale = 1.00f,
                SquidArmSpreadScale = 0.86f,
                WaveAmplitudeMultiplier = 0.78f,
                RimStrength = 0.65f,
                BobberScale = 0.25f,
                WaterDeepColor = new Color(0.018f, 0.088f, 0.120f, 1f),
                WaterShallowColor = new Color(0.078f, 0.260f, 0.310f, 1f),
                WaterClearColor = new Color(0.015f, 0.300f, 0.420f, 1f),
                WaterDeepNavyColor = new Color(0.002f, 0.015f, 0.060f, 1f),
                WaterBottomBleedColor = new Color(0.065f, 0.095f, 0.055f, 1f),
                WaterClearColorStrength = 0.52f,
                WaterDepthColorStrength = 0.52f,
                WaterBottomColorBleed = 0.09f,
                WaterOpticalDistortionStrength = 0.68f,
                WaterOpticalDistortionScale = 1.08f,
                WaterOpticalDistortionSpeed = 0.18f,
                WaterOpticalCausticFloorBias = 0.78f,
                WaterFishOpticalStrength = 0.16f,
                WaterShadowOpticalStrength = 0.48f,
                WaterBobberOpticalStrength = 0.34f,
                WaterBottomDarkColor = new Color(0.050f, 0.165f, 0.125f, 1f),
                WaterBottomLightColor = new Color(0.285f, 0.405f, 0.230f, 1f),
                WaterBottomVisibility = 0.72f,
                WaterBottomGrainStrength = 0.06f,
                WaterBottomVariationScale = 1.12f,
                WaterBottomTextureScale = 1.55f,
                WaterBottomTextureBlend = 0.66f,
                WaterBottomTextureContrast = 1.08f,
                WaterBottomSecondarySampleStrength = 0.42f,
                WaterLargeCausticColor = new Color(0.160f, 0.350f, 0.275f, 1f),
                WaterLargeCausticStrength = 0.68f,
                WaterLargeCausticScale = 1.35f,
                WaterLargeCausticSpeed = 0.060f,
                WaterMidCausticColor = new Color(0.225f, 0.470f, 0.360f, 1f),
                WaterMidCausticStrength = 0.36f,
                WaterMidCausticScale = 1.08f,
                WaterMidCausticSpeed = 0.18f,
                WaterMicroSurfaceColor = new Color(0.030f, 0.082f, 0.072f, 1f),
                WaterMicroSurfaceStrength = 0.14f,
                WaterClearZoneStrength = 0.62f,
                WaterClearZoneRadius = 0.76f,
                WaterEdgeFogStrength = 0.38f,
                WaterEdgeFogRadius = 0.44f,
                AccentColor = new Color(0.95f, 0.72f, 0.34f, 1f),
                EnvironmentSilhouetteStrength = 1.08f,
                LightDirection = new Vector3(-0.36f, 0.58f, 0.73f),
                FishSurfaceZ = 0.38f,
                FishBottomZ = 0.12f,
                FishDepthTintStrength = 0.82f,
                FishDepthDesaturation = 0.20f,
                FishDepthBrightnessDrop = 0.24f,
                FishDepthContrast = 0.08f,
                HeroContrastStrength = 0.08f,
                ShadowBottomZ = -0.06f,
                ShadowDepthInfluence = 0.92f,
                ShadowMinOpacity = 0.16f,
                ShadowMaxOpacity = 0.38f,
                ShadowMinScale = 0.96f,
                ShadowMaxScale = 1.12f,
                ShadowSoftness = 0.34f,
                CompactHud = true,
                ShowSpeciesCounters = false
            };
        }
    }

    /// <summary>
    /// Optional inspector-level overrides for the visual contract. Gameplay tuning remains
    /// separate, and disabling this object leaves the selected A/B preset untouched.
    /// </summary>
    [System.Serializable]
    public sealed class FishingV2PresentationOverrides
    {
        public bool Enabled;

        [Header("Water")]
        [Range(0f, 2f)] public float ClearZoneStrength = 0.56f;
        [Range(0.15f, 2.5f)] public float ClearZoneRadius = 0.76f;
        [Range(0f, 1f)] public float ClearColorStrength = 0.52f;
        [Range(0f, 1f)] public float DepthColorStrength = 0.52f;
        [Range(0f, 1f)] public float BottomColorBleed = 0.09f;
        [Header("Water optics")]
        [Range(0f, 1f)] public float OpticalDistortionStrength = 0.68f;
        [Range(0.25f, 2.5f)] public float OpticalDistortionScale = 1.08f;
        [Range(0f, 1f)] public float OpticalDistortionSpeed = 0.18f;
        [Range(0f, 1f)] public float OpticalCausticFloorBias = 0.78f;
        [Range(0f, 1f)] public float FishOpticalStrength = 0.16f;
        [Range(0f, 1f)] public float ShadowOpticalStrength = 0.48f;
        [Range(0f, 1f)] public float BobberOpticalStrength = 0.34f;
        [Range(0f, 1.5f)] public float BottomVisibility = 0.72f;
        [Range(0f, 1f)] public float BottomGrainStrength = 0.06f;
        [Range(0.25f, 3f)] public float BottomVariationScale = 1.12f;
        [Range(0.25f, 3f)] public float BottomTextureScale = 1.55f;
        [Range(0f, 1f)] public float BottomTextureBlend = 0.66f;
        [Range(0f, 2f)] public float BottomTextureContrast = 1.08f;
        [Range(0f, 1f)] public float BottomSecondarySampleStrength = 0.42f;
        [Range(0f, 2f)] public float LargeCausticStrength = 0.58f;
        [Range(0.25f, 3f)] public float LargeCausticScale = 1.35f;
        [Range(0f, 1f)] public float LargeCausticSpeed = 0.06f;
        [Range(0f, 2f)] public float MidCausticStrength = 0.30f;
        [Range(0.25f, 3f)] public float MidCausticScale = 1.08f;
        [Range(0f, 1f)] public float MidCausticSpeed = 0.18f;
        [Range(0f, 2f)] public float MicroSurfaceStrength = 0.14f;
        [Range(0f, 2f)] public float EdgeFogStrength = 0.42f;
        [Range(0.15f, 1.2f)] public float EdgeFogRadius = 0.44f;
        [Range(0f, 2f)] public float EnvironmentSilhouetteStrength = 1.08f;

        [Header("Fish depth / light")]
        [Range(0f, 2f)] public float FishDepthTintStrength = 0.82f;
        [Range(0f, 1f)] public float FishDepthDesaturation = 0.20f;
        [Range(0f, 1f)] public float FishDepthBrightnessDrop = 0.24f;
        [Range(0f, 1f)] public float FishDepthContrast = 0.08f;
        [Range(0f, 1f)] public float HeroContrastStrength = 0.08f;
        [Range(0f, 2f)] public float ShadowDepthInfluence = 0.92f;
        [Range(0f, 1f)] public float ShadowSoftness = 0.34f;

        public void ApplyTo(ref FishingV2PresentationSettings settings)
        {
            if (!Enabled)
            {
                return;
            }

            settings.WaterClearZoneStrength = ClearZoneStrength;
            settings.WaterClearZoneRadius = ClearZoneRadius;
            settings.WaterClearColorStrength = ClearColorStrength;
            settings.WaterDepthColorStrength = DepthColorStrength;
            settings.WaterBottomColorBleed = BottomColorBleed;
            settings.WaterOpticalDistortionStrength = OpticalDistortionStrength;
            settings.WaterOpticalDistortionScale = OpticalDistortionScale;
            settings.WaterOpticalDistortionSpeed = OpticalDistortionSpeed;
            settings.WaterOpticalCausticFloorBias = OpticalCausticFloorBias;
            settings.WaterFishOpticalStrength = FishOpticalStrength;
            settings.WaterShadowOpticalStrength = ShadowOpticalStrength;
            settings.WaterBobberOpticalStrength = BobberOpticalStrength;
            settings.WaterBottomVisibility = BottomVisibility;
            settings.WaterBottomGrainStrength = BottomGrainStrength;
            settings.WaterBottomVariationScale = BottomVariationScale;
            settings.WaterBottomTextureScale = BottomTextureScale;
            settings.WaterBottomTextureBlend = BottomTextureBlend;
            settings.WaterBottomTextureContrast = BottomTextureContrast;
            settings.WaterBottomSecondarySampleStrength = BottomSecondarySampleStrength;
            settings.WaterLargeCausticStrength = LargeCausticStrength;
            settings.WaterLargeCausticScale = LargeCausticScale;
            settings.WaterLargeCausticSpeed = LargeCausticSpeed;
            settings.WaterMidCausticStrength = MidCausticStrength;
            settings.WaterMidCausticScale = MidCausticScale;
            settings.WaterMidCausticSpeed = MidCausticSpeed;
            settings.WaterMicroSurfaceStrength = MicroSurfaceStrength;
            settings.WaterEdgeFogStrength = EdgeFogStrength;
            settings.WaterEdgeFogRadius = EdgeFogRadius;
            settings.EnvironmentSilhouetteStrength = EnvironmentSilhouetteStrength;
            settings.FishDepthTintStrength = FishDepthTintStrength;
            settings.FishDepthDesaturation = FishDepthDesaturation;
            settings.FishDepthBrightnessDrop = FishDepthBrightnessDrop;
            settings.FishDepthContrast = FishDepthContrast;
            settings.HeroContrastStrength = HeroContrastStrength;
            settings.ShadowDepthInfluence = ShadowDepthInfluence;
            settings.ShadowSoftness = ShadowSoftness;
        }
    }
}
