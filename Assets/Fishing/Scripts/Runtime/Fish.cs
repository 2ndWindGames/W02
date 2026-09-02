using UnityEngine;

namespace Fishing
{
    public enum FishState
    {
        Roam,        // 궤도 추종
        Notice,      // 발견 동작 — 멈칫하고 머리를 튼다 (§5-1)
        Interested,  // 궤도 이탈, 찌로 접근
        Bite,        // 히트 슬롯 점유
    }

    /// <summary>
    /// 물고기 한 마리. 상태머신 + 이동. 기획서 §5.
    ///
    /// ⚠️ 이동 규칙 (§5-3): 위치와 회전을 절대 따로 굴리지 않는다.
    ///    항상 "선회율 제한으로 머리를 돌리고, 머리 방향으로만 전진"한다.
    ///    각도를 목표로 보간하면 속도가 0에 가까울 때 제자리에서 빙글 돈다.
    /// </summary>
    public class Fish : MonoBehaviour
    {
        public FishSpeciesSO Species { get; private set; }
        public FishState State { get; private set; } = FishState.Roam;

        // 위치·방향
        public Vector2 Pos { get; private set; }
        public float Heading { get; private set; }          // 라디안
        public float TurnRateNow { get; private set; }      // 실제 각속도 (뱅킹용)
        public float SpeedNow { get; private set; }
        public float SizeCm { get; private set; }
        public float SizeScale { get; private set; } = 1f;

        // 경로
        PathEvaluator.LaneParams _lane;
        Vector2 _loopCenter;
        float _loopPhase;
        float _t;                       // 경로 파라미터
        Vector2 _offset;                // 궤도로부터의 이탈(놀람 등). 감쇠한다.

        // School
        public bool IsLeader { get; private set; }
        Vector2? _formationOffset;
        Vector2 _noiseSeed;

        // HoverDash
        Vector2 _hoverPos, _homePos;
        float _hoverTimer;
        float? _aimAngle;
        bool _dashing;
        float _dashT, _dashLen;

        // 타이머
        float _stun, _cooldown, _curiosityTimer, _noticeTimer, _interestTimer;
        float _biteTimer, _biteWindowMax, _biteHeading;
        Vector2 _bitePos;

        FishingSession _session;
        Vector2 _prevPos;

        public float BiteRemaining01 => _biteWindowMax > 0f ? Mathf.Clamp01(_biteTimer / _biteWindowMax) : 0f;

        // ─────────────────────────────────────────────────────────
        public void Init(FishingSession session, FishSpeciesSO species, Rect water)
        {
            _session = session;
            Species = species;
            SizeCm = SampleSize(species);
            SizeScale = Mathf.Lerp(0.92f, 1.08f, Mathf.InverseLerp(species.sizeCm.x, species.sizeCm.y, SizeCm));
            State = FishState.Roam;
            _offset = Vector2.zero;
            _t = 0f;
            _stun = _cooldown = _noticeTimer = _interestTimer = 0f;
            _curiosityTimer = Random.value * FishingTuning.CuriosityTick;
            IsLeader = false;
            _formationOffset = null;
            _noiseSeed = new Vector2(Random.value * 10f, Random.value * 10f);

            float y = Random.Range(water.yMin + species.band.x * water.height,
                                   water.yMin + species.band.y * water.height);

            switch (species.pathType)
            {
                case PathType.Lane:
                case PathType.School:
                {
                    float sign = Random.value < 0.5f ? 1f : -1f;
                    float a = Random.Range(-0.35f, 0.35f);
                    _lane = new PathEvaluator.LaneParams
                    {
                        origin = new Vector2(Random.Range(water.xMin + 1f, water.xMax - 1f), y),
                        dir = new Vector2(Mathf.Cos(a) * sign, Mathf.Sin(a) * sign),
                        speed = species.cruiseSpeed,
                        amp = species.laneAmp,
                        period = species.lanePeriod,
                        phase = Random.value * Mathf.PI * 2f,
                    };
                    // ⚠️ _t 를 랜덤하게 주면 안 된다. 경로가 origin + dir*speed*t 라서
                    //    t=20 이면 스폰 순간 이미 24유닛 진행한 화면 밖에 나타난다. (§11-1)
                    _t = 0f;
                    break;
                }
                case PathType.Loop:
                {
                    _loopCenter = new Vector2(
                        Mathf.Clamp(water.center.x + Random.Range(-1.2f, 1.2f),
                                    water.xMin + species.loopA * 0.55f + 0.6f,
                                    water.xMax - species.loopA * 0.55f - 0.6f),
                        Mathf.Clamp(y, water.yMin + species.loopB + 0.6f, water.yMax - species.loopB - 0.6f));
                    _loopPhase = Random.value * Mathf.PI * 2f;
                    break;
                }
                case PathType.HoverDash:
                {
                    Vector2 anchor = _session.RandomRockPoint(y);
                    _hoverPos = _homePos = anchor;
                    _hoverTimer = Random.Range(species.hoverTime.x, species.hoverTime.y);
                    _dashing = false;
                    _aimAngle = null;
                    break;
                }
            }

            Pos = _prevPos = EvaluatePath();
            Heading = Random.value * Mathf.PI * 2f;
            transform.position = Pos;
        }

