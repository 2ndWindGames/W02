using System.Collections.Generic;
using UnityEngine;

namespace Fishing.V2
{
    public static class FishingV2Catalog
    {
        public const int CurrentDataVersion = 25;

        public static List<FishSpeciesConfig> CreateDefaults()
        {
            return CreatePlayableDefaults();
        }

        public static List<FishSpeciesConfig> CreatePlayableDefaults()
        {
            List<FishSpeciesConfig> result = new List<FishSpeciesConfig>
            {
                CreateAnchovy(),
                CreateSalmon(),
                CreateMahi(),
                CreateSquid(),
                CreateTuna()
            };

            ApplyV25Data(result);
            return result;
        }

        /// <summary>
        /// v25 HTML의 실제 5종과 타입 조합 검증용 3종을 함께 반환한다.
        /// 기본 플레이 Spot은 CreatePlayableDefaults()를 사용해 밸런스가 섞이지 않게 한다.
        /// </summary>
        public static List<FishSpeciesConfig> CreateValidationDefaults()
        {
            List<FishSpeciesConfig> result = CreatePlayableDefaults();
            result.Add(CreateNeedle());
            result.Add(CreateMinnow());
            result.Add(CreateDisc());
            ApplyV25Data(result);
            return result;
        }

        public static List<FishSpeciesConfig> CreateAllDefaults()
        {
            return CreateValidationDefaults();
        }

