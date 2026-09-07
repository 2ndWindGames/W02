using System;
using UnityEngine;

namespace Fishing.V2
{
    public enum FishPathType
    {
        Lane,
        Loop,
        HoverDash
    }

    public enum FishState
    {
        Roam,
        Startle,
        Wary,
        Notice,
        Interested,
        Bite,
        Linger,
        Caught,

        // v25 states. The legacy values above remain serialized-compatible while the
        // gameplay integration is moved behind the water-branch merge gate.
        Strike,
        Hooked,
        Reject,
        Yield,
        Steal,
        Chase,
        AfterBite
    }

    public enum ApproachStyle
    {
        Direct,
        Dash,
        Drift,
        Wary,
        Spiral,
        Hesitate
    }

    public enum AfterBiteMode
    {
        Peel,
        Pass,
        School,
        Arc,
        Jet
    }

    [Serializable]
    public struct FloatRange
    {
        public float Min;
        public float Max;

        public FloatRange(float min, float max)
        {
            Min = min;
            Max = max;
        }

        public float Sample(System.Random random)
        {
            if (random == null)
            {
                return Min;
            }

            return Min + (float)random.NextDouble() * (Max - Min);
        }
    }

    [Serializable]
    public struct IntRange
    {
        public int Min;
        public int Max;

        public IntRange(int min, int max)
        {
            Min = min;
            Max = max;
        }

        public int Sample(System.Random random)
        {
            if (random == null)
            {
                return Min;
            }

            return random.Next(Min, Max + 1);
        }
    }

    [Serializable]
    public struct ApproachStep
    {
        public ApproachStyle Style;
        public bool HasUntil;
        public float UntilDistance;

        public ApproachStep(ApproachStyle style)
        {
            Style = style;
            HasUntil = false;
            UntilDistance = 0f;
        }

        public ApproachStep(ApproachStyle style, float untilDistance)
        {
            Style = style;
            HasUntil = true;
            UntilDistance = untilDistance;
        }
    }

    [Serializable]
    public sealed class FeedSignature
    {
        public float NoticeT = 0.30f;
        [Range(0f, 1f)] public float NoticeK = 0.35f;
        [Range(0f, 1f)] public float Carry = 0.60f;
        public float ApproachK = 1f;
        [Range(0f, 1f)] public float CarryApproach;
        public FloatRange Linger = new FloatRange(2.2f, 5f);
        public float LingerSpeed = 0.28f;
        public float LingerYaw = 1.20f;
        public float LingerFreq = 1.30f;
        public bool SchoolJoin;
        public FloatRange JoinDelay = new FloatRange(0.08f, 0.22f);
        public float Arrival = 0.35f;
        public FloatRange Commit = new FloatRange(0.62f, 0.86f);
        public FloatRange Reconsider = new FloatRange(0.65f, 1.25f);
        [Range(0f, 1f)] public float Doubt = 0.22f;
        [Range(0f, 1f)] public float QuitBelow = 0.24f;
        [Range(-1f, 1f)] public float Competition = 0.20f;
        public float StrikeBodyLengths = 0.70f;
        [Range(0f, 1f)] public float StrikeMinimum = 0.58f;
        public float StrikeSpeed = 1.90f;
        public float StrikeDuration = 0.55f;
        public FloatRange Reject = new FloatRange(0.65f, 1.15f);
        public float RejectSpeed = 0.75f;
        public FloatRange RejectCooldown = new FloatRange(2f, 3.6f);
    }

    [Serializable]
    public sealed class MotionMicroSignature
    {
        [Range(0f, 1f)] public float BurstProbability = 0.20f;
        [Range(0f, 1f)] public float CoastProbability = 0.20f;
        [Range(0f, 1f)] public float PauseProbability = 0.05f;
        public FloatRange BurstDuration = new FloatRange(0.18f, 0.32f);
        public FloatRange BurstSpeed = new FloatRange(1.15f, 1.30f);
        public FloatRange CoastDuration = new FloatRange(0.40f, 0.80f);
        public FloatRange CoastSpeed = new FloatRange(0.82f, 0.94f);
        public FloatRange PauseDuration = new FloatRange(0.12f, 0.28f);
        public float PauseSpeed = 0.50f;
        public FloatRange CruiseDuration = new FloatRange(0.80f, 1.80f);
    }

