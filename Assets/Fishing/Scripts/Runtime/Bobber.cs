using System.Collections.Generic;
using UnityEngine;

namespace Fishing
{
    public enum BobberPhase { Idle, InWater, Reeling, Casting }

    /// <summary>
    /// 찌 — 착수 · 놀람 · 유인 · 히트 슬롯. 기획서 §1-2, §5-0.
    ///
    /// 이 게임에서 플레이어의 유일한 필수 조작이 여기로 들어온다.
    /// </summary>
    public class Bobber : MonoBehaviour
    {
        [SerializeField] Transform _visual;

        public Vector2 Pos { get; private set; }
        public BobberPhase Phase { get; private set; } = BobberPhase.Idle;
        public bool IsInWater => Phase == BobberPhase.InWater;
        public Fish HitFish { get; private set; }

        float _phaseTimer;
        float _lastCastTime = -99f;
        float _lastLureTime = -99f;
        int _spamCount;
        Vector2 _pendingPos;

        FishingSession _session;
        readonly List<(Fish fish, float dist)> _lureCandidates = new List<(Fish, float)>();

        public void Bind(FishingSession session) { _session = session; }

        // ─────────────────────────────────────────────────────────
        /// <summary>수면을 눌렀을 때. 이미 물려 있으면 즉시 회수, 아니면 (재)착수.</summary>
        public void OnWaterTapped(Vector2 worldPos)
        {
            if (HitFish != null) { _session.CatchFish(HitFish); return; }
            if (Phase == BobberPhase.Reeling || Phase == BobberPhase.Casting) return;
            Cast(worldPos);
        }

        public void Cast(Vector2 worldPos)
        {
            Pos = _session.ClampToWater(worldPos, 0.5f);
            Phase = BobberPhase.InWater;
            if (_visual) { _visual.position = Pos; _visual.gameObject.SetActive(true); }

            // 연속 재착수 페널티 — 지속시간이 주로 받고, 반경은 조금만 (§1-2)
            _spamCount = (Time.time - _lastCastTime < FishingTuning.RecastSpamWindow) ? _spamCount + 1 : 0;
            _lastCastTime = Time.time;
            float timeMul = Mathf.Min(FishingTuning.RecastTimeMulMax, 1f + _spamCount * 0.5f);
            float radMul  = Mathf.Min(FishingTuning.RecastRadiusMulMax, 1f + _spamCount * 0.12f);

            // 찌를 걷어올렸다 다시 던지면 그 찌를 향하던 관심은 거리와 무관하게 전부 사라진다.
            // (§1-3 "잡어를 잡을수록 대물이 멀어진다"를 실제로 성립시키는 지점)
            foreach (var f in _session.Fishes)
                if (f.State == FishState.Interested || f.State == FishState.Notice)
                    f.ReturnToRoam(1.2f);

            Startle(Pos, radMul, timeMul, resetInterest: false);
            Lure(Pos, radMul);

            _session.SpawnRipple(Pos, 1.6f);
            _session.PlayCastSfx();
        }

        /// <summary>빈 찌를 걷어올린다 → 같은 자리에 다시 던진다 (획득 후 자동 흐름).</summary>
        public void ReelAndRecast()
        {
            _pendingPos = Pos;
            Phase = BobberPhase.Reeling;
            _phaseTimer = FishingTuning.ReelDuration;
            HitFish = null;
            if (_visual) _visual.gameObject.SetActive(false);
        }

        public void Retract()
        {
            Phase = BobberPhase.Idle;
            HitFish = null;
            if (_visual) _visual.gameObject.SetActive(false);
        }

        public void Tick(float dt)
        {
            switch (Phase)
            {
                case BobberPhase.Reeling:
                    _phaseTimer -= dt;
                    if (_phaseTimer <= 0f) { Phase = BobberPhase.Casting; _phaseTimer = FishingTuning.CastDuration; }
                    break;
                case BobberPhase.Casting:
                    _phaseTimer -= dt;
                    if (_phaseTimer <= 0f) Cast(_pendingPos);
                    break;
            }
        }

