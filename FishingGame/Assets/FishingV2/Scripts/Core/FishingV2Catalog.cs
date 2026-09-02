using System.Collections.Generic;
using UnityEngine;

namespace Fishing.V2
{
    public static class FishingV2Catalog
    {
        public static List<FishSpeciesConfig> CreateDefaults()
        {
            return new List<FishSpeciesConfig>
            {
                CreateAnchovy(),
                CreateSalmon(),
                CreateMahi(),
                CreateSquid(),
                CreateTuna()
            };
        }

        private static Vector2[] Profile(params float[] values)
        {
            Vector2[] result = new Vector2[values.Length / 2];
            for (int i = 0; i < result.Length; i++)
            {
                result[i] = new Vector2(values[i * 2], values[i * 2 + 1]);
            }

            return result;
        }

        private static FishVisualSpec Visual(
            float length,
            float width,
            float height,
            Vector2[] widthProfile,
            Vector2[] heightProfile,
            Color baseColor,
            Color edgeColor,
            Color finColor,
            float waveAmplitude,
            float waveFrequency)
        {
            return new FishVisualSpec
            {
                Length = length,
                MaxWidth = width,
                MaxHeight = height,
                WidthProfile = widthProfile,
                HeightProfile = heightProfile,
                BaseColor = baseColor,
                EdgeColor = edgeColor,
                FinColor = finColor,
                FinletColor = finColor,
                WaveAmplitude = waveAmplitude,
                WaveFrequency = waveFrequency,
                Tail = new TailSpec(),
                Pectoral = new FinSpec(),
                Dorsal = new DorsalSpec(),
                Eye = new EyeSpec(),
                Stripes = new StripeSpec()
            };
        }

        private static FishSpeciesConfig CreateAnchovy()
        {
            FishVisualSpec visual = Visual(
                0.42f, 0.048f, 0.056f,
                Profile(0f, 0f, .05f, .28f, .14f, .60f, .30f, .93f, .44f, 1f, .60f, .88f, .76f, .60f, .88f, .30f, 1f, .09f),
                Profile(0f, 0f, .05f, .34f, .14f, .66f, .30f, .95f, .44f, 1f, .60f, .92f, .76f, .66f, .88f, .36f, 1f, .11f),
                new Color(0.46f, 0.74f, 0.82f), new Color(0.99f, 0.99f, 0.98f), new Color(0.72f, 0.88f, 0.92f),
                0.70f, 2.30f);
            visual.Tail = new TailSpec { Length = 0.22f, Spread = 0.16f, Notch = 0.62f, Cant = 0.03f };
            visual.Pectoral = new FinSpec { U0 = .30f, U1 = .42f, Length = .062f, Sweep = .44f, Segments = 3 };
            visual.Dorsal = new DorsalSpec { U0 = .30f, U1 = .52f, Height = .09f };
            visual.Eye = new EyeSpec { U = .15f, Spread = .56f, Radius = .024f };
            visual.Stripes = new StripeSpec { Count = 1, U0 = .30f, U1 = .56f, Amount = .30f, Width = .60f };

            return new FishSpeciesConfig
            {
                SpeciesId = "anchovy",
                DisplayName = "멸치",
                PathType = FishPathType.Lane,
                BaseScore = 2,
                Zone = new Vector2(.05f, .45f),
                VisualDepth = new FloatRange(.08f, .30f),
                SpawnCount = new IntRange(1, 1),
                SizeCm = new FloatRange(6f, 10f),
                StartleRadius = 2.0f,
                StartleDuration = .60f,
                CuriosityPerSecond = .039f,
                LureChance = .38f,
                NoticeRadius = 3.6f,
                ApproachSpeed = 3.0f,
                BiteWindow = 1.0f,
                TurnRateDeg = 280f,
                Lane = new LanePathSettings { Speed = 1.90f, Amplitude = .38f, Period = 5.8f },
                ApproachPlan = new[] { new ApproachStep(ApproachStyle.Direct) },
                School = new SchoolSettings
                {
                    Enabled = true,
                    GroupSize = new IntRange(7, 11),
                    FormationRadius = 1.10f,
                    SoloChance = .06f,
                    GapMultiplier = 1.35f
                },
                Visual = visual
            };
        }

