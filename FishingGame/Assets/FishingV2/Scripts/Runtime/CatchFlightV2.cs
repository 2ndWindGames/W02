using System;
using System.Collections.Generic;
using UnityEngine;

namespace Fishing.V2
{
    /// <summary>
    /// 물고기를 수면에서 끌어올려 바구니 슬롯으로 보내는 보상 연출.
    /// 집계 콜백은 비행이 끝나는 시점에 호출한다.
    /// </summary>
    public sealed class CatchFlightV2 : MonoBehaviour
    {
        private sealed class Flight
        {
            public GameObject Object;
            public Vector3 From;
            public Vector3 Target;
            public float Heading;
            public float Time;
            public float Total;
            public float LiftDuration;
            public Vector2 Normal;
            public float Bow;
            public float RimZ;
            public float BaseScale;
            public FishSpeciesConfig Species;
            public float SizeCm;
            public Action<FishSpeciesConfig, float> OnArrive;
            public MaterialPropertyBlock PropertyBlock;
            public MeshRenderer Renderer;
        }

        private readonly List<Flight> _flights = new List<Flight>();
        private FishingV2TuningAsset _tuning;
        private Material _flightMaterial;
        private int _basketIndex;

        public void Initialize(FishingV2TuningAsset tuning, Material flightMaterial)
        {
            _tuning = tuning;
            _flightMaterial = flightMaterial;
        }

        public void Launch(FishAgentV2 fish, Vector3 target, Action<FishSpeciesConfig, float> onArrive)
        {
            if (fish == null || fish.SharedMesh == null)
            {
                return;
            }

            GameObject flyer = new GameObject("CatchFlight_" + fish.Species.SpeciesId);
            flyer.transform.SetParent(transform, true);
            flyer.layer = gameObject.layer;
            MeshFilter meshFilter = flyer.AddComponent<MeshFilter>();
            meshFilter.sharedMesh = fish.SharedMesh;
            MeshRenderer renderer = flyer.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = fish.SharedMaterial != null ? fish.SharedMaterial : _flightMaterial;

            // Keep the reward flight in the same foreground depth band as the fish and bobber.
            Vector3 from = new Vector3(fish.Position.x, fish.Position.y, fish.WorldDepthZ);
            Vector2 delta = new Vector2(target.x - from.x, target.y - from.y);
            float distance = Mathf.Max(0.001f, delta.magnitude);
            Vector2 pondCenter = _tuning != null ? _tuning.PondRect.center : Vector2.zero;
            Vector2 normal = FishingV2CatchMath.InwardNormal(new Vector2(from.x, from.y), new Vector2(target.x, target.y), pondCenter);
            Flight flight = new Flight
            {
                Object = flyer,
                From = from,
                Target = target,
                Heading = fish.HeadingRadians,
                Time = 0f,
                LiftDuration = _tuning != null ? _tuning.CatchLiftDuration : 0.30f,
                Total = (_tuning != null ? _tuning.CatchLiftDuration : 0.30f) +
                    (_tuning != null ? _tuning.CatchFlyDuration : 0.55f) +
                    (_tuning != null ? _tuning.CatchSettleDuration : 0.36f),
                Normal = normal,
                Bow = FishingV2CatchMath.FlightBow(distance),
                RimZ = target.z + 0.10f,
                BaseScale = fish.SizeScale,
                Species = fish.Species,
                SizeCm = fish.SizeCm,
                OnArrive = onArrive,
                PropertyBlock = new MaterialPropertyBlock(),
                Renderer = renderer
            };

            flyer.transform.position = from;
            flyer.transform.localScale = Vector3.one * fish.SizeScale;
            _flights.Add(flight);
        }