        // ── 놀람 ─────────────────────────────────────────────────
        /// <summary>
        /// 반경 배율과 지속 배율을 분리한다.
        /// ⚠️ 페널티가 반경까지 2.5배로 키우면 대물 반경이 연못 폭의 절반이 되어 물고기가 날아간다.
        /// </summary>
        public void Startle(Vector2 from, float radiusMul, float timeMul, bool resetInterest)
        {
            foreach (var f in _session.Fishes)
                f.ApplyStartle(from, radiusMul, timeMul, resetInterest);
        }

        // ── 유인 — §5-0 ──────────────────────────────────────────
        /// <summary>
        /// 착수한 물장구는 '전부 도망'이 아니다.
        /// 코앞의 물고기는 놀라 물러나지만, 중거리의 물고기는 오히려 무슨 일인가 보러 온다.
        /// 유인 계수는 어종마다 다르고 대물은 0이다 — "작은 놈은 물장구에 몰리고, 대물은 조용해져야 온다".
        /// </summary>
        void Lure(Vector2 from, float radiusMul)
        {
            // 물장구가 잦으면 시큰둥해진다. 이게 없으면 "빨리 잡을수록 유인이 더 자주 발동"하는
            // 복리가 생겨 탭이 사실상 의무가 된다.
            float gap = Time.time - _lastLureTime;
            float fatigue = Mathf.Clamp(gap / FishingTuning.LureRecovery, FishingTuning.LureFatigueFloor, 1f);
            _lastLureTime = Time.time;

            _lureCandidates.Clear();
            bool anyPulled = false;

            foreach (var f in _session.Fishes)
            {
                if (!f.CanBeLured(from, radiusMul, out float d)) continue;
                _lureCandidates.Add((f, d));
                if (Random.value < f.Species.lureChance * fatigue) { f.BecomeInterested(); anyPulled = true; }
            }

            // 한 마리도 안 걸리면 가장 가까운 놈을 끌어온다.
            // 조작에 대한 응답이 확률이면, 아무 반응 없는 착수가 생기고 그게 곧 '먹통'으로 읽힌다.
            if (!anyPulled && _lureCandidates.Count > 0 && fatigue > FishingTuning.LureGuaranteeThreshold)
            {
                int best = 0;
                for (int i = 1; i < _lureCandidates.Count; i++)
                    if (_lureCandidates[i].dist < _lureCandidates[best].dist) best = i;
                _lureCandidates[best].fish.BecomeInterested();
            }
        }

        // ── 히트 슬롯 ────────────────────────────────────────────
        /// <summary>도착한 물고기가 자리를 잡으려 한다. 이미 차 있으면 false.</summary>
        public bool TryTakeBite(Fish fish, Vector2 approachDir)
        {
            if (HitFish != null) return false;

            HitFish = fish;
            // 큰 물고기일수록 뒤에서 문다 — 찌를 가리지 않게
            float back = FishingTuning.BiteBackBase
                       + fish.Species.bodyLength * FishingTuning.BiteBackPerLength;
            fish.EnterBite(Pos - approachDir * back, fish.Species.biteWindow);
            _session.SpawnRipple(Pos, 1.2f);
            _session.PlayBiteSfx();
            return true;
        }

        public void ClearHit() { HitFish = null; }

        void LateUpdate()
        {
            if (_visual && IsInWater)
            {
                // 입질 중이면 찌가 잠기고 좌우로 떨린다
                float dip = HitFish != null ? 1f : 0f;
                float shake = dip * Mathf.Sin(Time.time * 17f) * 0.03f;
                _visual.position = new Vector3(Pos.x + shake, Pos.y - dip * 0.06f, _visual.position.z);
                _visual.localScale = Vector3.one * (1f - dip * 0.2f);
            }
        }
    }
}