        private static FishSpeciesConfig CreateSalmon()
        {
            FishVisualSpec visual = Visual(
                1.15f, 0.162f, 0.145f,
                Profile(0f, 0f, .04f, .30f, .12f, .62f, .26f, .88f, .40f, 1f, .56f, .96f, .72f, .76f, .86f, .42f, 1f, .13f),
                Profile(0f, 0f, .04f, .36f, .12f, .68f, .26f, .92f, .40f, 1f, .56f, .97f, .72f, .80f, .86f, .46f, 1f, .15f),
                new Color(0.74f, 0.30f, 0.20f), new Color(0.99f, 0.78f, 0.60f), new Color(0.66f, 0.32f, 0.26f),
                1.0f, 1.0f);
            visual.Tail = new TailSpec { Length = .23f, Spread = .19f, Notch = .26f, Cant = .05f };
            visual.Pectoral = new FinSpec { U0 = .29f, U1 = .43f, Length = .092f, Sweep = .42f, Segments = 3 };
            visual.Dorsal = new DorsalSpec { U0 = .30f, U1 = .56f, Height = .11f };
            visual.Eye = new EyeSpec { U = .165f, Spread = .57f, Radius = .027f };
            visual.Spots = true;

            return new FishSpeciesConfig
            {
                SpeciesId = "salmon",
                DisplayName = "연어",
                PathType = FishPathType.Lane,
                BaseScore = 1,
                Zone = new Vector2(.05f, .95f),
                VisualDepth = new FloatRange(.20f, .58f),
                SpawnCount = new IntRange(4, 6),
                SizeCm = new FloatRange(12f, 20f),
                StartleRadius = 1.5f,
                StartleDuration = .40f,
                CuriosityPerSecond = .050f,
                LureChance = .45f,
                NoticeRadius = 3.2f,
                ApproachSpeed = 2.5f,
                BiteWindow = 1.2f,
                TurnRateDeg = 200f,
                Lane = new LanePathSettings { Speed = 1.15f, Amplitude = .58f, Period = 8.0f },
                ApproachPlan = new[] { new ApproachStep(ApproachStyle.Dash) },
                School = new SchoolSettings
                {
                    Enabled = true,
                    GroupSize = new IntRange(2, 3),
                    FormationRadius = .95f,
                    SoloChance = .68f,
                    GapMultiplier = 1.25f
                },
                Visual = visual
            };
        }

        private static FishSpeciesConfig CreateMahi()
        {
            FishVisualSpec visual = Visual(
                1.55f, 0.150f, 0.218f,
                Profile(0f, 0f, .03f, .44f, .10f, .76f, .20f, .96f, .34f, 1f, .50f, .86f, .66f, .60f, .82f, .32f, 1f, .10f),
                Profile(0f, 0f, .03f, .62f, .10f, .92f, .20f, 1f, .34f, .99f, .50f, .86f, .66f, .62f, .82f, .34f, 1f, .11f),
                new Color(0.14f, 0.56f, 0.36f), new Color(0.99f, 0.88f, 0.20f), new Color(0.16f, 0.52f, 0.66f),
                1.45f, 1.05f);
            visual.Tail = new TailSpec { Length = .26f, Spread = .20f, Notch = .58f, Cant = .03f };
            visual.Pectoral = new FinSpec { U0 = .24f, U1 = .36f, Length = .080f, Sweep = .40f, Segments = 3 };
            visual.Dorsal = new DorsalSpec { U0 = .07f, U1 = .84f, Height = .13f };
            visual.Eye = new EyeSpec { U = .12f, Spread = .54f, Radius = .026f };
            visual.Spots = true;

            return new FishSpeciesConfig
            {
                SpeciesId = "mahi",
                DisplayName = "만새기",
                PathType = FishPathType.Loop,
                BaseScore = 3,
                Zone = new Vector2(.25f, .75f),
                VisualDepth = new FloatRange(.36f, .68f),
                SpawnCount = new IntRange(1, 2),
                SizeCm = new FloatRange(25f, 40f),
                StartleRadius = 1.7f,
                StartleDuration = .55f,
                CuriosityPerSecond = .045f,
                LureChance = .34f,
                NoticeRadius = 3.0f,
                ApproachSpeed = 2.2f,
                BiteWindow = 1.0f,
                TurnRateDeg = 190f,
                Loop = new LoopPathSettings { A = 3.4f, B = 2.1f, Period = 6.5f, DriftSpeed = .42f },
                ApproachPlan = new[]
                {
                    new ApproachStep(ApproachStyle.Dash, 2.2f),
                    new ApproachStep(ApproachStyle.Hesitate)
                },
                Visual = visual
            };
        }