        /// <summary>
        /// Existing serialized FishSpeciesAsset files may predate the v25 fields. Normalize one
        /// asset in place so opening the old Beach spot does not silently fall back to generic
        /// signature defaults. The caller owns persistence of the asset.
        /// </summary>
        public static void ApplyV25Defaults(FishSpeciesConfig species)
        {
            if (species == null)
            {
                return;
            }

            ApplyV25Data(new List<FishSpeciesConfig> { species });
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

        private static FeedSignature Feed(
            float noticeT,
            float noticeK,
            float carry,
            float approachK,
            float carryApproach,
            float lingerMin,
            float lingerMax,
            float lingerSpeed,
            float lingerYaw,
            float lingerFreq,
            bool schoolJoin,
            float joinMin,
            float joinMax,
            float arrival,
            float commitMin,
            float commitMax,
            float reconsiderMin,
            float reconsiderMax,
            float doubt,
            float quitBelow,
            float competition,
            float strikeBodyLengths,
            float strikeMinimum,
            float strikeSpeed,
            float strikeDuration)
        {
            return new FeedSignature
            {
                NoticeT = noticeT,
                NoticeK = noticeK,
                Carry = carry,
                ApproachK = approachK,
                CarryApproach = carryApproach,
                Linger = new FloatRange(lingerMin, lingerMax),
                LingerSpeed = lingerSpeed,
                LingerYaw = lingerYaw,
                LingerFreq = lingerFreq,
                SchoolJoin = schoolJoin,
                JoinDelay = new FloatRange(joinMin, joinMax),
                Arrival = arrival,
                Commit = new FloatRange(commitMin, commitMax),
                Reconsider = new FloatRange(reconsiderMin, reconsiderMax),
                Doubt = doubt,
                QuitBelow = quitBelow,
                Competition = competition,
                StrikeBodyLengths = strikeBodyLengths,
                StrikeMinimum = strikeMinimum,
                StrikeSpeed = strikeSpeed,
                StrikeDuration = strikeDuration
            };
        }

        private static MotionMicroSignature Micro(
            float burstProbability,
            float coastProbability,
            float pauseProbability,
            float burstDurationMin,
            float burstDurationMax,
            float burstSpeedMin,
            float burstSpeedMax,
            float coastDurationMin,
            float coastDurationMax,
            float coastSpeedMin,
            float coastSpeedMax,
            float pauseDurationMin,
            float pauseDurationMax,
            float pauseSpeed,
            float cruiseDurationMin,
            float cruiseDurationMax)
        {
            return new MotionMicroSignature
            {
                BurstProbability = burstProbability,
                CoastProbability = coastProbability,
                PauseProbability = pauseProbability,
                BurstDuration = new FloatRange(burstDurationMin, burstDurationMax),
                BurstSpeed = new FloatRange(burstSpeedMin, burstSpeedMax),
                CoastDuration = new FloatRange(coastDurationMin, coastDurationMax),
                CoastSpeed = new FloatRange(coastSpeedMin, coastSpeedMax),
                PauseDuration = new FloatRange(pauseDurationMin, pauseDurationMax),
                PauseSpeed = pauseSpeed,
                CruiseDuration = new FloatRange(cruiseDurationMin, cruiseDurationMax)
            };
        }

        private static MotionSignature Motion(
            float beatMin,
            float beatMax,
            float glideMin,
            float glideMax,
            float kick,
            float drive,
            float glideDrop,
            float cadence,
            float turnPrep,
            MotionMicroSignature micro)
        {
            return new MotionSignature
            {
                Beat = new FloatRange(beatMin, beatMax),
                Glide = new FloatRange(glideMin, glideMax),
                Kick = kick,
                Drive = drive,
                GlideDrop = glideDrop,
                Cadence = cadence,
                TurnPrep = turnPrep,
                Micro = micro
            };
        }

        private static ContestSignature Contest(
            float trigger,
            float yieldWeight,
            float stealWeight,
            float chaseWeight,
            float yieldSpeed,
            float stealSpeed,
            float chaseSpeed,
            float flank)
        {
            ContestSignature result = new ContestSignature
            {
                Trigger = trigger,
                YieldWeight = yieldWeight,
                StealWeight = stealWeight,
                ChaseWeight = chaseWeight,
                YieldSpeed = yieldSpeed,
                StealSpeed = stealSpeed,
                ChaseSpeed = chaseSpeed,
                Flank = flank
            };
            return result;
        }

        private static AfterBiteProfile After(
            AfterBiteMode mode,
            float durationMin,
            float durationMax,
            float speedK,
            float carry,
            float away,
            float arc,
            float turnBodyLengths,
            float cooldownMin,
            float cooldownMax,
            float depth)
        {
            return new AfterBiteProfile
            {
                Mode = mode,
                Duration = new FloatRange(durationMin, durationMax),
                SpeedK = speedK,
                Carry = carry,
                Away = away,
                Arc = arc,
                TurnBodyLengths = turnBodyLengths,
                Cooldown = new FloatRange(cooldownMin, cooldownMax),
                Depth = depth
            };
        }

        private static void ApplyV25Data(List<FishSpeciesConfig> species)
        {
            for (int i = 0; i < species.Count; i++)
            {
                FishSpeciesConfig fish = species[i];
                if (fish == null || fish.Visual == null)
                {
                    continue;
                }

                fish.Visual.MantleJet = false;
                fish.Visual.HasMouthOffsetOverride = false;
                fish.Visual.MouthOffset = 0f;
                fish.DataVersion = CurrentDataVersion;

                switch (fish.SpeciesId)
                {
                    case "anchovy":
                        fish.BaseScore = 1;
                        fish.Visual.Dorsal.Height = .052f;
                        fish.Visual.FinRipple = .007f;
                        fish.Visual.FinRate = 2.6f;
                        fish.Visual.Bank = .50f;
                        fish.Feed = Feed(.12f, .56f, .78f, 1.16f, .12f, .70f, 1.30f, .24f, 1.45f, 1.80f, true, .03f, .15f, .30f, .78f, .98f, .55f, .95f, .10f, .16f, .65f, 1.00f, .60f, 2.15f, .46f);
                        fish.Motion = Motion(.11f, .18f, .18f, .34f, .42f, .50f, .30f, 1.10f, 1.18f,
                            Micro(.44f, .22f, .08f, .15f, .28f, 1.25f, 1.38f, .35f, .70f, .80f, .91f, .10f, .22f, .38f, .65f, 1.35f));
                        fish.Contest = Contest(.52f, .06f, .34f, .18f, .68f, 1.52f, 1.34f, .34f);
                        fish.AfterBite = After(AfterBiteMode.School, .34f, .55f, 1.55f, .72f, .92f, .30f, .72f, .70f, 1.20f, .20f);
                        break;

                    case "salmon":
                        fish.BaseScore = 3;
                        fish.Visual.Dorsal.Height = .066f;
                        fish.Visual.FinRipple = .011f;
                        fish.Visual.FinRate = 1.9f;
                        fish.Visual.Bank = .42f;
                        fish.Feed = Feed(.44f, .28f, .50f, .90f, .08f, 1.70f, 3.00f, .20f, .70f, .95f, false, .08f, .22f, .36f, .48f, .78f, .80f, 1.40f, .34f, .30f, -.12f, .62f, .66f, 1.55f, .62f);
                        fish.Motion = Motion(.24f, .38f, .45f, .82f, .32f, .40f, .20f, .94f, .90f,
                            Micro(.25f, .20f, .07f, .24f, .42f, 1.17f, 1.28f, .65f, 1.20f, .84f, .94f, .18f, .36f, .58f, 1.50f, 3.10f));
                        fish.Contest = Contest(.62f, .34f, .08f, .04f, .58f, 1.52f, 1.34f, .42f);
                        fish.AfterBite = After(AfterBiteMode.Peel, .72f, 1.08f, 1.02f, .62f, .78f, .22f, 1.05f, 1.50f, 2.40f, .34f);
                        break;

                    case "mahi":
                        fish.BaseScore = 8;
                        fish.Visual.Dorsal.Height = .086f;
                        fish.Visual.FinRipple = .012f;
                        fish.Visual.FinRate = 2.0f;
                        fish.Visual.Bank = .52f;
                        fish.Feed = Feed(.18f, .52f, .78f, 1.12f, .14f, .90f, 1.70f, .34f, 1.15f, 1.35f, false, .08f, .22f, .40f, .70f, .96f, .55f, 1.00f, .12f, .18f, .80f, .60f, .55f, 2.35f, .52f);
                        fish.Motion = Motion(.17f, .27f, .30f, .52f, .46f, .54f, .20f, 1.02f, 1.22f,
                            Micro(.40f, .19f, .025f, .18f, .32f, 1.22f, 1.34f, .42f, .82f, .84f, .94f, .14f, .28f, .62f, 1.00f, 2.10f));
                        fish.Contest = Contest(.44f, .03f, .54f, .38f, .68f, 1.72f, 1.52f, .42f);
                        fish.AfterBite = After(AfterBiteMode.Arc, .62f, .92f, 1.48f, .84f, .32f, .52f, .82f, 1.05f, 1.75f, .26f);
                        break;

                    case "squid":
                        fish.Visual.Dorsal.Height = .018f;
                        fish.Visual.FinRipple = .020f;
                        fish.Visual.FinWave = 11.0f;
                        fish.Visual.FinRate = 4.2f;
                        fish.Visual.MantleJet = true;
                        fish.Visual.ArmDrift = .080f;
                        fish.Visual.ArmSpring = 18f;
                        fish.Visual.ArmDamping = 7.2f;
                        fish.Visual.ArmGain = .95f;
                        fish.Visual.ArmForwardGain = .12f;
                        fish.Visual.ArmClamp = .48f;
                        fish.Visual.HasMouthOffsetOverride = true;
                        fish.Visual.MouthOffset = -.55f;
                        fish.Feed = Feed(.55f, .18f, .28f, .82f, .02f, 2.40f, 4.20f, .12f, .48f, .72f, false, .08f, .22f, .30f, .38f, .70f, .90f, 1.50f, .42f, .34f, -.25f, .65f, .68f, 2.10f, .62f);
                        fish.Motion = Motion(.16f, .28f, .30f, .60f, .30f, .40f, .20f, .90f, .48f, null);
                        fish.Contest = Contest(.56f, .48f, .05f, .03f, .55f, 1.52f, 1.34f, .42f);
                        fish.AfterBite = After(AfterBiteMode.Jet, .46f, .72f, 1.75f, .48f, .92f, .20f, .92f, 1.30f, 2.10f, .42f);
                        break;

                    case "tuna":
                        fish.BaseScore = 30;
                        fish.Visual.Dorsal.Height = .078f;
                        fish.Visual.FinRipple = .013f;
                        fish.Visual.FinRate = 1.5f;
                        fish.Visual.Bank = .40f;
                        fish.Feed = Feed(.22f, .70f, .92f, 1.18f, .62f, .75f, 1.35f, .62f, .30f, .70f, false, .08f, .22f, .52f, .65f, .90f, .65f, 1.10f, .08f, .16f, .72f, .50f, .52f, 2.20f, .86f);
                        fish.Motion = Motion(.14f, .22f, .12f, .24f, .18f, .24f, .08f, 1.08f, .58f,
                            Micro(.10f, .10f, 0f, .22f, .36f, 1.08f, 1.16f, .30f, .58f, .94f, .99f, .10f, .18f, .85f, 2.20f, 4.60f));
                        fish.Contest = Contest(.42f, .02f, .42f, .46f, .68f, 1.62f, 1.58f, .42f);
                        fish.AfterBite = After(AfterBiteMode.Pass, .82f, 1.20f, 1.34f, .96f, .05f, .04f, 1.75f, 1.15f, 1.85f, .38f);
                        break;

                    case "needle":
                        fish.ValidationOnly = true;
                        fish.Visual.Dorsal.Height = .038f;
                        fish.Visual.FinRipple = .008f;
                        fish.Visual.FinRate = 2.6f;
                        fish.Visual.Bank = .50f;
                        fish.Feed = Feed(.10f, .62f, .82f, 1.20f, .18f, .55f, 1.05f, .32f, 1.05f, 1.65f, true, .05f, .19f, .30f, .82f, .98f, .48f, .86f, .08f, .14f, .55f, .72f, .55f, 2.20f, .44f);
                        fish.Motion = Motion(.12f, .20f, .45f, .80f, .30f, .35f, .15f, 1.12f, .72f,
                            Micro(.18f, .19f, .015f, .16f, .28f, 1.17f, 1.28f, .62f, 1.15f, .90f, .97f, .10f, .20f, .72f, 1.35f, 2.80f));
                        fish.Contest = Contest(.48f, .04f, .48f, .24f, .68f, 1.68f, 1.34f, .34f);
                        fish.AfterBite = After(AfterBiteMode.Pass, .42f, .68f, 1.38f, .86f, .12f, .06f, 1.45f, .90f, 1.45f, .24f);
                        break;

                    case "minnow":
                        fish.ValidationOnly = true;
                        fish.Visual.FinRipple = .009f;
                        fish.Visual.FinRate = 2.5f;
                        fish.Visual.Bank = .50f;
                        fish.Feed = Feed(.08f, .64f, .72f, 1.28f, .12f, .45f, .95f, .25f, 1.65f, 2.00f, true, .025f, .12f, .27f, .86f, .99f, .42f, .78f, .06f, .12f, .75f, 1.15f, .52f, 2.50f, .40f);
                        fish.Motion = Motion(.16f, .28f, .30f, .60f, .30f, .40f, .15f, 1.16f, 1.30f, null);
                        fish.Contest = Contest(.47f, .05f, .40f, .24f, .68f, 1.52f, 1.34f, .30f);
                        fish.AfterBite = After(AfterBiteMode.School, .28f, .48f, 1.62f, .68f, .96f, .34f, .68f, .55f, 1.00f, .18f);
                        break;

                    case "disc":
                        fish.ValidationOnly = true;
                        fish.Visual.FinRipple = .012f;
                        fish.Visual.FinRate = 2.0f;
                        fish.Visual.Bank = .44f;
                        fish.Feed = Feed(.50f, .24f, .36f, .80f, .04f, 2.00f, 3.80f, .14f, .88f, .78f, false, .08f, .22f, .34f, .34f, .66f, .85f, 1.45f, .45f, .38f, -.28f, .70f, .70f, 1.45f, .70f);
                        fish.Motion = Motion(.34f, .54f, .58f, .96f, .22f, .30f, .24f, .82f, 1.02f,
                            Micro(.18f, .18f, .16f, .28f, .48f, 1.10f, 1.20f, .72f, 1.35f, .78f, .90f, .26f, .58f, .32f, 1.25f, 2.60f));
                        fish.Contest = Contest(.55f, .56f, .03f, .02f, .50f, 1.52f, 1.34f, .42f);
                        fish.AfterBite = After(AfterBiteMode.Peel, .82f, 1.22f, .88f, .52f, .76f, .28f, 1.18f, 1.70f, 2.80f, .32f);
                        break;
                }
            }
        }

        private static FishSpeciesConfig CreateNeedle()
        {
            FishVisualSpec visual = Visual(
                .88f, .040f, .050f,
                Profile(0f, 0f, .10f, .30f, .26f, .62f, .44f, .92f, .60f, 1f, .76f, .84f, .90f, .48f, 1f, .13f),
                Profile(0f, 0f, .10f, .36f, .26f, .68f, .44f, .94f, .60f, 1f, .76f, .88f, .90f, .54f, 1f, .16f),
                new Color(.42f, .50f, .58f), new Color(.84f, .88f, .92f), new Color(.50f, .58f, .66f), .45f, 2.2f);
            visual.Tail = new TailSpec { Length = .20f, Spread = .13f, Notch = .45f, Cant = .03f };
            visual.Pectoral = new FinSpec { U0 = .38f, U1 = .51f, Length = .058f, Sweep = .48f, Segments = 3 };
            visual.Dorsal = new DorsalSpec { U0 = .52f, U1 = .72f, Height = .038f };
            visual.Eye = new EyeSpec { U = .24f, Spread = .55f, Radius = .020f };
            return new FishSpeciesConfig
            {
                SpeciesId = "needle",
                DisplayName = "은빛 학공치",
                PathType = FishPathType.Lane,
                BaseScore = 1,
                ValidationOnly = true,
                Zone = new Vector2(.05f, .35f),
                VisualDepth = new FloatRange(.10f, .38f),
                SpawnCount = new IntRange(2, 3),
                SizeCm = new FloatRange(8f, 14f),
                StartleRadius = 1.5f,
                StartleDuration = .45f,
                CuriosityPerSecond = .055f,
                LureChance = .45f,
                NoticeRadius = 3.0f,
                ApproachSpeed = 3.2f,
                BiteWindow = 1.0f,
                TurnRateDeg = 240f,
                Lane = new LanePathSettings { Speed = 2.5f, Amplitude = .16f, Period = 7.0f },
                ApproachPlan = new[] { new ApproachStep(ApproachStyle.Dash) },
                School = new SchoolSettings { Enabled = true, GroupSize = new IntRange(2, 4), FormationRadius = .75f, SoloChance = .55f, GapMultiplier = 1.25f },
                Visual = visual
            };
        }

        private static FishSpeciesConfig CreateMinnow()
        {
            FishVisualSpec visual = Visual(
                .34f, .045f, .052f,
                Profile(0f, 0f, .06f, .30f, .18f, .64f, .34f, .94f, .48f, 1f, .64f, .90f, .80f, .58f, .92f, .30f, 1f, .12f),
                Profile(0f, 0f, .06f, .36f, .18f, .70f, .34f, .96f, .48f, 1f, .64f, .92f, .80f, .62f, .92f, .34f, 1f, .14f),
                new Color(.72f, .62f, .20f), new Color(.94f, .90f, .56f), new Color(.66f, .58f, .24f), .70f, 2.6f);
            visual.Tail = new TailSpec { Length = .28f, Spread = .20f, Notch = .40f, Cant = .04f };
            visual.Pectoral = new FinSpec { U0 = .31f, U1 = .43f, Length = .065f, Sweep = .46f, Segments = 3 };
            visual.Dorsal = new DorsalSpec { U0 = .40f, U1 = .62f, Height = .050f };
            visual.Eye = new EyeSpec { U = .20f, Spread = .58f, Radius = .030f };
            return new FishSpeciesConfig
            {
                SpeciesId = "minnow",
                DisplayName = "노란 피라미",
                PathType = FishPathType.HoverDash,
                BaseScore = 1,
                ValidationOnly = true,
                Zone = new Vector2(.30f, .80f),
                VisualDepth = new FloatRange(.18f, .52f),
                SpawnCount = new IntRange(1, 1),
                SizeCm = new FloatRange(5f, 9f),
                StartleRadius = 1.1f,
                StartleDuration = .40f,
                CuriosityPerSecond = .060f,
                LureChance = .50f,
                NoticeRadius = 2.4f,
                ApproachSpeed = 3.4f,
                BiteWindow = .80f,
                TurnRateDeg = 330f,
                HoverDash = new HoverDashSettings
                {
                    HoverTime = new FloatRange(1.1f, 2.4f),
                    DashDuration = .35f,
                    DashDistance = new FloatRange(.7f, 1.3f),
                    HomeRadius = 1.8f,
                    DriftSpeed = .16f,
                    MinTurnRadiusBodyLengths = .75f
                },
                ApproachPlan = new[] { new ApproachStep(ApproachStyle.Direct) },
                School = new SchoolSettings { Enabled = true, GroupSize = new IntRange(6, 9), FormationRadius = .85f, SoloChance = .20f, GapMultiplier = 1.25f },
                Visual = visual
            };
        }

        private static FishSpeciesConfig CreateDisc()
        {
            FishVisualSpec visual = Visual(
                .66f, .255f, .150f,
                Profile(0f, 0f, .05f, .55f, .14f, .85f, .28f, 1f, .50f, 1f, .68f, .88f, .82f, .58f, .92f, .30f, 1f, .12f),
                Profile(0f, 0f, .05f, .58f, .14f, .86f, .28f, 1f, .50f, 1f, .68f, .88f, .82f, .60f, .92f, .32f, 1f, .14f),
                new Color(.14f, .52f, .52f), new Color(.56f, .86f, .84f), new Color(.18f, .46f, .48f), .80f, 1.2f);
            visual.Tail = new TailSpec { Length = .20f, Spread = .19f, Notch = .32f, Cant = .05f };
            visual.Pectoral = new FinSpec { U0 = .26f, U1 = .41f, Length = .098f, Sweep = .38f, Segments = 3 };
            visual.Dorsal = new DorsalSpec { U0 = .42f, U1 = .68f, Height = .050f };
            visual.Eye = new EyeSpec { U = .155f, Spread = .58f, Radius = .028f };
            visual.Stripes = new StripeSpec { Count = 4, U0 = .14f, U1 = .78f, Amount = .30f, Width = .30f };
            return new FishSpeciesConfig
            {
                SpeciesId = "disc",
                DisplayName = "청록 접시고기",
                PathType = FishPathType.Loop,
                BaseScore = 1,
                ValidationOnly = true,
                Zone = new Vector2(.35f, .90f),
                VisualDepth = new FloatRange(.28f, .66f),
                SpawnCount = new IntRange(1, 2),
                SizeCm = new FloatRange(12f, 22f),
                StartleRadius = 1.4f,
                StartleDuration = .55f,
                CuriosityPerSecond = .045f,
                LureChance = .40f,
                NoticeRadius = 2.8f,
                ApproachSpeed = 2.0f,
                BiteWindow = 1.2f,
                TurnRateDeg = 160f,
                Loop = new LoopPathSettings { A = 1.7f, B = 1.1f, Period = 5.5f, DriftSpeed = 0f },
                ApproachPlan = new[] { new ApproachStep(ApproachStyle.Spiral, 1.0f), new ApproachStep(ApproachStyle.Direct) },
                Visual = visual
            };
        }
    }
}
