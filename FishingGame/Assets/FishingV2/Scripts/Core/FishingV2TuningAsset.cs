using UnityEngine;

namespace Fishing.V2
{
    [CreateAssetMenu(menuName = "Fishing V2/Tuning", fileName = "FishingV2Tuning")]
    public sealed class FishingV2TuningAsset : ScriptableObject
    {
        [Header("Play space")]
        public Vector2 PondSize = new Vector2(16f, 9f);
        public float SessionSeconds = 90f;
        public float MaxDeltaTime = 0.05f;

        [Header("State machine")]
        public float NoticeDuration = 0.30f;
        public float CuriosityTick = 1f;
        public float ArriveRadius = 0.35f;
        public float RejectedCooldown = 3f;
        public float ApproachTimeout = 18f;
        public float OrbitBreakOff = 3.5f;
        public float LingerMin = 1.2f;
        public float LingerMax = 2.4f;

        [Header("Steering")]
        public float MinTurnRadiusBodyLengths = 0.55f;
        [Range(0.15f, 1f)] public float CloseTurnBoost = 0.40f;
        [Range(0.2f, 1f)] public float SpeedFitK = 0.85f;
        [Range(0f, 45f)] public float SlipMaxDeg = 14f;
        [Range(0f, 1f)] public float HeadTrack = 0.35f;
        public float HeadCourseSmoothing = 0.55f;

        [Header("Startle and recast")]
        public float StartleImpulse = 0.45f;
        public float StartleOffsetCap = 0.85f;
        public float StartleOffsetDecay = 0.16f;
        public float RecastSpamWindow = 3f;
        public float RecastTimeMultiplierMax = 2.5f;
        public float RecastRadiusMultiplierMax = 1.35f;
        public float LureRecovery = 2.5f;
        public float LureFatigueFloor = 0.55f;
        public float LureGuaranteeThreshold = 0.60f;

        [Header("Avoidance")]
        public float AvoidanceRadiusBodyLengths = 3.6f;
        public float AvoidanceGain = 5.5f;
        public float AvoidanceOffsetCapBodyLengths = 0.85f;
        public float AvoidanceTargetSmoothing = 1.1f;
        public float AvoidanceSmoothing = 0.9f;

        [Header("Catch presentation")]
        public float ReelDuration = 0.30f;
        public float CastDuration = 0.30f;
        public float CatchLiftDuration = 0.30f;
        public float CatchFlyDuration = 0.55f;
        public float CatchArcHeight = 1.7f;
        public float BiteBackBase = 0.16f;
        public float BiteBackPerLength = 0.52f;

        [Header("Vertex presentation")]
        public float WaveAmplitudeScale = 0.40f;
        public float PivotU = 0.36f;
        public float TurnBendScale = 0.22f;
        public float MaxTurnBend = 0.60f;

        [Header("Rules — change only with a documented balance pass")]
        public bool EnableLure = true;
        public bool EnableAutomaticReel = true;
        public bool EnableSpeedFit = true;
        public bool EnableAvoidance = true;
        public bool EnableCatchFlight = true;

        public Rect PondRect
        {
            get { return new Rect(-PondSize.x * 0.5f, -PondSize.y * 0.5f, PondSize.x, PondSize.y); }
        }

        public static FishingV2TuningAsset CreateRuntimeDefaults()
        {
            FishingV2TuningAsset tuning = CreateInstance<FishingV2TuningAsset>();
            tuning.name = "FishingV2Tuning_Runtime";
            return tuning;
        }

        public float ClampDelta(float dt)
        {
            return Mathf.Clamp(dt, 0f, Mathf.Max(0.001f, MaxDeltaTime));
        }
    }
}