        private static FishSpeciesConfig CreateSquid()
        {
            FishVisualSpec visual = Visual(
                1.05f, 0.130f, 0.108f,
                Profile(0f, 0f, .07f, .28f, .20f, .58f, .38f, .86f, .56f, 1f, .72f, .99f, .88f, .93f, 1f, .82f),
                Profile(0f, 0f, .07f, .30f, .20f, .58f, .38f, .84f, .56f, 1f, .72f, .97f, .88f, .89f, 1f, .76f),
                new Color(0.80f, 0.66f, 0.62f), new Color(0.99f, 0.96f, 0.92f), new Color(0.74f, 0.58f, 0.56f),
                0.55f, 0.90f);
            visual.Tail = new TailSpec { Length = .16f, Spread = .16f, Notch = .15f, Cant = .05f };
            visual.Pectoral = new FinSpec { U0 = .10f, U1 = .56f, Length = .150f, Sweep = .05f, Segments = 12 };
            visual.Dorsal = new DorsalSpec { U0 = .32f, U1 = .72f, Height = .018f };
            visual.Eye = new EyeSpec { U = .84f, Spread = .70f, Radius = .038f, RingScale = 1.24f };
            visual.Arms = new ArmSpec { Count = 8, Length = .40f, Spread = .40f, Width = .026f, Segments = 7 };
            visual.WaveLag = 3.4f;
            visual.WaveExponent = 1.0f;
            visual.WaveHinge = .95f;
            visual.WaveSpread = 5.4f;
            visual.FinRipple = .020f;
            visual.FinWave = 11.0f;
            visual.FinRate = 4.2f;
            visual.ArmDrift = .105f;
            visual.ArmSpring = 26f;
            visual.ArmDamping = 5.4f;
            visual.ArmGain = 1.5f;
            visual.ArcBody = .22f;
            visual.Bank = .10f;
            visual.Spots = true;

            return new FishSpeciesConfig
            {
                SpeciesId = "squid",
                DisplayName = "오징어",
                PathType = FishPathType.HoverDash,
                BaseScore = 5,
                Zone = new Vector2(.20f, .80f),
                VisualDepth = new FloatRange(.28f, .60f),
                SpawnCount = new IntRange(1, 2),
                SizeCm = new FloatRange(15f, 25f),
                StartleRadius = 1.2f,
                StartleDuration = .80f,
                CuriosityPerSecond = .063f,
                LureChance = .30f,
                NoticeRadius = 2.6f,
                ApproachSpeed = 1.5f,
                BiteWindow = 1.5f,
                TurnRateDeg = 95f,
                RespawnDelay = 15f,
                HoverDash = new HoverDashSettings
                {
                    HoverTime = new FloatRange(1.6f, 3.4f),
                    DashDuration = .58f,
                    DashDistance = new FloatRange(1.8f, 2.9f),
                    HomeRadius = 3.0f,
                    DriftSpeed = .20f,
                    MinTurnRadiusBodyLengths = .85f
                },
                ApproachPlan = new[]
                {
                    new ApproachStep(ApproachStyle.Wary, 1.7f),
                    new ApproachStep(ApproachStyle.Hesitate)
                },
                Visual = visual
            };
        }

        private static FishSpeciesConfig CreateTuna()
        {
            FishVisualSpec visual = Visual(
                2.55f, 0.352f, 0.296f,
                Profile(0f, 0f, .03f, .30f, .09f, .62f, .20f, .90f, .34f, 1f, .48f, .99f, .64f, .82f, .80f, .42f, .90f, .20f, 1f, .07f),
                Profile(0f, 0f, .03f, .36f, .09f, .68f, .20f, .93f, .34f, 1f, .48f, 1f, .64f, .86f, .80f, .46f, .90f, .22f, 1f, .08f),
                new Color(0.08f, 0.17f, 0.42f), new Color(0.78f, 0.83f, 0.89f), new Color(0.14f, 0.22f, 0.40f),
                1.75f, 0.98f);
            visual.Tail = new TailSpec { Length = .26f, Spread = .21f, Notch = .66f, Cant = .03f };
            visual.Pectoral = new FinSpec { U0 = .26f, U1 = .40f, Length = .112f, Sweep = .42f, Segments = 3 };
            visual.Dorsal = new DorsalSpec { U0 = .28f, U1 = .50f, Height = .13f };
            visual.Eye = new EyeSpec { U = .14f, Spread = .55f, Radius = .024f };
            visual.Finlets = new FinletSpec { Count = 4, U0 = .66f, U1 = .88f, Length = .020f };
            visual.FinletColor = new Color(0.85f, 0.68f, 0.20f);

            return new FishSpeciesConfig
            {
                SpeciesId = "tuna",
                DisplayName = "참치",
                PathType = FishPathType.Loop,
                BaseScore = 30,
                Zone = new Vector2(.50f, .95f),
                VisualDepth = new FloatRange(.58f, .86f),
                SpawnCount = new IntRange(1, 1),
                SizeCm = new FloatRange(35f, 60f),
                StartleRadius = 3.0f,
                StartleDuration = 1.2f,
                CuriosityPerSecond = .063f,
                LureChance = 0f,
                MinNoticeRadius = 3.2f,
                NoticeRadius = 3.2f,
                ApproachSpeed = .50f,
                BiteWindow = .60f,
                TurnRateDeg = 85f,
                RespawnDelay = 25f,
                Loop = new LoopPathSettings { A = 4.6f, B = 2.9f, Period = 7.0f, DriftSpeed = .55f },
                ApproachPlan = new[]
                {
                    new ApproachStep(ApproachStyle.Drift, 4.5f),
                    new ApproachStep(ApproachStyle.Spiral, 1.5f),
                    new ApproachStep(ApproachStyle.Hesitate)
                },
                Visual = visual
            };
        }
    }
}