        public void MakeLeader(PathEvaluator.LaneParams lane, float t)
        {
            IsLeader = true; _formationOffset = null; _lane = lane; _t = t;
        }

        public void MakeFollower(Vector2 formationOffset)
        {
            IsLeader = false; _formationOffset = formationOffset;
        }

        public void ShareLane(PathEvaluator.LaneParams lane, float t) { _lane = lane; _t = t; }

        static float SampleSize(FishSpeciesSO sp)
        {
            // 정규분포 근사 — 큰 개체가 희소하게
            float u = (Random.value + Random.value + Random.value) / 3f;
            return Mathf.Lerp(sp.sizeCm.x, sp.sizeCm.y, u);
        }

        // ─────────────────────────────────────────────────────────
        public void Tick(float dt, Bobber bobber, Rect water)
        {
            _prevPos = Pos;
            if (_stun > 0f) _stun -= dt;
            if (_cooldown > 0f) _cooldown -= dt;

            switch (State)
            {
                case FishState.Roam:       TickRoam(dt, bobber, water); break;
                case FishState.Notice:     TickNotice(dt, bobber); break;
                case FishState.Interested: TickInterested(dt, bobber); break;
                case FishState.Bite:       TickBite(dt); break;
            }

            SpeedNow = (Pos - _prevPos).magnitude / Mathf.Max(dt, 1e-4f);
            transform.position = Pos;
        }