    [Serializable]
    public sealed class MotionSignature
    {
        public FloatRange Beat = new FloatRange(0.16f, 0.28f);
        public FloatRange Glide = new FloatRange(0.30f, 0.60f);
        public float Kick = 0.30f;
        public float Drive = 0.40f;
        public float GlideDrop = 0.20f;
        public float Cadence = 1f;
        public float TurnPrep = 1f;
        public MotionMicroSignature Micro = new MotionMicroSignature();
    }

    [Serializable]
    public sealed class ContestSignature
    {
        [Range(0f, 1f)] public float Trigger = 0.58f;
        public FloatRange Evaluation = new FloatRange(0.34f, 0.62f);
        public float Range = 1.25f;
        [Range(0f, 1f)] public float YieldWeight = 0.18f;
        [Range(0f, 1f)] public float StealWeight = 0.20f;
        [Range(0f, 1f)] public float ChaseWeight = 0.14f;
        public FloatRange YieldDuration = new FloatRange(0.42f, 0.70f);
        public FloatRange StealDuration = new FloatRange(0.34f, 0.58f);
        public FloatRange ChaseDuration = new FloatRange(0.38f, 0.66f);
        public float YieldSpeed = 0.68f;
        public float StealSpeed = 1.52f;
        public float ChaseSpeed = 1.34f;
        public float Flank = 0.42f;
        public float CommitSteal = 0.10f;
        public float CommitChase = 0.07f;
        public float CommitYield = -0.08f;
        public FloatRange Cooldown = new FloatRange(0.75f, 1.35f);
    }

    [Serializable]
    public sealed class AfterBiteProfile
    {
        public AfterBiteMode Mode = AfterBiteMode.Peel;
        public FloatRange Duration = new FloatRange(0.55f, 0.85f);
        public float SpeedK = 1.15f;
        [Range(0f, 1f)] public float Carry = 0.65f;
        [Range(0f, 1f)] public float Away = 0.65f;
        public float Arc = 0.18f;
        public float TurnBodyLengths = 1f;
        public FloatRange Cooldown = new FloatRange(1.4f, 2.4f);
        [Range(0f, 1f)] public float Depth = 0.28f;
    }

    [Serializable]
    public sealed class LanePathSettings
    {
        public float Speed = 1.2f;
        public float Amplitude = 0.6f;
        public float Period = 8f;
    }

    [Serializable]
    public sealed class LoopPathSettings
    {
        public float A = 4f;
        public float B = 2.5f;
        public float Period = 10f;
        public float DriftSpeed = 0.2f;
    }

    [Serializable]
    public sealed class HoverDashSettings
    {
        public FloatRange HoverTime = new FloatRange(1.6f, 3.4f);
        public float DashDuration = 0.58f;
        public FloatRange DashDistance = new FloatRange(1.8f, 2.9f);
        public float HomeRadius = 3f;
        public float DriftSpeed = 0.20f;
        public float MinTurnRadiusBodyLengths = 0.85f;
    }

    [Serializable]
    public sealed class SchoolSettings
    {
        public bool Enabled;
        public IntRange GroupSize = new IntRange(2, 3);
        public float FormationRadius = 0.95f;
        [Range(0f, 1f)] public float SoloChance = 1f;
        public float GapMultiplier = 1.25f;
    }

    [Serializable]
    public sealed class TailSpec
    {
        public float Length = 0.23f;
        public float Spread = 0.19f;
        public float Notch = 0.26f;
        public float Cant = 0.04f;
    }

    [Serializable]
    public sealed class FinSpec
    {
        public float U0 = 0.30f;
        public float U1 = 0.43f;
        public float Length = 0.09f;
        public float Sweep = 0.42f;
        public int Segments = 3;
    }

    [Serializable]
    public sealed class DorsalSpec
    {
        public float U0 = 0.30f;
        public float U1 = 0.56f;
        public float Height = 0.08f;
    }

    [Serializable]
    public sealed class EyeSpec
    {
        public float U = 0.16f;
        public float Spread = 0.56f;
        public float Radius = 0.026f;
        public float RingScale = 1.30f;
    }

    [Serializable]
    public sealed class ArmSpec
    {
        public int Count = 8;
        public float Length = 0.40f;
        public float Spread = 0.40f;
        public float Width = 0.026f;
        public int Segments = 7;
    }