        public void Tick(float dt)
        {
            dt = _tuning != null ? _tuning.ClampDelta(dt) : Mathf.Clamp(dt, 0f, 0.05f);
            for (int i = _flights.Count - 1; i >= 0; i--)
            {
                Flight flight = _flights[i];
                flight.Time += dt;
                float liftDuration = flight.LiftDuration;
                float settleDuration = _tuning != null ? _tuning.CatchSettleDuration : 0.36f;
                float flyDuration = Mathf.Max(0.01f, flight.Total - liftDuration - settleDuration);
                float lift = Mathf.Clamp01(flight.Time / Mathf.Max(0.01f, liftDuration));
                float fly = Mathf.Clamp01((flight.Time - liftDuration) / flyDuration);
                float settle = Mathf.Clamp01((flight.Time - liftDuration - flyDuration) / Mathf.Max(0.01f, settleDuration));
                float liftEase = SmoothStep(lift);
                float flyEase = SmoothStep(fly);

                Vector3 position;
                float scale;
                if (flight.Time < liftDuration)
                {
                    position = flight.From + Vector3.forward * (liftEase * 0.55f);
                    float rise = liftEase;
                    scale = flight.BaseScale * FishingV2CatchMath.LiftScale(rise, 0f, _tuning != null ? _tuning.CatchLiftScale : 1.5f);
                }
                else if (flight.Time < liftDuration + flyDuration)
                {
                    Vector3 start = flight.From + Vector3.forward * 0.55f;
                    position = Vector3.Lerp(start, flight.Target, flyEase);
                    float bow = Mathf.Sin(flyEase * Mathf.PI) * flight.Bow;
                    position.x += flight.Normal.x * bow;
                    position.y += flight.Normal.y * bow;
                    float top = flight.From.z + 0.55f;
                    position.z = top + Mathf.Sin(fly * Mathf.PI) * (_tuning != null ? _tuning.CatchArcHeight : 1.7f) +
                        (flight.RimZ - top) * flyEase * flyEase;
                    float rise = Mathf.Clamp((position.z - flight.From.z) / 0.55f, 0f, 4f);
                    scale = flight.BaseScale * FishingV2CatchMath.LiftScale(rise, 0.42f * flyEase * flyEase, _tuning != null ? _tuning.CatchLiftScale : 1.5f);
                }
                else
                {
                    float s = settle;
                    float firstEnd = _tuning != null ? _tuning.CatchBounceOneEnd : 0.46f;
                    float secondEnd = _tuning != null ? _tuning.CatchBounceTwoEnd : 0.78f;
                    float firstHeight = _tuning != null ? _tuning.CatchBounceOneHeight : 0.46f;
                    float secondHeight = _tuning != null ? _tuning.CatchBounceTwoHeight : 0.19f;
                    float h;
                    h = FishingV2CatchMath.BasketBounceHeight(s, firstHeight, secondHeight, firstEnd, secondEnd);

                    float jig = Mathf.Sin(s * Mathf.PI * 4.6f) * Mathf.Pow(1f - s, 1.6f) * 0.16f;
                    position = flight.Target + new Vector3(flight.Normal.x * jig, flight.Normal.y * jig, 0f);
                    position.z = flight.RimZ + h - 0.62f * Mathf.Pow(s, 2.2f);
                    float rise = Mathf.Clamp((position.z - flight.From.z) / 0.55f, 0f, 4f);
                    float sink = 0.42f + 0.52f * s * s;
                    scale = flight.BaseScale * FishingV2CatchMath.LiftScale(rise, sink, _tuning != null ? _tuning.CatchLiftScale : 1.5f);

                    if (s >= 1f)
                    {
                        if (flight.OnArrive != null) flight.OnArrive(flight.Species, flight.SizeCm);
                        DestroyObjectSafe(flight.Object);
                        _flights.RemoveAt(i);
                        continue;
                    }
                }

                flight.Object.transform.position = position;
                float normalizedFlight = Mathf.Clamp01(flight.Time / Mathf.Max(0.01f, flight.Total));
                flight.Object.transform.rotation = Quaternion.Euler(0f, 0f,
                    flight.Heading - normalizedFlight * 0.5f + Mathf.Sin(normalizedFlight * 8f) * 0.09f * (1f - normalizedFlight));
                flight.Object.transform.localScale = Vector3.one * scale;

                if (flight.Renderer != null)
                {
                    flight.PropertyBlock.Clear();
                    flight.PropertyBlock.SetFloat("_Amp", 0f);
                    flight.PropertyBlock.SetFloat("_Phase", 0f);
                    flight.PropertyBlock.SetFloat("_Turn", 0f);
                    flight.PropertyBlock.SetFloat("_Len", flight.Species.Visual.Length);
                    flight.PropertyBlock.SetFloat("_Lag", flight.Species.Visual.WaveLag);
                    flight.PropertyBlock.SetFloat("_Exp", flight.Species.Visual.WaveExponent);
                    flight.PropertyBlock.SetFloat("_Hinge", flight.Species.Visual.WaveHinge);
                    flight.PropertyBlock.SetFloat("_Spread", flight.Species.Visual.WaveSpread);
                    flight.PropertyBlock.SetFloat("_Jet", 0.5f);
                    flight.PropertyBlock.SetFloat("_Roll", 0f);
                    flight.PropertyBlock.SetFloat("_Rip", 0f);
                    flight.PropertyBlock.SetFloat("_RipW", flight.Species.Visual.FinWave);
                    flight.PropertyBlock.SetFloat("_RipF", flight.Species.Visual.FinRate);
                    flight.PropertyBlock.SetFloat("_TimeOffset", 0f);
                    flight.PropertyBlock.SetFloat("_Drift", 0f);
                    flight.PropertyBlock.SetFloat("_TurnLag", 0f);
                    flight.PropertyBlock.SetFloat("_ArcBody", flight.Species.Visual.ArcBody);
                    flight.PropertyBlock.SetFloat("_ArmSwing", 0f);
                    flight.PropertyBlock.SetFloat("_ArmTuck", 1f);
                    flight.PropertyBlock.SetFloat("_ArmFlow", 0f);
                    flight.PropertyBlock.SetFloat("_DriftPh", 0f);
                    flight.PropertyBlock.SetFloat("_MantleJet", flight.Species.Visual.MantleJet ? 1f : 0f);
                    flight.PropertyBlock.SetFloat("_TurnPrep", 0f);
                    flight.PropertyBlock.SetFloat("_Pivot", _tuning != null ? _tuning.PivotU : 0.36f);
                    flight.PropertyBlock.SetFloat("_RimStrength", 1f);
                    flight.PropertyBlock.SetFloat("_Depth", 0f);
                    flight.Renderer.SetPropertyBlock(flight.PropertyBlock);
                }
            }
        }

        public Vector3 NextBasketTarget(Rect pond)
        {
            // 첫 슬라이스에서는 별도 UI 캔버스 없이 수면 안쪽 오른쪽에 바구니 슬롯을 둔다.
            // 카메라 밖으로 날려버리면 획득 연출이 실제로 보상처럼 읽히지 않는다.
            float x = pond.xMax - 0.55f;
            float y = pond.yMin + 0.55f + (_basketIndex % 5) * 0.72f;
            _basketIndex++;
            return new Vector3(x, y, 0.65f);
        }

        private static float SmoothStep(float value)
        {
            value = Mathf.Clamp01(value);
            return value * value * (3f - 2f * value);
        }

        private static void DestroyObjectSafe(UnityEngine.Object target)
        {
            if (target == null) return;
            if (Application.isPlaying) UnityEngine.Object.Destroy(target);
            else UnityEngine.Object.DestroyImmediate(target);
        }
    }
}