        // ── Roam ─────────────────────────────────────────────────
        void TickRoam(float dt, Bobber bobber, Rect water)
        {
            _t += dt;
            if (Species.pathType == PathType.HoverDash) TickHover(dt);

            Vector2 pathPos = EvaluatePath();
            Pos = pathPos + _offset;

            _offset *= Mathf.Pow(FishingTuning.OffsetDecayPerSecond, dt);
            float om = _offset.magnitude;
            if (om > FishingTuning.MaxPathOffset) _offset *= FishingTuning.MaxPathOffset / om;

            // 랩 — 자기 경로를 가진 개체만. 추종 개체는 리더 위치에서 파생된다.
            if ((Species.pathType == PathType.Lane || Species.pathType == PathType.School)
                && !_formationOffset.HasValue)
            {
                if (PathEvaluator.WrapOrigin(ref _lane, Pos - _offset, water))
                    Pos = EvaluatePath() + _offset;
            }

            // 안전망 — 어떤 경로로든 크게 벗어나면 경로를 현재 위치에 다시 앵커한다
            if (!water.Contains(Pos) &&
                (Pos.x < water.xMin - 3.5f || Pos.x > water.xMax + 3.5f ||
                 Pos.y < water.yMin - 3.5f || Pos.y > water.yMax + 3.5f))
            {
                ReanchorAt(new Vector2(Mathf.Clamp(Pos.x, water.xMin + 0.6f, water.xMax - 0.6f),
                                       Mathf.Clamp(Pos.y, water.yMin + 0.6f, water.yMax - 0.6f)));
            }

            // 회전 — 경로가 위치를 결정하므로 머리는 진행 방향을 따라가되 선회율로 제한
            if (Species.pathType != PathType.HoverDash)
            {
                Vector2 v = Pos - _prevPos;
                if (v.sqrMagnitude > 1e-6f)
                    TurnRateNow = Steer(Mathf.Atan2(v.y, v.x), dt, Species.turnRateDeg * 2.5f);
                else TurnRateNow = 0f;
            }

            // 호기심 판정
            if (bobber != null && bobber.IsInWater && _stun <= 0f && _cooldown <= 0f && _session.IsRunning)
            {
                _curiosityTimer -= dt;
                if (_curiosityTimer <= 0f)
                {
                    _curiosityTimer = FishingTuning.CuriosityTick;
                    float d = Vector2.Distance(Pos, bobber.Pos);
                    if (d < Species.NoticeRadius && d >= Species.minNoticeRadius
                        && Random.value < Species.curiosity)
                        BecomeInterested();
                }
            }
        }

        void TickHover(float dt)
        {
            var sp = Species;
            if (_dashing)
            {
                // 돌진은 머리 방향으로만. 목표 좌표로 보간하면 몸이 옆으로 미끄러진다.
                _dashT += dt;
                float u = Mathf.Clamp01(_dashT / sp.dashTime);
                float spd = _dashLen * 3f * (1f - u) * (1f - u) / sp.dashTime;   // ease-out 속도 곡선
                _hoverPos += new Vector2(Mathf.Cos(Heading), Mathf.Sin(Heading)) * spd * dt;
                if (u >= 1f)
                {
                    _dashing = false; _aimAngle = null;
                    _hoverTimer = Random.Range(sp.hoverTime.x, sp.hoverTime.y);
                }
            }
            else
            {
                _hoverTimer -= dt;
                // ⚠️ 돌진 순간에 방향을 정하면 최대 180도를 즉시 돌아야 해서 제자리 회전이 된다.
                //    부유하는 동안 미리 조준해두고, 돌진은 이미 향한 쪽으로만 나간다.
                if (!_aimAngle.HasValue)
                {
                    Vector2 fromHome = _hoverPos - _homePos;
                    _aimAngle = fromHome.magnitude > sp.homeRadius * 0.7f
                        ? Mathf.Atan2(-fromHome.y, -fromHome.x)
                        : Heading + Random.Range(-1.2f, 1.2f);
                }
                TurnRateNow = Steer(_aimAngle.Value, dt, sp.turnRateDeg * 0.7f);
                _hoverPos += new Vector2(Mathf.Cos(Heading), Mathf.Sin(Heading)) * 0.07f * dt;
                if (_hoverTimer <= 0f)
                {
                    _dashing = true; _dashT = 0f;
                    _dashLen = Random.Range(sp.dashDistance.x, sp.dashDistance.y);
                }
            }
        }

        // ── Notice ───────────────────────────────────────────────
        void TickNotice(float dt, Bobber bobber)
        {
            if (bobber == null || !bobber.IsInWater) { ReturnToRoam(0.6f); return; }
            _noticeTimer -= dt;
            // 급감속하면서 머리를 홱 튼다. 멈추는 게 아니라 미끄러지며 꺾는 동작이라
            // 제자리 회전으로 안 보인다. 감속 자체가 발견의 신호.
            Vector2 to = bobber.Pos - Pos;
            TurnRateNow = Steer(Mathf.Atan2(to.y, to.x), dt,
                                Species.turnRateDeg * FishingTuning.NoticeTurnBoost);
            Advance(Species.approachSpeed * FishingTuning.NoticeSpeedScale, dt);
            if (_noticeTimer <= 0f) State = FishState.Interested;
        }

