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
        Caught
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
        public float ArcBody = 1f;
        public float Bank = 0.42f;
    }

    [Serializable]
    public sealed class FishSpeciesConfig
    {
        public string SpeciesId = "fish";
        public string DisplayName = "물고기";
        public FishPathType PathType = FishPathType.Lane;
        public int BaseScore = 1;
        public Vector2 Zone = new Vector2(0.05f, 0.95f);
        [Header("Presentation")]
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