    [Serializable]
    public sealed class FinletSpec
    {
        public int Count = 4;
        public float U0 = 0.66f;
        public float U1 = 0.88f;
        public float Length = 0.020f;
    }

    [Serializable]
    public sealed class StripeSpec
    {
        public int Count;
        public float U0;
        public float U1;
        public float Amount = 0.30f;
        public float Width = 0.60f;
    }

    [Serializable]
    public sealed class FishVisualSpec
    {
        public float Length = 1f;
        public float MaxWidth = 0.12f;
        public float MaxHeight = 0.10f;
        public Vector2[] WidthProfile;
        public Vector2[] HeightProfile;
        public TailSpec Tail = new TailSpec();
        public FinSpec Pectoral = new FinSpec();
        public DorsalSpec Dorsal = new DorsalSpec();
        public EyeSpec Eye = new EyeSpec();
        public ArmSpec Arms;
        public FinletSpec Finlets;
        public StripeSpec Stripes = new StripeSpec();
        public bool Spots;
        public Color BaseColor = Color.gray;
        public Color EdgeColor = Color.white;
        public Color FinColor = Color.gray;
        public Color FinletColor = Color.gray;
        public float WaveAmplitude = 1f;
        public float WaveFrequency = 1f;
        public float WaveLag = 2.4f;
        public float WaveExponent = 1.6f;
        public float WaveHinge;
        public float WaveSpread;
        public float FinRipple;
        public float FinWave;
        public float FinRate = 2.2f;
        public float ArmDrift;
        public float ArmSpring = 26f;
        public float ArmDamping = 5.4f;
        public float ArmGain = 1.5f;
        public float ArmForwardGain = 0.16f;
        public float ArmClamp = 1.05f;
        public bool MantleJet;
        public bool HasMouthOffsetOverride;
        public float MouthOffset;
        public float ArcBody = 1f;
        public float Bank = 0.42f;
    }

    [Serializable]
    public sealed class FishSpeciesConfig
    {
        public int DataVersion;
        public string SpeciesId = "fish";
        public string DisplayName = "물고기";
        public FishPathType PathType = FishPathType.Lane;
        public int BaseScore = 1;
        public Vector2 Zone = new Vector2(0.05f, 0.95f);
        [Header("Presentation")]
        [Tooltip("Optional HUD portrait. When empty, the runtime builds a small portrait from the species visual data.")]
        public Texture2D HudIcon;
        [Range(0.45f, 1f)]
        [Tooltip("Relative portrait size inside the fixed HUD slot. Tuna uses 1.0 as the visual ceiling.")]
        public float HudIconScale = 1f;
        // Normalized distance below the surface. A negative range means that legacy data
        // should fall back to a stable zone-derived depth in FishAgentV2.
        public FloatRange VisualDepth = new FloatRange(-1f, -1f);
        public IntRange SpawnCount = new IntRange(1, 1);
        public FloatRange SizeCm = new FloatRange(10f, 20f);

        [Header("Gameplay")]
        public float StartleRadius = 1.5f;
        public float StartleDuration = 0.4f;
        [Range(0f, 1f)] public float CuriosityPerSecond = 0.05f;
        [Range(0f, 1f)] public float LureChance = 0.45f;
        public float MinNoticeRadius;
        public float NoticeRadius = 3.2f;
        public float ApproachSpeed = 2.5f;
        public float BiteWindow = 1.2f;
        public float TurnRateDeg = 200f;
        public float RespawnDelay;

        [Header("v25 Signatures")]
        public FeedSignature Feed = new FeedSignature();
        public MotionSignature Motion = new MotionSignature();
        public ContestSignature Contest = new ContestSignature();
        public AfterBiteProfile AfterBite = new AfterBiteProfile();
        public bool ValidationOnly;

        [Header("Movement")]
        public LanePathSettings Lane = new LanePathSettings();
        public LoopPathSettings Loop = new LoopPathSettings();
        public HoverDashSettings HoverDash = new HoverDashSettings();
        public ApproachStep[] ApproachPlan = new[] { new ApproachStep(ApproachStyle.Direct) };
        public SchoolSettings School;

        [Header("Visual")]
        public FishVisualSpec Visual = new FishVisualSpec();

        public bool IsSchool => School != null && School.Enabled;
    }

}