        // ── Interested ───────────────────────────────────────────
        void TickInterested(float dt, Bobber bobber)
        {
            if (bobber == null || !bobber.IsInWater) { ReturnToRoam(0.6f); return; }

            Vector2 to = bobber.Pos - Pos;
            float d = to.magnitude;

            if (d < FishingTuning.ArriveRadius)
            {
                // 도착. 자리가 비었으면 물고, 차 있으면 흥미를 잃고 돌아간다.
                // (줄 세우면 화면만 어지럽고 플레이어 판단에 보태는 게 없다 — §5-2)
                if (!bobber.TryTakeBite(this, to / Mathf.Max(d, 1e-4f)))
                    ReturnToRoam(FishingTuning.RejectedCooldown);
                return;
            }

            // 가까울수록 더 급하게 돈다. 먹이를 낚아채는 동작이면서,
            // 선회 반경 때문에 목표를 영원히 맴도는 미사일 문제를 막는 장치이기도 하다.
            float boost = 1f + Mathf.Clamp01((FishingTuning.CloseTurnBoostRange - d)
                              / FishingTuning.CloseTurnBoostRange) * FishingTuning.CloseTurnBoostMax;
            TurnRateNow = Steer(Mathf.Atan2(to.y, to.x), dt, Species.turnRateDeg * boost);
            Advance(Species.approachSpeed, dt);

            _interestTimer += dt;
            if (_interestTimer > FishingTuning.ApproachTimeout)
                ReturnToRoam(FishingTuning.RejectedCooldown);
        }

        // ── Bite ─────────────────────────────────────────────────
        public void EnterBite(Vector2 bitePos, float window)
        {
            State = FishState.Bite;
            _bitePos = bitePos;
            _biteTimer = _biteWindowMax = window;
            _biteHeading = Heading;
        }

        void TickBite(float dt)
        {
            Pos = Vector2.Lerp(Pos, _bitePos, Mathf.Min(1f, dt * 9f));
            // 문 상태에서는 방향을 바꾸지 않는다. 좌우로 파닥이기만 한다.
            Heading = _biteHeading + Mathf.Sin(Time.time * 13f) * 0.20f;
            TurnRateNow = 0f;
            _biteTimer -= dt;
            if (_biteTimer <= 0f) _session.OnBiteWindowExpired(this);
        }

        // ── 상태 전이 ────────────────────────────────────────────
        public void BecomeInterested()
        {
            _interestTimer = 0f;
            _cooldown = 0f; _stun = 0f;
            State = FishingTuning.NoticeDuration > 0f ? FishState.Notice : FishState.Interested;
            _noticeTimer = FishingTuning.NoticeDuration;
        }

        /// <summary>
        /// 궤도 복귀 — 텔레포트 대신 현재 위치를 오프셋으로 흡수해 부드럽게 돌아온다.
        /// ⚠️ 무리 추종 개체는 자기 경로를 안 쓴다. 그걸 기준으로 오프셋을 잡으면
        ///    수십 유닛짜리 오프셋이 생겨 다음 프레임에 화면 밖으로 날아간다. (§11-1)
        /// </summary>
        public void ReturnToRoam(float cooldown)
        {
            if (Species.pathType == PathType.HoverDash)
            {
                _hoverPos = Pos; _dashing = false; _aimAngle = null;
                _hoverTimer = Random.Range(Species.hoverTime.x, Species.hoverTime.y);
                _offset = Vector2.zero;
            }
            else
            {
                Vector2 pathPos = EvaluatePath();
                _offset = Pos - pathPos;
                float m = _offset.magnitude;
                if (m > FishingTuning.MaxPathOffset) _offset *= FishingTuning.MaxPathOffset / m;
            }
            State = FishState.Roam;
            _cooldown = Mathf.Max(_cooldown, cooldown);
            _noticeTimer = _interestTimer = 0f;
        }

