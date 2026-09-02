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
            Vector3 from = new Vector3(fish.Position.x, fish.Position.y, 0.52f);
            Flight flight = new Flight
            {
                Object = flyer,
                From = from,
                Target = target,
                Heading = fish.HeadingRadians,
                Time = 0f,
                LiftDuration = _tuning != null ? _tuning.CatchLiftDuration : 0.30f,
                Total = (_tuning != null ? _tuning.CatchLiftDuration : 0.30f) + (_tuning != null ? _tuning.CatchFlyDuration : 0.55f),
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
                float flyDuration = Mathf.Max(0.01f, flight.Total - liftDuration);
                float lift = Mathf.Clamp01(flight.Time / Mathf.Max(0.01f, liftDuration));
                float fly = Mathf.Clamp01((flight.Time - liftDuration) / flyDuration);
                float liftEase = SmoothStep(lift);
                float flyEase = SmoothStep(fly);

                Vector3 position;
                float scale;
                float alpha;
                if (flight.Time < liftDuration)
                {
                    position = flight.From + Vector3.forward * (liftEase * 0.35f);
                    scale = 1f + liftEase * 0.20f;
                    alpha = 1f;
                }
                else
                {
                    position = Vector3.Lerp(flight.From + Vector3.forward * 0.35f, flight.Target, flyEase);
                    position.z += Mathf.Sin(fly * Mathf.PI) * (_tuning != null ? _tuning.CatchArcHeight : 1.7f) * 0.20f;
                    scale = 1.20f - flyEase * 0.86f;
                    alpha = 1f - Mathf.Clamp01((fly - 0.75f) / 0.25f);
                }

                flight.Object.transform.position = position;
                flight.Object.transform.rotation = Quaternion.Euler(0f, 0f, flight.Heading - fly * 0.5f + Mathf.Sin(fly * 8f) * 0.09f * (1f - fly));
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
                    flight.PropertyBlock.SetFloat("_Pivot", _tuning != null ? _tuning.PivotU : 0.36f);
                    flight.PropertyBlock.SetFloat("_RimStrength", 1f);
                    flight.Renderer.SetPropertyBlock(flight.PropertyBlock);
                }

                if (flight.Time >= flight.Total)
                {
                    if (flight.OnArrive != null) flight.OnArrive(flight.Species, flight.SizeCm);
                    DestroyObjectSafe(flight.Object);
                    _flights.RemoveAt(i);
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