        void ReanchorAt(Vector2 p)
        {
            Pos = p; _offset = Vector2.zero;
            switch (Species.pathType)
            {
                case PathType.Lane:
                case PathType.School:
                    _formationOffset = null; IsLeader = true; _t = 0f;
                    _lane.origin = p;
                    break;
                case PathType.HoverDash:
                    _hoverPos = _homePos = p; _dashing = false; _aimAngle = null;
                    break;
            }
        }

        // ── 놀람 / 유인 ──────────────────────────────────────────
        public void ApplyStartle(Vector2 from, float radiusMul, float timeMul, bool resetInterest)
        {
            if (State == FishState.Bite) return;

            Vector2 away = Pos - from;
            float d = away.magnitude;
            float R = Species.startleRadius * radiusMul;
            if (d >= R || d < 1e-3f) return;

            if (resetInterest && State != FishState.Roam) ReturnToRoam(0.35f);

            float k = 1f - d / R;
            _offset += away / d * (k * Species.startleRadius * FishingTuning.StartleImpulse);
            // ⚠️ 누적 상한 — 이게 없으면 연속 재착수 시 화면 밖으로 날아간다
            float cap = Species.startleRadius * FishingTuning.StartleOffsetCap;
            float m = _offset.magnitude;
            if (m > cap) _offset *= cap / m;

            _stun = Mathf.Max(_stun, Species.startleTime * timeMul);
            _cooldown = Mathf.Max(_cooldown, Species.startleTime * timeMul * 0.8f);
        }

        public bool CanBeLured(Vector2 from, float radiusMul, out float dist)
        {
            dist = Vector2.Distance(Pos, from);
            if (State != FishState.Roam || Species.lureChance <= 0f) return false;
            if (dist < Species.startleRadius * radiusMul) return false;   // 놀람 반경 안 → 이미 물러났다
            if (dist >= Species.NoticeRadius) return false;               // 인지 반경 밖 → 못 봤다
            if (dist < Species.minNoticeRadius) return false;
            return true;
        }

        // ── 이동 원시 함수 — §5-3 ────────────────────────────────
        /// <summary>각도를 목표로 보간하지 않는다. 초당 최대 선회율로 깎아 돌린다.</summary>
        float Steer(float targetRad, float dt, float omegaDeg)
        {
            float delta = Mathf.DeltaAngle(Heading * Mathf.Rad2Deg, targetRad * Mathf.Rad2Deg) * Mathf.Deg2Rad;
            float max = omegaDeg * Mathf.Deg2Rad * dt;
            float step = Mathf.Clamp(delta, -max, max);
            Heading += step;
            return step / Mathf.Max(dt, 1e-4f);
        }

        /// <summary>머리 방향으로만 전진한다.</summary>
        void Advance(float speed, float dt)
        {
            Pos += new Vector2(Mathf.Cos(Heading), Mathf.Sin(Heading)) * speed * dt;
        }

        Vector2 EvaluatePath()
        {
            switch (Species.pathType)
            {
                case PathType.Loop:
                    return PathEvaluator.Loop(_loopCenter, Species.loopA, Species.loopB,
                                              Species.loopPeriod, _loopPhase, _t);
                case PathType.HoverDash:
                    return _hoverPos;
                default:
                    if (_formationOffset.HasValue)
                    {
                        Fish lead = _session.FindLeader(Species);
                        if (lead != null)
                            return PathEvaluator.SchoolFollow(lead.Pos, lead.HeadingVector,
                                                              _formationOffset.Value, _noiseSeed, Time.time);
                        // 리더 소멸 → 현재 위치에서 단독 개체로 전환
                        _formationOffset = null; IsLeader = true; _t = 0f; _lane.origin = Pos;
                        return Pos;
                    }
                    return PathEvaluator.Lane(_lane, _t);
            }
        }

        public Vector2 HeadingVector => new Vector2(Mathf.Cos(Heading), Mathf.Sin(Heading));
        public PathEvaluator.LaneParams LaneParams => _lane;
        public float PathTime => _t;
    }
}
