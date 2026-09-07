using System;
using System.Collections.Generic;
using UnityEngine;

namespace Fishing.V2
{
    /// <summary>
    /// 물고기 한 마리의 시뮬레이션과 렌더 파라미터를 함께 보유한다.
    /// 외부 매니저를 찾아가지 않고, Tick에 전달된 데이터와 콜백만 사용한다.
    /// </summary>
    public sealed class FishAgentV2 : MonoBehaviour
    {
        private const float Tau = Mathf.PI * 2f;
        private const float DegreesToRadians = Mathf.PI / 180f;
        // Matches the HTML prototype's default UI.amp. Without this factor the Unity port
        // applies the species wave amplitude at 6.25x the reference bend, making tails and
        // squid arms look much longer even though their mesh lengths are identical.
        private const float PrototypeWaveAmplitude = 0.16f;

        private FishSpeciesConfig _species;
        private FishingV2TuningAsset _tuning;
        private FishingV2PresentationSettings _presentation;
        private Rect _pond;
        // 랩 경계 바깥 띠. 물고기의 절반 이상이 상시 연못 밖에 있으므로(게임플레이 화면
        // 자체가 연못보다 넓다) 이 대역은 랩 경계에 바짝 붙어야 한다. 조금만 안쪽으로
        // 당기면 정상적으로 보여야 할 물고기가 디더로 지워진다.
        // 축마다 여백이 다르므로 절대 거리가 아니라 "그 축 경계까지의 비율"로 잰다.
        // 0.90 = 경계의 90% 지점에서 지워지기 시작한다.
        private const float WrapFadeStart = 0.90f;
        private System.Random _random;
        private FishAgentV2 _leader;
        private Vector2 _formationOffset;
        private Vector2 _formationNoise;

        private Vector2 _position;
        private Vector2 _previousPosition;
        private float _heading;
        private float _turnRate;
        private float _speedNow;
        private float _sizeScale = 1f;
        private float _sizeCm;
        private float _mouthOffset;
        // Presentation-only depth. Movement remains a 2D simulation; this value controls
        // the actual render Z, depth tint, and projected shadow relationship.
        private float _visualDepth;

        private float _pathTime;
        private Vector2 _laneOrigin;
        private Vector2 _laneDirection;
        private float _lanePhase;
        private Vector2 _loopCenter;
        private float _loopPhase;
        private Vector2 _hoverPosition;
        private Vector2 _homePosition;
        private float _hoverRemaining;
        private float _hoverMax;
        private bool _isDashing;
        private float _dashElapsed;
        private float _dashDistance;
        private Vector2 _hoverAim;
        private bool _hasHoverAim;
        private HoverMood _mood;
        private int _moodRemaining;

        private Vector2 _startleOffset;
        private Vector2 _avoidTarget;
        private Vector2 _avoidOffset;
        private float _courseAngle;
        private bool _hasCourseAngle;
        private float _cooldown;
        private float _stateTimer;
        private float _curiosityTimer;
        private float _approachTimer;
        private float _orbitTimer;
        private float _breakOffTimer;
        private float _lingerDuration;
        private int _approachStage = -1;
        private ApproachStyle _approachStyle;

        // v25 feed signature state. The lure/approach rules stay local to the agent so
        // they remain deterministic and do not depend on a session singleton.
        private bool _feedInitialized;
        private float _feedCommitment;
        private float _feedDecisionTimer;
        private float _feedLastDistance = float.PositiveInfinity;
        private float _feedCompetition;
        private float _feedCarrySpeed;
        private bool _strikeReady;
        private float _rejectDuration;
        private float _rejectAim;
        private float _lureDelay = -1f;

        // v25 contest state. A rival is an object reference, never a species lookup.
        private FishAgentV2 _contestRival;
        private float _contestTimer;
        private float _contestDuration;
        private float _contestCooldown;
        private int _contestSide = 1;

        // v25 after-bite escape state. The profile is data-driven; these values are the
        // sampled per-contact runtime state so a school member can finish naturally without
        // rebuilding its formation slot or falling back to a generic linger.
        private float _afterBiteDuration;
        private float _afterBiteSpeed;
        private float _afterBiteAim;
        private float _afterBiteSide;
        private AfterBiteMode _afterBiteMode;

        // Roam Micro and propulsion cause the path speed and the visual beat together.
        private int _microMode;
        private float _microTimer;
        private float _microDuration;
        private float _microSpeed = 1f;
        private float _microDrive = 1f;
        private float _microSpeedTarget = 1f;
        private float _microDriveTarget = 1f;
        private float _propRate = 1f;
        private float _propDrive = 1f;

        // Loose-school spring state. The formation slot is a target, not the source of truth.
        private Vector2 _schoolTarget;
        private Vector2 _schoolPosition;
        private Vector2 _schoolVelocity;

        // Scheduled startle state. Position offsets are applied only when the event fires.
        private float _startleDelay = -1f;
        private float _startleCooldown;
        private float _startleBreakTimer;
        private float _startleStrength;
        private float _startleTimeMultiplier = 1f;
        private Vector2 _startleSource;
        private Vector2 _startleTargetOffset;
        private int _startleGeneration;
        private bool _startleEmit;

        private float _spiralSide;
        private float _spiralOffset;
        private int _spiralPass;
        private bool _spiralArmed;
        private float _spiralBrake;
        private float _spiralWobble;
        private float _hesitateHold;
        private float _hesitateSnap;
        private float _hesitateSide;

        private float _phase;
        private float _vSm;
        private float _turnSm;
        private float _vAvg = 1f;
        private float _vPrev;
        private float _jetCharge = 0.5f;
        private float _roll;
        private float _beat = 1f;
        private float _fin = 1f;
        private float _cStartRemaining;
        private float _cStartDuration = 0.34f;
        private float _cStartBend;
        private float _cStartDirection;
        private float _cStartAwayAngle;
        private float _armX;
        private float _armV;
        private float _armTuck = 1f;
        private float _armAmbient = 1f;
        private float _armFlow;
        private float _driftPhase;
        private float _delayedTurn;
        private float _seed;
        private float _turnPrep;
        private float _turnPrepTarget;

        private MeshFilter _meshFilter;
        private MeshRenderer _meshRenderer;
        private MeshRenderer _shadowRenderer;
        private Transform _shadowTransform;
        private MeshRenderer _shadowSoftRenderer;
        private Transform _shadowSoftTransform;
        private MaterialPropertyBlock _propertyBlock;
        private MaterialPropertyBlock _shadowPropertyBlock;
        private MaterialPropertyBlock _shadowSoftPropertyBlock;
        private bool _initialized;

        private enum HoverMood
        {
            Still,
            Cruise,
            Skittish
        }

        private struct ApproachResult
        {
            public float Aim;
            public float Speed;
            public float RadiusBodyLengths;

            public ApproachResult(float aim, float speed, float radiusBodyLengths)
            {
                Aim = aim;
                Speed = speed;
                RadiusBodyLengths = radiusBodyLengths;
            }
        }

        public FishSpeciesConfig Species { get { return _species; } }
        public FishState State { get; private set; } = FishState.Roam;
        public Vector2 Position { get { return _position; } }
        public Vector2 PreviousPosition { get { return _previousPosition; } }
        public float HeadingRadians { get { return _heading; } }
        public Vector2 HeadingVector { get { return new Vector2(Mathf.Cos(_heading), Mathf.Sin(_heading)); } }
        public float TurnRateNow { get { return _turnRate; } }
        public float SpeedNow { get { return _speedNow; } }
        public float SizeScale { get { return _sizeScale; } }
        public float SizeCm { get { return _sizeCm; } }
        public float MouthOffset { get { return _mouthOffset * _sizeScale; } }
        public Vector2 MouthPosition
        {
            get
            {
                Vector2 heading = HeadingVector;
                return _position + heading * MouthOffset;
            }
        }
        public AfterBiteMode CurrentAfterBiteMode { get { return _afterBiteMode; } }
        public float AfterBiteProgress01
        {
            get { return _afterBiteDuration > 0f ? Mathf.Clamp01(_stateTimer / _afterBiteDuration) : 0f; }
        }
        /// <summary>
        /// Returns whether the fish's mouth swept through the bobber during the current
        /// simulation step. The root can pass beside the bobber while the authored mouth
        /// (or a rear-biased mouth offset such as squid's) makes the actual contact.
        /// </summary>
        public bool TryGetMouthContact(Vector2 bobberPosition, float radius, out Vector2 contact)
        {
            return FishingV2CatchMath.TrySweptMouthContact(
                _previousPosition,
                _position,
                _heading,
                MouthOffset,
                bobberPosition,
                Mathf.Max(0.001f, radius),
                out contact);
        }
        public float VisualDepth01 { get { return _visualDepth; } }
        public float WorldDepthZ { get { return transform.position.z; } }
        public bool IsCaught { get { return State == FishState.Caught; } }
        public bool IsHooked { get { return State == FishState.Bite || State == FishState.Hooked; } }
        public bool IsFollower { get { return _leader != null; } }
        public FishAgentV2 Leader { get { return _leader; } }
        public Mesh SharedMesh { get { return _meshFilter != null ? _meshFilter.sharedMesh : null; } }
        public Material SharedMaterial { get { return _meshRenderer != null ? _meshRenderer.sharedMaterial : null; } }

        public void Initialize(
            FishSpeciesConfig species,
            FishingV2TuningAsset tuning,
            Rect pond,
            System.Random random,
            Mesh mesh,
            Material fishMaterial,
            Material shadowMaterial,
            FishAgentV2 leader,
            Vector2 formationOffset,
            FishingV2PresentationSettings presentation)
        {
            if (species != null && species.DataVersion < FishingV2Catalog.CurrentDataVersion)
            {
                // Existing serialized assets predate the v25 fields. Normalize them at the
                // runtime boundary so an old Spot asset cannot silently run generic defaults.
                FishingV2Catalog.ApplyV25Defaults(species);
            }

            _species = species;
            _tuning = tuning;
            _presentation = presentation;
            _pond = pond;
            _random = random ?? new System.Random(1);
            _leader = leader;
            _formationOffset = formationOffset;
            _formationNoise = new Vector2(RandomRange(0f, Tau), RandomRange(0f, Tau));
            _sizeScale = RandomRange(0.92f, 1.08f);
            _sizeCm = SampleNormal(species.SizeCm.Min, species.SizeCm.Max);
            _heading = RandomRange(0f, Tau);
            _phase = RandomRange(0f, Tau);
            _seed = RandomRange(0f, 1f);
            _curiosityTimer = RandomRange(0f, Mathf.Max(0.1f, tuning.CuriosityTick));
            _visualDepth = ResolveVisualDepth();
            _feedInitialized = false;
            _feedCommitment = 0.5f;
            _feedDecisionTimer = 0f;
            _feedLastDistance = float.PositiveInfinity;
            _feedCompetition = 0f;
            _feedCarrySpeed = 0f;
            _strikeReady = false;
            _rejectDuration = 0f;
            _lureDelay = -1f;
            _contestRival = null;
            _contestTimer = 0f;
            _contestDuration = 0f;
            _contestCooldown = RandomRange(0.2f, 0.8f);
            _afterBiteDuration = 0f;
            _afterBiteSpeed = 0f;
            _afterBiteAim = 0f;
            _afterBiteSide = 1f;
            _afterBiteMode = AfterBiteMode.Peel;
            _microMode = -1;
            _microTimer = 0f;
            _microDuration = 0f;
            _microSpeed = 1f;
            _microDrive = 1f;
            _microSpeedTarget = 1f;
            _microDriveTarget = 1f;
            _propRate = 1f;
            _propDrive = 1f;
            _schoolPosition = Vector2.zero;
            _schoolTarget = Vector2.zero;
            _schoolVelocity = Vector2.zero;
            _startleDelay = -1f;
            _startleCooldown = 0f;
            _startleBreakTimer = 0f;
            _startleStrength = 0f;
            _startleTimeMultiplier = 1f;
            _startleSource = Vector2.zero;
            _startleTargetOffset = Vector2.zero;
            _startleGeneration = 0;
            _startleEmit = false;
            _armFlow = 0f;
            _driftPhase = RandomRange(0f, Tau);
            _turnPrep = 0f;
            _turnPrepTarget = 0f;

            SetupPath();
            _previousPosition = _position;
            _schoolPosition = _position;
            _schoolTarget = _position;

            _meshFilter = gameObject.GetComponent<MeshFilter>();
            if (_meshFilter == null) _meshFilter = gameObject.AddComponent<MeshFilter>();
            _meshRenderer = gameObject.GetComponent<MeshRenderer>();
            if (_meshRenderer == null) _meshRenderer = gameObject.AddComponent<MeshRenderer>();
            _meshFilter.sharedMesh = mesh != null ? mesh : FishMeshBuilderV2.BuildFallback("FishV2_Fallback");
            _mouthOffset = FishMeshBuilderV2.GetMouthOffset(species, _meshFilter.sharedMesh);
            _meshRenderer.sharedMaterial = fishMaterial;
            gameObject.name = "FishV2_" + species.SpeciesId;
            transform.localScale = Vector3.one * _sizeScale;

            GameObject shadow = new GameObject("Shadow");
            shadow.transform.SetParent(transform, false);
            _shadowTransform = shadow.transform;
            _shadowTransform.localScale = new Vector3(1.02f, 1.02f, 0.001f);
            MeshFilter shadowFilter = shadow.AddComponent<MeshFilter>();
            shadowFilter.sharedMesh = _meshFilter.sharedMesh;
            _shadowRenderer = shadow.AddComponent<MeshRenderer>();
            _shadowRenderer.sharedMaterial = shadowMaterial;

            GameObject softShadow = new GameObject("ShadowSoft");
            softShadow.transform.SetParent(transform, false);
            _shadowSoftTransform = softShadow.transform;
            _shadowSoftTransform.localScale = new Vector3(1.08f, 1.08f, 0.001f);
            MeshFilter softShadowFilter = softShadow.AddComponent<MeshFilter>();
            softShadowFilter.sharedMesh = _meshFilter.sharedMesh;
            _shadowSoftRenderer = softShadow.AddComponent<MeshRenderer>();
            _shadowSoftRenderer.sharedMaterial = shadowMaterial;

            _propertyBlock = new MaterialPropertyBlock();
            _shadowPropertyBlock = new MaterialPropertyBlock();
            _shadowSoftPropertyBlock = new MaterialPropertyBlock();
            _initialized = true;
            ApplyVisual(0f, 0f);
        }

        /// <summary>
        /// 랩 경계 근처에서 물고기를 지운다.
        ///
        /// 랩 자체는 설계상 필요하다(§11-1). 문제는 x 랩 경계가 연못 가장자리에서 1.8, 즉
        /// ±9.8인데 게임플레이 화면 폭이 정확히 ±9.8이라는 것이다 — 카메라를 조금만 빼도
        /// 순간이동이 그대로 보인다. 경계를 밀거나 연못을 넓히면 물고기 밀도와 조우율이
        /// 같이 바뀌므로, 이동 규칙은 그대로 두고 보이지만 않게 한다.
        /// </summary>
        private float EvaluateWrapEdgeFade()
        {
            Vector2 position = new Vector2(transform.position.x, transform.position.y);
            float outsideX = Mathf.Abs(position.x - _pond.center.x) - _pond.width * 0.5f;
            float outsideY = Mathf.Abs(position.y - _pond.center.y) - _pond.height * 0.5f;
            float outside = Mathf.Max(0f, Mathf.Max(
                outsideX / PathEvaluatorV2.WrapReentryMarginX,
                outsideY / PathEvaluatorV2.WrapReentryMarginY));
            // Mathf.SmoothStep(a, b, t)는 HLSL의 smoothstep(edge0, edge1, x)이 아니다 —
            // t를 0..1 보간 계수로 보고 a와 b 사이 값을 돌려준다. 여기서 필요한 것은
            // 경계 사이의 정규화라 InverseLerp를 먼저 거쳐야 한다.
            float t = Mathf.InverseLerp(WrapFadeStart, 1f, outside);
            return 1f - t * t * (3f - 2f * t);
        }

        public void Tick(
            float dt,
            float now,
            BobberV2 bobber,
            IReadOnlyList<FishAgentV2> allFish,
            Action<FishAgentV2> onReachedBobber)
        {
            if (!_initialized || IsCaught)
            {
                return;
            }

            dt = _tuning.ClampDelta(dt);
            _previousPosition = _position;
            _turnRate = 0f;

            // Unity's destroyed-object equality can make a follower's cached leader look
            // null while the managed reference is still present. Detach before the next
            // movement solve so a caught leader cannot leave the follower on stale formation
            // state and snap to an unrelated path origin.
            if (!ReferenceEquals(_leader, null) && _leader == null)
            {
                BecomeIndependent();
            }

            _cooldown = Mathf.Max(0f, _cooldown - dt);
            _contestCooldown = Mathf.Max(0f, _contestCooldown - dt);
            _startleCooldown = Mathf.Max(0f, _startleCooldown - dt);

            TickScheduledStartle(dt, allFish);
            if (_lureDelay >= 0f)
            {
                if (bobber == null || !bobber.IsInWater || State != FishState.Roam)
                {
                    _lureDelay = -1f;
                }
                else
                {
                    _lureDelay -= dt;
                    if (_lureDelay <= 0f)
                    {
                        _lureDelay = -1f;
                        BecomeInterested();
                    }
                }
            }

            switch (State)
            {
                case FishState.Roam:
                case FishState.Startle:
                case FishState.Wary:
                    TickRoaming(dt, now, bobber, allFish);
                    break;
                case FishState.Notice:
                    TickNotice(dt, bobber);
                    break;
                case FishState.Interested:
                    TickInterested(dt, now, bobber, allFish, onReachedBobber);
                    break;
                case FishState.Strike:
                    TickStrike(dt, now, bobber, allFish, onReachedBobber);
                    break;
                case FishState.Reject:
                    TickReject(dt);
                    break;
                case FishState.Yield:
                case FishState.Steal:
                case FishState.Chase:
                    TickContest(dt, now, bobber, allFish);
                    break;
                case FishState.AfterBite:
                    TickAfterBite(dt, now, allFish);
                    break;
                case FishState.Bite:
                case FishState.Hooked:
                    TickBite(dt, bobber);
                    break;
                case FishState.Linger:
                    TickLinger(dt, now);
                    break;
            }

            TickCStart(dt);
            UpdateMotionTelemetry(dt);
            UpdateDepth(dt);
            ApplyVisual(dt, now);
        }

        public bool CanBeLured(Vector2 bobberPosition, float radiusMultiplier, out float distance)
        {
            distance = Vector2.Distance(_position, bobberPosition);
            if (State != FishState.Roam && State != FishState.Wary)
            {
                return false;
            }

            float startleRadius = _species.StartleRadius * radiusMultiplier;
            return distance >= startleRadius &&
                   distance < _species.NoticeRadius &&
                   distance >= _species.MinNoticeRadius &&
                   _species.LureChance > 0f;
        }

        public bool Roll(float probability)
        {
            return _random.NextDouble() < Mathf.Clamp01(probability);
        }

        public void BecomeInterested()
        {
            if (IsCaught || State == FishState.Bite || State == FishState.Hooked)
            {
                return;
            }

            State = FishState.Notice;
            _stateTimer = 0f;
            _approachTimer = 0f;
            _approachStage = -1;
            _approachStyle = ApproachStyle.Direct;
            _orbitTimer = 0f;
            _breakOffTimer = 0f;
            _feedInitialized = false;
            _feedCommitment = 0.5f;
            _feedDecisionTimer = 0f;
            _feedLastDistance = float.PositiveInfinity;
            _feedCompetition = 0f;
            _feedCarrySpeed = 0f;
            _strikeReady = false;
            _contestRival = null;
            _contestTimer = 0f;
            _contestDuration = 0f;
            _lureDelay = -1f;
        }

        public void EnterBite(Vector2 bitePosition)
        {
            if (IsCaught)
            {
                return;
            }

            State = FishState.Hooked;
            _stateTimer = 0f;
            _approachTimer = 0f;
            _bitePosition = bitePosition;
            _biteHeading = _heading;
            _strikeReady = false;
            _feedInitialized = false;
        }

        /// <summary>
        /// Starts the v25 post-contact escape used by released fish and presentation-only
        /// catch paths. The sampled aim/speed remains on the agent for the duration so the
        /// behavior is deterministic and can preserve an existing school slot on finish.
        /// </summary>
        public void BeginAfterBite(Vector2 bobberPosition)
        {
            if (IsCaught)
            {
                return;
            }

            AfterBiteProfile profile = _species != null && _species.AfterBite != null
                ? _species.AfterBite
                : new AfterBiteProfile();
            Vector2 away = _position - bobberPosition;
            if (away.sqrMagnitude < 0.0001f)
            {
                away = -HeadingVector;
            }

            float awayAngle = Mathf.Atan2(away.y, away.x);
            _afterBiteMode = profile.Mode;
            _afterBiteDuration = Mathf.Max(0.10f, RandomRange(profile.Duration.Min, profile.Duration.Max));
            _afterBiteSide = _random.NextDouble() < 0.5 ? -1f : 1f;
            _afterBiteAim = _heading + WrapAngle(awayAngle - _heading) * Mathf.Clamp01(profile.Away) +
                _afterBiteSide * profile.Arc;
            _afterBiteSpeed = Mathf.Max(
                _species != null ? _species.ApproachSpeed * Mathf.Max(0f, profile.SpeedK) : 0.5f,
                _vSm * Mathf.Clamp01(profile.Carry));

            State = FishState.AfterBite;
            _stateTimer = 0f;
            _approachTimer = 0f;
            _approachStage = -1;
            _breakOffTimer = 0f;
            _feedInitialized = false;
            _feedCarrySpeed = 0f;
            _strikeReady = false;
            _contestRival = null;
            _contestTimer = 0f;
            _contestDuration = 0f;
        }

        public void Release(float cooldown, float radiusMultiplier, float timeMultiplier)
        {
            ReturnToRoam(cooldown);
            ApplyStartle(_position - HeadingVector * 0.01f, radiusMultiplier, timeMultiplier, false);
        }

        public void ReturnToRoam(float cooldown)
        {
            if (IsCaught)
            {
                return;
            }

            State = FishState.Roam;
            _cooldown = Mathf.Max(_cooldown, cooldown);
            _stateTimer = 0f;
            _approachTimer = 0f;
            _approachStage = -1;
            _orbitTimer = 0f;
            _breakOffTimer = 0f;
            _startleOffset = Vector2.zero;
            _startleDelay = -1f;
            _startleBreakTimer = 0f;
            _startleTimeMultiplier = 1f;
            _startleEmit = false;
            _startleTargetOffset = Vector2.zero;
            _contestRival = null;
            _contestTimer = 0f;
            _contestDuration = 0f;
            _afterBiteDuration = 0f;
            _afterBiteSpeed = 0f;
            _afterBiteAim = 0f;
            _afterBiteSide = 1f;
            _feedInitialized = false;
            _feedCommitment = 0.5f;
            _feedDecisionTimer = 0f;
            _feedLastDistance = float.PositiveInfinity;
            _feedCompetition = 0f;
            _feedCarrySpeed = 0f;
            _strikeReady = false;
            _lureDelay = -1f;
            _schoolVelocity = Vector2.zero;
            _schoolPosition = _position;
            _schoolTarget = _position;
            ReanchorAtCurrentPosition();
        }

        public void ApplyStartle(Vector2 source, float radiusMultiplier, float timeMultiplier, bool resetInterest)
        {
            if (IsCaught || State == FishState.Bite || State == FishState.Hooked)
            {
                return;
            }

            Vector2 away = _position - source;
            float distance = away.magnitude;
            float radius = _species.StartleRadius * Mathf.Max(0.01f, radiusMultiplier);
            if (distance <= 0.001f || distance >= radius)
            {
                return;
            }

            if (resetInterest && (State == FishState.Notice || State == FishState.Interested || State == FishState.Linger))
            {
                ReturnToRoam(1.2f);
            }

            away /= distance;
            float strength = (1f - distance / radius) * _species.StartleRadius * _tuning.StartleImpulse;
            float durationMultiplier = Mathf.Max(0.1f, timeMultiplier);
            ScheduleStartle(_position - away * distance, strength, 0f, 0, durationMultiplier);
        }

        public void MarkCaught()
        {
            State = FishState.Caught;
            if (_meshRenderer != null) _meshRenderer.enabled = false;
            if (_shadowRenderer != null) _shadowRenderer.enabled = false;
            if (_shadowSoftRenderer != null) _shadowSoftRenderer.enabled = false;
        }

        public void SetLeader(FishAgentV2 leader, Vector2 formationOffset)
        {
            _leader = leader;
            _formationOffset = formationOffset;
        }

        public void BecomeIndependent()
        {
            _leader = null;
            _formationOffset = Vector2.zero;
            ReanchorAtCurrentPosition();
        }

        public bool SharesSchoolWith(FishAgentV2 other)
        {
            if (other == null)
            {
                return false;
            }

            if (_leader == null && other._leader == null)
            {
                return false;
            }

            return (_leader != null && (_leader == other || _leader == other._leader)) ||
                   (other._leader != null && other._leader == this);
        }

        private Vector2 _bitePosition;
        private float _biteHeading;

        private void SetupPath()
        {
            _pathTime = 0f;
            FishVisualSpec visual = _species.Visual;

            if (_species.PathType == FishPathType.Lane)
            {
                float angle = RandomRange(-0.35f, 0.35f);
                float directionSign = _random.NextDouble() < 0.5 ? -1f : 1f;
                _laneDirection = new Vector2(Mathf.Cos(angle) * directionSign, Mathf.Sin(angle) * directionSign).normalized;
                _laneOrigin = new Vector2(
                    RandomRange(_pond.xMin + 1f, _pond.xMax - 1f),
                    RandomRange(_pond.yMin + _species.Zone.x * _pond.height, _pond.yMin + _species.Zone.y * _pond.height));
                _lanePhase = RandomRange(0f, Tau);
                _position = EvaluateLane(0f);
            }
            else if (_species.PathType == FishPathType.Loop)
            {
                float minX = _pond.xMin + _species.Loop.A * 0.55f + 0.6f;
                float maxX = _pond.xMax - _species.Loop.A * 0.55f - 0.6f;
                float minY = _pond.yMin + _species.Loop.B * 0.6f + 0.4f;
                float maxY = _pond.yMax - _species.Loop.B * 0.6f - 0.4f;
                _loopCenter = new Vector2(
                    maxX > minX ? RandomRange(minX, maxX) : _pond.center.x,
                    Mathf.Clamp(RandomRange(_pond.yMin + _species.Zone.x * _pond.height, _pond.yMin + _species.Zone.y * _pond.height), minY, maxY));
                _loopPhase = RandomRange(0f, Tau);
                _position = EvaluateLoop(0f);
            }
            else
            {
                _hoverPosition = new Vector2(
                    RandomRange(_pond.xMin + 2.5f, _pond.xMax - 2.5f),
                    Mathf.Clamp(RandomRange(_pond.yMin + _species.Zone.x * _pond.height, _pond.yMin + _species.Zone.y * _pond.height), _pond.yMin + 1.2f, _pond.yMax - 1.2f));
                _homePosition = _hoverPosition;
                _hoverRemaining = RandomRange(_species.HoverDash.HoverTime.Min, _species.HoverDash.HoverTime.Max);
                _hoverMax = _hoverRemaining;
                _isDashing = false;
                _hasHoverAim = false;
                _position = _hoverPosition;
            }

            // visual을 참조해 컴파일러가 잘못된 null 최적화를 하지 않도록 하지 않는다.
            if (visual == null)
            {
                _position = _pond.center;
            }
        }

        private float ResolveVisualDepth()
        {
            if (_leader != null && !_leader.IsCaught)
            {
                return Mathf.Clamp01(_leader._visualDepth + RandomRange(-0.035f, 0.035f));
            }

            FloatRange configured = _species != null ? _species.VisualDepth : new FloatRange(-1f, -1f);
            if (configured.Min >= 0f && configured.Max > configured.Min)
            {
                return Mathf.Clamp01(RandomRange(configured.Min, configured.Max));
            }

            // Legacy data has no presentation depth. Keep the result deterministic and
            // visually varied by deriving a shallow-to-deep bias from its existing zone.
            float zoneCenter = _species != null ? Mathf.Clamp01((_species.Zone.x + _species.Zone.y) * 0.5f) : 0.5f;
            return Mathf.Clamp01(Mathf.Lerp(0.10f, 0.84f, zoneCenter) + RandomRange(-0.10f, 0.10f));
        }

        private void TickRoaming(float dt, float now, BobberV2 bobber, IReadOnlyList<FishAgentV2> allFish)
        {
            Vector2 basePosition;
            bool follower = _leader != null && !_leader.IsCaught;

            if (State == FishState.Roam || State == FishState.Wary)
            {
                UpdateRoamMicro(dt);
            }
            UpdateStartleOffset(dt);

            if (follower)
            {
                float cos = Mathf.Cos(_leader._heading);
                float sin = Mathf.Sin(_leader._heading);
                Vector2 rotatedOffset = new Vector2(
                    _formationOffset.x * cos - _formationOffset.y * sin,
                    _formationOffset.x * sin + _formationOffset.y * cos);
                _schoolTarget = _leader._position + rotatedOffset + new Vector2(
                    Mathf.Sin(now * 0.7f + _formationNoise.x) * 0.12f,
                    Mathf.Sin(now * 0.9f + _formationNoise.y) * 0.12f);

                if (_startleBreakTimer > 0f)
                {
                    // _position already contains the previous frame's startle offset. Remove
                    // it before rebuilding the pose so the offset is not added repeatedly
                    // while the agent is held in its C-start break.
                    basePosition = _position - _avoidOffset - _startleOffset;
                    _schoolVelocity = Vector2.zero;
                }
                else
                {
                    // Spring-damper following keeps the slot readable without freezing every
                    // follower to the leader's transform.
                    float spring = 7.5f;
                    float damping = 4.2f;
                    _schoolVelocity += (_schoolTarget - _schoolPosition) * spring * dt;
                    _schoolVelocity *= Mathf.Clamp01(1f - damping * dt);
                    _schoolPosition += _schoolVelocity * dt;
                    basePosition = _schoolPosition;
                }
            }
            else
            {
                basePosition = _startleBreakTimer > 0f
                    ? _position - _avoidOffset - _startleOffset
                    : TickPath(dt, now);
            }

            basePosition += ComputeSchoolSeparation(basePosition, allFish);
            UpdateAvoidance(basePosition, allFish, dt);
            _position = basePosition + _avoidOffset + _startleOffset;
            ResolveHardOverlap(allFish);

            float breakTimerBefore = _startleBreakTimer;
            _startleBreakTimer = Mathf.Max(0f, _startleBreakTimer - dt);
            if (breakTimerBefore > 0f && _startleBreakTimer <= 0f)
            {
                // Resume the owned path from the actual post-break position. Otherwise a
                // Lane/Loop/ Hover-Dash agent resumes from stale path state and snaps back
                // several body lengths when the C-start hold ends.
                Vector2 resumePosition = _position - _startleOffset;
                _startleTargetOffset = Vector2.zero;
                ReanchorAtPosition(resumePosition);
                _previousPosition = _position;
            }

            if (!follower && _species.PathType == FishPathType.Lane)
            {
                WrapLaneIfNeeded(allFish);
            }

            UpdateTurnPreparation(dt, follower);

            // 경로가 위치를 계산하더라도 머리 방향은 실제 진행 방향을 제한 선회로 따라가야 한다.
            // 방향을 위치에 즉시 대입하면 사행·랩 프레임에서 도리도리와 순간 반전이 생긴다.
            if (_species.PathType != FishPathType.HoverDash)
            {
                Vector2 movement = _position - _previousPosition;
                float movementSpeed = movement.magnitude / Mathf.Max(dt, 0.0001f);
                if (movementSpeed > 0.02f)
                {
                    float direction = follower ? _leader._heading : Mathf.Atan2(movement.y, movement.x);
                    if (!_hasCourseAngle) _courseAngle = direction;
                    _courseAngle = AngleLerp(_courseAngle, direction, Mathf.Min(1f, dt * _tuning.HeadCourseSmoothing));
                    float target = follower
                        ? _leader._heading
                        : _courseAngle + WrapAngle(direction - _courseAngle) * _tuning.HeadTrack;
                    if (!follower)
                    {
                        float slip = WrapAngle(target - direction);
                        float limit = _tuning.SlipMaxDeg * DegreesToRadians;
                        if (Mathf.Abs(slip) > limit) target = direction + Mathf.Sign(slip) * limit;
                    }

                    _turnRate = SteerAngle(target, movementSpeed, 1f, dt);
                }
            }

            if (State == FishState.Startle)
            {
                _stateTimer -= dt;
                if (_stateTimer <= 0f)
                {
                    State = FishState.Wary;
                    _stateTimer = 0.8f;
                }
            }
            else if (State == FishState.Wary)
            {
                _stateTimer -= dt;
                if (_stateTimer <= 0f)
                {
                    State = FishState.Roam;
                }
            }

            if ((State == FishState.Roam || State == FishState.Wary) &&
                bobber != null && bobber.IsInWater && _cooldown <= 0f)
            {
                _curiosityTimer -= dt;
                if (_curiosityTimer <= 0f)
                {
                    _curiosityTimer = Mathf.Max(0.1f, _tuning.CuriosityTick);
                    float distance = Vector2.Distance(_position, bobber.Position);
                    if (distance < _species.NoticeRadius && distance >= _species.MinNoticeRadius &&
                        Roll(_species.CuriosityPerSecond))
                    {
                        BecomeInterested();
                        NotifySchoolLure(allFish);
                    }
                }
            }
        }

        private Vector2 TickPath(float dt, float now)
        {
            float pathDt = dt * Mathf.Clamp(_microSpeed, 0.20f, 1.60f);
            if (_species.PathType == FishPathType.Lane)
            {
                _pathTime += pathDt;
                return EvaluateLane(_pathTime);
            }

            if (_species.PathType == FishPathType.Loop)
            {
                float drift = _species.Loop.DriftSpeed;
                _loopCenter += new Vector2(
                    Mathf.Sin(now * 0.13f + _loopPhase) * drift * pathDt,
                    Mathf.Cos(now * 0.11f + _loopPhase * 1.7f) * drift * 0.55f * pathDt);
                _loopCenter.x = Mathf.Clamp(_loopCenter.x, _pond.xMin - _species.Loop.A * 0.55f, _pond.xMax + _species.Loop.A * 0.55f);
                _loopCenter.y = Mathf.Clamp(_loopCenter.y, _pond.yMin - _species.Loop.B * 0.30f, _pond.yMax + _species.Loop.B * 0.30f);
                _pathTime += pathDt;
                return EvaluateLoop(_pathTime);
            }

            _position = _hoverPosition;
            TickHover(dt, now);
            _hoverPosition = _position;
            return _position;
        }

        private Vector2 EvaluateLane(float time)
        {
            return PathEvaluatorV2.EvaluateLane(
                new PathEvaluatorV2.LaneState
                {
                    Origin = _laneOrigin,
                    Direction = _laneDirection,
                    Phase = _lanePhase,
                    Time = time
                },
                _species.Lane,
                time);
        }

        private Vector2 EvaluateLoop(float time)
        {
            return PathEvaluatorV2.EvaluateLoop(
                new PathEvaluatorV2.LoopState
                {
                    Center = _loopCenter,
                    Phase = _loopPhase,
                    Time = time
                },
                _species.Loop,
                time);
        }

        private void TickHover(float dt, float now)
        {
            if (_moodRemaining <= 0)
            {
                PickMood();
            }

            if (_isDashing)
            {
                _dashElapsed += dt;
                float u = Mathf.Clamp01(_dashElapsed / Mathf.Max(0.01f, _species.HoverDash.DashDuration));
                float speed = _dashDistance * 12f * u * Mathf.Pow(1f - u, 2f) / Mathf.Max(0.01f, _species.HoverDash.DashDuration);
                if (_hasHoverAim)
                {
                    _turnRate = SteerTowards(_hoverAim, speed, _species.HoverDash.MinTurnRadiusBodyLengths, dt);
                }
                MoveForward(speed, dt);

                if (u >= 1f)
                {
                    _isDashing = false;
                    _hasHoverAim = false;
                    _moodRemaining--;
                    _hoverRemaining = RandomRange(_species.HoverDash.HoverTime.Min, _species.HoverDash.HoverTime.Max) * MoodHoverMultiplier();
                    _hoverMax = _hoverRemaining;
                }
            }
            else
            {
                _hoverRemaining -= dt;
                if (!_hasHoverAim)
                {
                    Vector2 fromHome = _position - _homePosition;
                    _hoverAim = fromHome.magnitude > _species.HoverDash.HomeRadius * 0.7f
                        ? _homePosition
                        : _position + AngleVector(RandomRange(-80f, 80f) * DegreesToRadians) * 2f;
                    _hasHoverAim = true;
                }

                _turnRate = SteerTowards(_hoverAim, _species.HoverDash.DriftSpeed, _species.HoverDash.MinTurnRadiusBodyLengths, dt);
                MoveForward(_species.HoverDash.DriftSpeed, dt);
                if (_hoverRemaining <= 0f)
                {
                    _isDashing = true;
                    _dashElapsed = 0f;
                    _dashDistance = RandomRange(_species.HoverDash.DashDistance.Min, _species.HoverDash.DashDistance.Max) * MoodDashMultiplier();
                }
            }

            _position.x = Mathf.Clamp(_position.x, _pond.xMin + 0.6f, _pond.xMax - 0.6f);
            _position.y = Mathf.Clamp(_position.y, _pond.yMin + 0.6f, _pond.yMax - 0.6f);
        }

        private void PickMood()
        {
            float roll = RandomRange(0f, 1f);
            if (roll < 0.30f)
            {
                _mood = HoverMood.Still;
                _moodRemaining = RandomInt(1, 2);
            }
            else if (roll < 0.72f)
            {
                _mood = HoverMood.Cruise;
                _moodRemaining = RandomInt(2, 3);
            }
            else
            {
                _mood = HoverMood.Skittish;
                _moodRemaining = RandomInt(3, 6);
            }
        }

        private float MoodHoverMultiplier()
        {
            switch (_mood)
            {
                case HoverMood.Still: return 2.4f;
                case HoverMood.Skittish: return 0.26f;
                default: return 1f;
            }
        }

        private float MoodDashMultiplier()
        {
            switch (_mood)
            {
                case HoverMood.Still: return 0.72f;
                case HoverMood.Skittish: return 0.62f;
                default: return 1f;
            }
        }

        private void TickNotice(float dt, BobberV2 bobber)
        {
            if (bobber == null || !bobber.IsInWater)
            {
                ReturnToRoam(2f);
                return;
            }

            FeedSignature feed = GetFeedSignature();
            _stateTimer += dt;
            Vector2 toBobber = bobber.Position - _position;
            float distance = toBobber.magnitude;
            _feedCarrySpeed = Mathf.Max(0f, _speedNow) * Mathf.Clamp01(feed.Carry);
            float noticeSpeed = _species.ApproachSpeed * 0.18f;
            float retainedSpeed = _feedCarrySpeed * (1f - Mathf.Clamp01(feed.NoticeK));
            float speed = Mathf.Max(noticeSpeed, retainedSpeed + noticeSpeed * Mathf.Clamp01(feed.NoticeK));
            _turnRate = SteerTowards(bobber.Position, speed, 0.9f, dt);
            MoveForward(speed, dt);

            if (_stateTimer >= Mathf.Max(0.01f, feed.NoticeT))
            {
                State = FishState.Interested;
                _stateTimer = 0f;
                _approachTimer = 0f;
                _approachStage = -1;
                _feedInitialized = true;
                _feedCommitment = RandomRange(feed.Commit.Min, feed.Commit.Max);
                _feedDecisionTimer = RandomRange(feed.Reconsider.Min, feed.Reconsider.Max);
                _feedLastDistance = distance;
                _feedCompetition = 0f;
                _strikeReady = false;
            }
        }

        private void TickInterested(
            float dt,
            float now,
            BobberV2 bobber,
            IReadOnlyList<FishAgentV2> allFish,
            Action<FishAgentV2> onReachedBobber)
        {
            if (bobber == null || !bobber.IsInWater)
            {
                BeginLinger();
                return;
            }

            FeedSignature feed = GetFeedSignature();
            if (!_feedInitialized)
            {
                _feedInitialized = true;
                _feedCommitment = RandomRange(feed.Commit.Min, feed.Commit.Max);
                _feedDecisionTimer = RandomRange(feed.Reconsider.Min, feed.Reconsider.Max);
                _feedLastDistance = Vector2.Distance(_position, bobber.Position);
            }

            _stateTimer += dt;
            _approachTimer += dt;
            float distance = Vector2.Distance(_position, bobber.Position);

            if (_contestCooldown <= 0f && TryBeginContest(bobber, allFish, distance))
            {
                return;
            }

            float progress = _feedLastDistance - distance;
            _feedLastDistance = distance;
            _feedDecisionTimer -= dt;
            float strikeDistance = Mathf.Max(
                feed.Arrival * 1.55f,
                Mathf.Max(0.05f, _species.Visual.Length) * feed.StrikeBodyLengths);
            if (_feedDecisionTimer <= 0f && distance > strikeDistance)
            {
                float close = Mathf.Clamp01(1f - distance / Mathf.Max(0.5f, _species.NoticeRadius));
                float stalled = progress < 0.012f ? 1f : 0f;
                _feedCompetition = Mathf.Lerp(_feedCompetition, ComputeLureCompetition(bobber, allFish, distance), Mathf.Min(1f, dt * 5f));
                float social = _feedCompetition * feed.Competition;
                float delta = close * 0.10f + social * 0.16f - feed.Doubt * (0.10f + stalled * 0.10f) + RandomRange(-0.035f, 0.035f);
                _feedCommitment = Mathf.Clamp01(_feedCommitment + delta);
                _feedDecisionTimer = RandomRange(feed.Reconsider.Min, feed.Reconsider.Max);
                if (_feedCommitment < feed.QuitBelow)
                {
                    BeginReject(bobber.Position, feed);
                    return;
                }
            }

            _strikeReady = distance <= strikeDistance && _feedCommitment >= feed.StrikeMinimum;
            if (_strikeReady)
            {
                // Keep the strike as an observable first-class state. The next fixed step
                // owns the fast approach and mouth-contact callback, which prevents a root
                // distance threshold from swallowing the Strike transition.
                State = FishState.Strike;
                _stateTimer = 0f;
                return;
            }

            int stage = GetApproachStage(distance);
            ApproachStyle style = _species.ApproachPlan != null && _species.ApproachPlan.Length > 0
                ? _species.ApproachPlan[Mathf.Clamp(stage, 0, _species.ApproachPlan.Length - 1)].Style
                : ApproachStyle.Direct;
            if (stage != _approachStage || style != _approachStyle)
            {
                _approachStage = stage;
                _approachStyle = style;
                EnterApproachStyle(style, bobber.Position, distance);
            }

            ApproachResult result = _strikeReady
                ? new ApproachResult(
                    Mathf.Atan2(bobber.Position.y - _position.y, bobber.Position.x - _position.x),
                    _species.ApproachSpeed * feed.StrikeSpeed,
                    0.58f)
                : StepApproach(style, dt, now, bobber.Position, distance);
            float desiredSpeed = result.Speed * Mathf.Max(0.10f, feed.ApproachK);
            desiredSpeed = Mathf.Max(desiredSpeed, _feedCarrySpeed * Mathf.Clamp01(feed.CarryApproach));
            float speed = desiredSpeed;
            if (_tuning.EnableSpeedFit)
            {
                float fitSpeed = _species.TurnRateDeg * DegreesToRadians * Mathf.Max(0.22f, distance) * _tuning.SpeedFitK;
                speed = Mathf.Max(desiredSpeed * 0.22f, Mathf.Min(desiredSpeed, fitSpeed));
            }

            _turnRate = SteerTowards(bobber.Position, speed, result.RadiusBodyLengths, dt, result.Aim);
            MoveForward(speed, dt);

            if (distance < 2.2f * Mathf.Max(0.05f, _species.Visual.Length))
            {
                _orbitTimer += dt;
            }
            else
            {
                _orbitTimer = 0f;
            }

            if (_orbitTimer > _tuning.OrbitBreakOff)
            {
                _orbitTimer = 0f;
                _approachStage = -1;
                _breakOffTimer = 0.9f;
            }

            if (_breakOffTimer > 0f)
            {
                _breakOffTimer -= dt;
                _turnRate = SteerTowards(_position + HeadingVector, speed, 2.4f, dt);
                MoveForward(result.Speed * 0.9f, dt);
            }

            float contactRadius = Mathf.Max(0.08f, feed.Arrival * 0.72f);
            bool mouthContact = TryGetMouthContact(bobber.Position, contactRadius, out Vector2 ignoredContact);
            if (mouthContact || Vector2.Distance(MouthPosition, bobber.Position) <= contactRadius)
            {
                if (onReachedBobber != null)
                {
                    onReachedBobber(this);
                }
                return;
            }

            if (_stateTimer > _tuning.ApproachTimeout)
            {
                if (_feedCommitment < Mathf.Max(feed.QuitBelow + 0.12f, 0.48f))
                {
                    BeginReject(bobber.Position, feed);
                }
                else
                {
                    BeginLinger();
                }
            }
        }

        private void TickStrike(
            float dt,
            float now,
            BobberV2 bobber,
            IReadOnlyList<FishAgentV2> allFish,
            Action<FishAgentV2> onReachedBobber)
        {
            if (bobber == null || !bobber.IsInWater)
            {
                ReturnToRoam(2f);
                return;
            }

            FeedSignature feed = GetFeedSignature();
            _stateTimer += dt;
            float distance = Vector2.Distance(_position, bobber.Position);
            float speed = Mathf.Max(_species.ApproachSpeed * feed.StrikeSpeed, _vSm * 0.88f);
            _turnRate = SteerTowards(bobber.Position, speed, 0.58f, dt);
            MoveForward(speed, dt);
            float contactRadius = Mathf.Max(0.08f, feed.Arrival * 0.72f);
            bool mouthContact = TryGetMouthContact(bobber.Position, contactRadius, out Vector2 ignoredContact);
            if (mouthContact || Vector2.Distance(MouthPosition, bobber.Position) <= contactRadius)
            {
                if (onReachedBobber != null) onReachedBobber(this);
            }
            else if (_stateTimer > Mathf.Max(0.05f, feed.StrikeDuration))
            {
                BeginReject(bobber.Position, feed);
            }
        }

        private void TickReject(float dt)
        {
            _stateTimer += dt;
            float speed = Mathf.Max(_species.ApproachSpeed * GetFeedSignature().RejectSpeed, _vSm * 0.62f);
            _turnRate = SteerAngle(_rejectAim, speed, 1.25f, dt);
            MoveForward(speed, dt);
            if (_stateTimer > _rejectDuration)
            {
                FeedSignature feed = GetFeedSignature();
                ReturnToRoam(RandomRange(feed.RejectCooldown.Min, feed.RejectCooldown.Max));
            }
        }

        private void TickContest(float dt, float now, BobberV2 bobber, IReadOnlyList<FishAgentV2> allFish)
        {
            if (bobber == null || !bobber.IsInWater)
            {
                State = FishState.Roam;
                _contestRival = null;
                ReanchorAtCurrentPosition();
                return;
            }

            _contestTimer += dt;
            ContestSignature contest = _species.Contest ?? new ContestSignature();
            Vector2 target;
            float speedMultiplier;
            float radius;

            if (State == FishState.Yield)
            {
                Vector2 away = _position - bobber.Position;
                if (away.sqrMagnitude < 0.0001f) away = -HeadingVector;
                target = _position + away.normalized * Mathf.Max(0.8f, _species.Visual.Length * 1.8f);
                speedMultiplier = contest.YieldSpeed;
                radius = 1.25f;
            }
            else if (State == FishState.Steal)
            {
                Vector2 toTarget = bobber.Position - _position;
                Vector2 side = new Vector2(-toTarget.y, toTarget.x).normalized * _contestSide;
                target = bobber.Position + side * Mathf.Max(0.25f, _species.Visual.Length * contest.Flank);
                speedMultiplier = contest.StealSpeed;
                radius = 0.72f;
            }
            else
            {
                FishAgentV2 rival = _contestRival;
                if (rival == null || rival.IsCaught)
                {
                    State = FishState.Interested;
                    _contestRival = null;
                    _contestCooldown = RandomRange(contest.Cooldown.Min, contest.Cooldown.Max);
                    return;
                }

                target = rival.Position - rival.HeadingVector * Mathf.Max(0.10f, rival.Species.Visual.Length * 0.32f);
                speedMultiplier = contest.ChaseSpeed;
                radius = 0.82f;
            }

            float speed = Mathf.Max(_species.ApproachSpeed * speedMultiplier, _vSm * 0.80f);
            _turnRate = SteerTowards(target, speed, radius, dt);
            MoveForward(speed, dt);
            ResolveHardOverlap(allFish);

            if (_contestTimer >= _contestDuration)
            {
                State = FishState.Interested;
                _stateTimer = 0f;
                _contestRival = null;
                _contestCooldown = RandomRange(contest.Cooldown.Min, contest.Cooldown.Max);
                _approachStage = -1;
            }
        }

        private void TickAfterBite(float dt, float now, IReadOnlyList<FishAgentV2> allFish)
        {
            _stateTimer += dt;
            AfterBiteProfile profile = _species != null && _species.AfterBite != null
                ? _species.AfterBite
                : new AfterBiteProfile();
            float duration = Mathf.Max(0.10f, _afterBiteDuration > 0f ? _afterBiteDuration : profile.Duration.Max);
            float u = Mathf.Clamp01(_stateTimer / duration);
            float aim = _afterBiteAim;
            if (_afterBiteMode == AfterBiteMode.Arc)
            {
                aim += _afterBiteSide * profile.Arc * Mathf.Sin(u * Mathf.PI) * 0.65f;
            }

            float speed = _afterBiteSpeed > 0f ? _afterBiteSpeed * (1f - 0.18f * u) : 0.5f;
            if (_afterBiteMode == AfterBiteMode.Jet)
            {
                // Jet exits sharply, then bleeds back into the sampled profile speed.
                speed *= Mathf.Lerp(1.38f, 0.92f, u);
                _jetCharge = Mathf.Lerp(_jetCharge, 0.15f, Mathf.Min(1f, dt * 9f));
            }

            _turnRate = SteerAngle(aim, speed, Mathf.Max(0.40f, profile.TurnBodyLengths), dt);
            MoveForward(speed, dt);
            ResolveHardOverlap(allFish);

            if (_stateTimer >= duration)
            {
                FinishAfterBite(profile);
            }
        }

        private void FinishAfterBite(AfterBiteProfile profile)
        {
            State = FishState.Roam;
            _cooldown = Mathf.Max(_cooldown, RandomRange(profile.Cooldown.Min, profile.Cooldown.Max));
            _stateTimer = 0f;
            _approachTimer = 0f;
            _feedCarrySpeed = 0f;
            _afterBiteDuration = 0f;
            _afterBiteSpeed = 0f;
            _afterBiteAim = 0f;
            _afterBiteSide = 1f;

            // A follower returns through the existing spring slot instead of re-anchoring a
            // path it does not own. Leaders and solo fish re-anchor at their actual exit point.
            if (_leader != null && !_leader.IsCaught)
            {
                _schoolPosition = _position;
                _schoolTarget = _position;
                _schoolVelocity = Vector2.zero;
                return;
            }

            ReanchorAtCurrentPosition();
        }

        private void TickBite(float dt, BobberV2 bobber)
        {
            if (bobber != null && bobber.IsInWater && bobber.HitFish == this)
            {
                // Keep following the actual bobber position while the player holds the
                // bite. The contact target can be several tenths away when a swept segment
                // catches the lure; MoveTowards keeps that correction visible without a
                // one-frame teleport that reads as a school jump.
                _bitePosition = bobber.Position - HeadingVector * MouthOffset;
            }

            float maxStep = Mathf.Max(0.12f, dt * 8f);
            _position = Vector2.MoveTowards(_position, _bitePosition, maxStep);
            _heading = _biteHeading;
            _turnRate = 0f;
        }

        private void TickLinger(float dt, float now)
        {
            _stateTimer += dt;
            FeedSignature feed = GetFeedSignature();
            float speed = _species.ApproachSpeed * feed.LingerSpeed;
            float targetAngle = _heading + Mathf.Sin(now * feed.LingerFreq + _phase) * feed.LingerYaw;
            _turnRate = SteerTowards(_position + AngleVector(targetAngle), speed, 1f, dt);
            MoveForward(speed, dt);
            if (_stateTimer > _lingerDuration)
            {
                ReturnToRoam(RandomRange(feed.RejectCooldown.Min, feed.RejectCooldown.Max));
            }
        }

        private void BeginLinger()
        {
            FeedSignature feed = GetFeedSignature();
            State = FishState.Linger;
            _stateTimer = 0f;
            _lingerDuration = RandomRange(feed.Linger.Min, feed.Linger.Max);
            _feedInitialized = false;
            _strikeReady = false;
        }

        private FeedSignature GetFeedSignature()
        {
            return _species != null && _species.Feed != null ? _species.Feed : new FeedSignature();
        }

        private void BeginReject(Vector2 target, FeedSignature feed)
        {
            State = FishState.Reject;
            _stateTimer = 0f;
            _rejectDuration = RandomRange(feed.Reject.Min, feed.Reject.Max);
            Vector2 away = _position - target;
            if (away.sqrMagnitude < 0.0001f) away = -HeadingVector;
            _rejectAim = Mathf.Atan2(away.y, away.x);
            _feedInitialized = false;
            _strikeReady = false;
            _contestRival = null;
            _approachStage = -1;
        }

        private float ComputeLureCompetition(BobberV2 bobber, IReadOnlyList<FishAgentV2> allFish, float distance)
        {
            if (bobber == null || allFish == null)
            {
                return 0f;
            }

            float pressure = 0f;
            for (int i = 0; i < allFish.Count; i++)
            {
                FishAgentV2 other = allFish[i];
                if (other == null || other == this || other.IsCaught || SharesSchoolWith(other))
                {
                    continue;
                }

                if (other.State != FishState.Interested &&
                    other.State != FishState.Strike &&
                    other.State != FishState.Linger &&
                    other.State != FishState.Yield &&
                    other.State != FishState.Steal &&
                    other.State != FishState.Chase)
                {
                    continue;
                }

                float otherDistance = Vector2.Distance(other.Position, bobber.Position);
                if (otherDistance > distance + 0.55f)
                {
                    continue;
                }

                float ahead = Mathf.Clamp01((distance - otherDistance + 0.20f) / Mathf.Max(0.35f, distance));
                float stateWeight = other.State == FishState.Linger
                    ? 0.92f
                    : (other.State == FishState.Strike
                        ? 0.82f
                        : ((other.State == FishState.Steal || other.State == FishState.Chase) ? 0.68f : 0.42f));
                pressure = Mathf.Max(pressure, Mathf.Clamp01(stateWeight + ahead * 0.45f));
            }

            return pressure;
        }

        private FishAgentV2 FindLureRival(BobberV2 bobber, IReadOnlyList<FishAgentV2> allFish, float distance, float range)
        {
            if (bobber == null || allFish == null)
            {
                return null;
            }

            FishAgentV2 best = null;
            float bestDistance = float.PositiveInfinity;
            for (int i = 0; i < allFish.Count; i++)
            {
                FishAgentV2 other = allFish[i];
                if (other == null || other == this || other.IsCaught || SharesSchoolWith(other))
                {
                    continue;
                }

                if (other.State != FishState.Interested &&
                    other.State != FishState.Strike &&
                    other.State != FishState.Linger &&
                    other.State != FishState.Steal &&
                    other.State != FishState.Chase)
                {
                    continue;
                }

                float otherDistance = Vector2.Distance(other.Position, bobber.Position);
                if (otherDistance > distance + range || otherDistance >= bestDistance)
                {
                    continue;
                }

                best = other;
                bestDistance = otherDistance;
            }

            return best;
        }

        private bool TryBeginContest(BobberV2 bobber, IReadOnlyList<FishAgentV2> allFish, float distance)
        {
            ContestSignature contest = _species.Contest;
            if (contest == null || allFish == null || _contestCooldown > 0f)
            {
                return false;
            }

            float pressure = ComputeLureCompetition(bobber, allFish, distance);
            if (pressure < contest.Trigger)
            {
                return false;
            }

            FishAgentV2 rival = FindLureRival(bobber, allFish, distance, contest.Range);
            if (rival == null)
            {
                return false;
            }

            float yieldWeight = Mathf.Max(0f, contest.YieldWeight);
            float stealWeight = Mathf.Max(0f, contest.StealWeight);
            float chaseWeight = Mathf.Max(0f, contest.ChaseWeight);
            float total = yieldWeight + stealWeight + chaseWeight;
            if (total <= 0.0001f || RandomRange(0f, 1f) > Mathf.Clamp01(pressure))
            {
                return false;
            }

            float roll = RandomRange(0f, total);
            FishState mode;
            if (roll < yieldWeight)
            {
                mode = FishState.Yield;
                _contestDuration = RandomRange(contest.YieldDuration.Min, contest.YieldDuration.Max);
            }
            else if (roll < yieldWeight + stealWeight)
            {
                mode = FishState.Steal;
                _contestDuration = RandomRange(contest.StealDuration.Min, contest.StealDuration.Max);
            }
            else
            {
                mode = FishState.Chase;
                _contestDuration = RandomRange(contest.ChaseDuration.Min, contest.ChaseDuration.Max);
            }

            State = mode;
            _contestRival = rival;
            _contestTimer = 0f;
            _contestSide = _random.NextDouble() < 0.5 ? -1 : 1;
            _approachStage = -1;
            return true;
        }

        private void NotifySchoolLure(IReadOnlyList<FishAgentV2> allFish)
        {
            FeedSignature feed = GetFeedSignature();
            if (!feed.SchoolJoin || allFish == null || !IsInSchool())
            {
                return;
            }

            for (int i = 0; i < allFish.Count; i++)
            {
                FishAgentV2 other = allFish[i];
                if (other == null || other == this || other.IsCaught || !SharesSchoolWith(other))
                {
                    continue;
                }

                if (other.State == FishState.Roam && other._lureDelay < 0f)
                {
                    other._lureDelay = other.RandomRange(feed.JoinDelay.Min, feed.JoinDelay.Max);
                }
            }
        }

        private bool IsInSchool()
        {
            return _leader != null || _species != null && _species.IsSchool;
        }

        private void UpdateRoamMicro(float dt)
        {
            MotionSignature motion = _species != null ? _species.Motion : null;
            if (motion == null || motion.Micro == null)
            {
                _microSpeed = Mathf.Lerp(_microSpeed, 1f, Mathf.Min(1f, dt * 4f));
                _microDrive = Mathf.Lerp(_microDrive, 1f, Mathf.Min(1f, dt * 4f));
                _propRate = 1f;
                _propDrive = 1f;
                return;
            }

            if (_microMode < 0 || _microTimer <= 0f)
            {
                EnterRoamMicro(motion);
            }

            _microTimer -= dt;
            float response = Mathf.Min(1f, dt * 4.5f);
            _microSpeed = Mathf.Lerp(_microSpeed, _microSpeedTarget, response);
            _microDrive = Mathf.Lerp(_microDrive, _microDriveTarget, Mathf.Min(1f, dt * 6f));
            _propRate = Mathf.Clamp(0.72f + 0.58f * _microDrive, 0.38f, 1.42f);
            _propDrive = Mathf.Clamp(_microDrive, 0.38f, 1.42f);
        }

        private void EnterRoamMicro(MotionSignature motion)
        {
            MotionMicroSignature micro = motion.Micro;
            float roll = RandomRange(0f, 1f);
            float burstLimit = Mathf.Clamp01(micro.BurstProbability);
            float coastLimit = burstLimit + Mathf.Clamp01(micro.CoastProbability);
            float pauseLimit = coastLimit + Mathf.Clamp01(micro.PauseProbability);

            if (roll < burstLimit)
            {
                _microMode = 1;
                _microDuration = RandomRange(micro.BurstDuration.Min, micro.BurstDuration.Max);
                _microSpeedTarget = RandomRange(micro.BurstSpeed.Min, micro.BurstSpeed.Max);
                _microDriveTarget = Mathf.Clamp(1f + motion.Kick * 0.55f, 0.40f, 1.42f);
            }
            else if (roll < coastLimit)
            {
                _microMode = 2;
                _microDuration = RandomRange(micro.CoastDuration.Min, micro.CoastDuration.Max);
                _microSpeedTarget = RandomRange(micro.CoastSpeed.Min, micro.CoastSpeed.Max);
                _microDriveTarget = Mathf.Clamp(1f - motion.GlideDrop * 0.55f, 0.38f, 1.20f);
            }
            else if (roll < pauseLimit)
            {
                _microMode = 3;
                _microDuration = RandomRange(micro.PauseDuration.Min, micro.PauseDuration.Max);
                _microSpeedTarget = Mathf.Max(0.20f, micro.PauseSpeed);
                _microDriveTarget = Mathf.Clamp(1f - motion.GlideDrop, 0.38f, 1.20f);
            }
            else
            {
                _microMode = 0;
                _microDuration = RandomRange(micro.CruiseDuration.Min, micro.CruiseDuration.Max);
                _microSpeedTarget = 1f;
                _microDriveTarget = Mathf.Clamp(1f + motion.Drive * 0.25f, 0.38f, 1.42f);
            }

            _microTimer = Mathf.Max(0.05f, _microDuration);
        }

        private void EnterApproachStyle(ApproachStyle style, Vector2 target, float distance)
        {
            switch (style)
            {
                case ApproachStyle.Wary:
                    _approachTimer = 0f;
                    break;
                case ApproachStyle.Spiral:
                    _spiralSide = _random.NextDouble() < 0.5 ? 1f : -1f;
                    _spiralOffset = Mathf.Max(1.3f * _species.Visual.Length, distance * 0.42f);
                    _spiralPass = 0;
                    _spiralArmed = false;
                    _spiralBrake = 0f;
                    _spiralWobble = RandomRange(0f, Tau);
                    break;
                case ApproachStyle.Hesitate:
                    _hesitateHold = RandomRange(0.9f, 2.2f);
                    _hesitateSnap = 0f;
                    _hesitateSide = _random.NextDouble() < 0.5 ? 1f : -1f;
                    break;
            }
        }

        private ApproachResult StepApproach(ApproachStyle style, float dt, float now, Vector2 target, float distance)
        {
            Vector2 toTarget = target - _position;
            float targetAngle = Mathf.Atan2(toTarget.y, toTarget.x);
            switch (style)
            {
                case ApproachStyle.Dash:
                    return new ApproachResult(targetAngle, _species.ApproachSpeed * 1.9f, 0.85f);
                case ApproachStyle.Drift:
                    return new ApproachResult(_heading + WrapAngle(targetAngle - _heading) * 0.34f, _species.ApproachSpeed * 0.88f, 1.8f);
                case ApproachStyle.Wary:
                    bool moving = (_approachTimer % 1.7f) < 0.75f;
                    return new ApproachResult(targetAngle, _species.ApproachSpeed * (moving ? 1.55f : 0.35f), 1.2f);
                case ApproachStyle.Spiral:
                    return StepSpiral(dt, now, target, distance, targetAngle);
                case ApproachStyle.Hesitate:
                    return StepHesitate(dt, now, target, distance, targetAngle);
                default:
                    return new ApproachResult(targetAngle, _species.ApproachSpeed, 1f);
            }
        }

        private ApproachResult StepSpiral(float dt, float now, Vector2 target, float distance, float targetAngle)
        {
            float front = Mathf.Cos(WrapAngle(targetAngle - _heading));
            if (front > 0.55f)
            {
                _spiralArmed = true;
            }
            else if (_spiralArmed && front < 0.15f)
            {
                _spiralArmed = false;
                _spiralPass++;
                _spiralOffset = Mathf.Max(0.22f * _species.Visual.Length, _spiralOffset * 0.5f);
                _spiralBrake = RandomRange(0.30f, 0.65f);
                if (_random.NextDouble() < 0.55) _spiralSide *= -1f;
            }

            if (_spiralBrake > 0f)
            {
                _spiralBrake -= dt;
                return new ApproachResult(_heading, _species.ApproachSpeed * 1.05f, 2.2f);
            }

            if (_spiralOffset <= 0.30f * _species.Visual.Length || _spiralPass >= 4)
            {
                return new ApproachResult(targetAngle, _species.ApproachSpeed * 1.25f, 0.8f);
            }

            Vector2 side = new Vector2(-Mathf.Sin(targetAngle), Mathf.Cos(targetAngle));
            Vector2 passTarget = target + side * (_spiralSide * _spiralOffset);
            float speed = _species.ApproachSpeed * (0.85f + 0.45f * Mathf.Min(1f, distance / (3.2f * _species.Visual.Length)));
            float aim = Mathf.Atan2(passTarget.y - _position.y, passTarget.x - _position.x) + Mathf.Sin(now * 0.8f + _spiralWobble) * 0.08f;
            return new ApproachResult(aim, speed, 1f);
        }

        private ApproachResult StepHesitate(float dt, float now, Vector2 target, float distance, float targetAngle)
        {
            if (_hesitateSnap > 0f)
            {
                _hesitateSnap -= dt;
                return new ApproachResult(targetAngle, _species.ApproachSpeed * 2.8f, 0.6f);
            }

            if (distance < 1.25f)
            {
                _hesitateHold -= dt;
                if (_hesitateHold <= 0f)
                {
                    _hesitateSnap = 0.75f;
                }

                float sideAngle = _hesitateSide * (0.75f + Mathf.Sin(now * 1.6f + _phase) * 0.25f);
                return new ApproachResult(targetAngle + sideAngle, _species.ApproachSpeed * 0.30f, 1.1f);
            }

            return new ApproachResult(targetAngle, _species.ApproachSpeed * 0.92f, 1.3f);
        }

        private int GetApproachStage(float distance)
        {
            if (_species.ApproachPlan == null || _species.ApproachPlan.Length == 0)
            {
                return 0;
            }

            for (int i = 0; i < _species.ApproachPlan.Length; i++)
            {
                ApproachStep step = _species.ApproachPlan[i];
                if (!step.HasUntil || distance > step.UntilDistance)
                {
                    return i;
                }
            }

            return _species.ApproachPlan.Length - 1;
        }

        private float SteerTowards(Vector2 target, float speed, float radiusBodyLengths, float dt)
        {
            float targetAngle = Mathf.Atan2(target.y - _position.y, target.x - _position.x);
            return SteerTowards(target, speed, radiusBodyLengths, dt, targetAngle);
        }

        private float SteerTowards(Vector2 target, float speed, float radiusBodyLengths, float dt, float targetAngle)
        {
            float distance = Vector2.Distance(_position, target);
            float radiusMultiplier = 1f;
            if (distance >= 0f)
            {
                float t = Mathf.Clamp01(distance / (2.2f * Mathf.Max(0.05f, _species.Visual.Length)));
                float smooth = t * t * (3f - 2f * t);
                radiusMultiplier = _tuning.CloseTurnBoost + (1f - _tuning.CloseTurnBoost) * smooth;
            }

            float radius = Mathf.Max(0.04f,
                radiusBodyLengths * _tuning.MinTurnRadiusBodyLengths * Mathf.Max(0.05f, _species.Visual.Length) * radiusMultiplier);
            float omega = Mathf.Min(_species.TurnRateDeg * DegreesToRadians, Mathf.Max(0f, speed) / radius);
            float delta = WrapAngle(targetAngle - _heading);
            float maxStep = omega * dt;
            float step = Mathf.Clamp(delta, -maxStep, maxStep);
            _heading = WrapAngle(_heading + step);
            return step / Mathf.Max(dt, 0.0001f);
        }

        private float SteerAngle(float targetAngle, float speed, float radiusBodyLengths, float dt)
        {
            float radius = Mathf.Max(0.04f,
                radiusBodyLengths * _tuning.MinTurnRadiusBodyLengths * Mathf.Max(0.05f, _species.Visual.Length));
            float omega = Mathf.Min(_species.TurnRateDeg * DegreesToRadians * 2.5f,
                Mathf.Max(0f, speed) / radius);
            float delta = WrapAngle(targetAngle - _heading);
            float maxStep = omega * dt;
            float step = Mathf.Clamp(delta, -maxStep, maxStep);
            _heading = WrapAngle(_heading + step);
            return step / Mathf.Max(dt, 0.0001f);
        }

        private void MoveForward(float speed, float dt)
        {
            _position += HeadingVector * speed * dt;
        }

        private void UpdateAvoidance(Vector2 basePosition, IReadOnlyList<FishAgentV2> allFish, float dt)
        {
            if (!_tuning.EnableAvoidance || allFish == null)
            {
                _avoidTarget = Vector2.zero;
                _avoidOffset = Vector2.Lerp(_avoidOffset, Vector2.zero, Mathf.Min(1f, dt * 2f));
                return;
            }

            float radius = Mathf.Max(0.1f, _species.Visual.Length * _tuning.AvoidanceRadiusBodyLengths);
            Vector2 sum = Vector2.zero;
            int count = 0;
            Vector2 referencePosition = basePosition + _avoidOffset + _startleOffset;
            float horizon = Mathf.Clamp(_tuning.PersonalSpaceHorizon, 0.01f, 0.60f);
            Vector2 predictedPosition = referencePosition + HeadingVector * Mathf.Max(0f, _speedNow) * horizon;
            for (int i = 0; i < allFish.Count; i++)
            {
                FishAgentV2 other = allFish[i];
                if (other == null || other == this || other.IsCaught || SharesSchoolWith(other))
                {
                    continue;
                }

                float otherLength = other.Species != null && other.Species.Visual != null ? other.Species.Visual.Length : 1f;
                float safeDistance = Mathf.Max(
                    radius,
                    (_species.Visual.Length + otherLength) * (0.62f + _tuning.HardOverlapMargin));
                Vector2 otherPredicted = other.Position + other.HeadingVector * Mathf.Max(0f, other.SpeedNow) * horizon;
                Vector2 difference = predictedPosition - otherPredicted;
                float distanceSquared = difference.sqrMagnitude;
                if (distanceSquared < 0.000001f || distanceSquared > safeDistance * safeDistance)
                {
                    continue;
                }

                Vector2 toOther = other.Position - referencePosition;
                if (Vector2.Dot(HeadingVector, toOther) <= 0f)
                {
                    continue;
                }

                float distance = Mathf.Sqrt(distanceSquared);
                float yieldWeight = otherLength / Mathf.Max(0.01f, _species.Visual.Length + otherLength);
                float weight = (1f - distance / safeDistance) * yieldWeight / Mathf.Max(distance, 0.30f);
                sum += difference / distance * weight;
                count++;
            }

            Vector2 target = count > 0 ? sum / count * _tuning.AvoidanceGain / Mathf.Max(0.5f, _species.Visual.Length) : Vector2.zero;
            float cap = _species.Visual.Length * _tuning.AvoidanceOffsetCapBodyLengths;
            if (target.magnitude > cap)
            {
                target = target.normalized * cap;
            }

            _avoidTarget = Vector2.Lerp(_avoidTarget, target, Mathf.Min(1f, dt * _tuning.AvoidanceTargetSmoothing));
            _avoidOffset = Vector2.Lerp(_avoidOffset, _avoidTarget, Mathf.Min(1f, dt * _tuning.AvoidanceSmoothing));
        }

        private Vector2 ComputeSchoolSeparation(Vector2 basePosition, IReadOnlyList<FishAgentV2> allFish)
        {
            if (_leader == null || allFish == null || _species == null || _species.Visual == null)
            {
                return Vector2.zero;
            }

            Vector2 separation = Vector2.zero;
            int count = 0;
            for (int i = 0; i < allFish.Count; i++)
            {
                FishAgentV2 other = allFish[i];
                if (other == null || other == this || other.IsCaught || !SharesSchoolWith(other))
                {
                    continue;
                }

                float otherLength = other.Species != null && other.Species.Visual != null ? other.Species.Visual.Length : 1f;
                float minimum = (_species.Visual.Length + otherLength) * 0.44f;
                Vector2 difference = basePosition - other.Position;
                float distance = difference.magnitude;
                if (distance < 0.0001f || distance >= minimum)
                {
                    continue;
                }

                separation += difference / distance * ((minimum - distance) / minimum);
                count++;
            }

            if (count == 0)
            {
                return Vector2.zero;
            }

            Vector2 result = separation / count * _species.Visual.Length * 0.70f;
            float cap = _species.Visual.Length * 0.45f;
            return result.magnitude > cap ? result.normalized * cap : result;
        }

        private void UpdateTurnPreparation(float dt, bool follower)
        {
            MotionSignature motion = _species != null ? _species.Motion : null;
            if (motion == null || (State != FishState.Roam && State != FishState.Wary))
            {
                _turnPrepTarget = 0f;
                _turnPrep = Mathf.Lerp(_turnPrep, 0f, Mathf.Min(1f, dt * _tuning.TurnPrepFallRate));
                return;
            }

            float futureDelta = 0f;
            if (follower && _leader != null && !_leader.IsCaught)
            {
                // Followers prepare from the leader's intent instead of independently
                // sampling a path that they do not own.
                futureDelta = _leader._turnRate * 0.18f;
            }
            else if (_species.PathType == FishPathType.Lane || _species.PathType == FishPathType.Loop)
            {
                Vector2 current;
                Vector2 future;
                float lookAhead = 0.18f * Mathf.Clamp(_microSpeed, 0.65f, 1.35f);
                if (_species.PathType == FishPathType.Lane)
                {
                    current = EvaluateLane(_pathTime);
                    future = EvaluateLane(_pathTime + lookAhead);
                }
                else
                {
                    current = EvaluateLoop(_pathTime);
                    future = EvaluateLoop(_pathTime + lookAhead);
                }

                Vector2 delta = future - current;
                if (delta.sqrMagnitude > 0.0001f)
                {
                    futureDelta = WrapAngle(Mathf.Atan2(delta.y, delta.x) - _heading);
                }
            }

            // The preparation bends against the upcoming turn. tanh gives a smooth bound and
            // avoids the visibly stuck shape caused by a hard clamp.
            float tanh = (float)System.Math.Tanh(futureDelta * 1.6f);
            _turnPrepTarget = -tanh * Mathf.Clamp(motion.TurnPrep, 0f, 1.5f) * _tuning.TurnPrepMax;
            float rate = Mathf.Abs(_turnPrepTarget) > Mathf.Abs(_turnPrep)
                ? _tuning.TurnPrepRiseRate
                : _tuning.TurnPrepFallRate;
            _turnPrep = Mathf.Lerp(_turnPrep, _turnPrepTarget, Mathf.Min(1f, dt * Mathf.Max(0.01f, rate)));
        }

        private void ResolveHardOverlap(IReadOnlyList<FishAgentV2> allFish)
        {
            if (!_tuning.EnableAvoidance || allFish == null || _species == null || _species.Visual == null)
            {
                return;
            }

            Vector2 correction = Vector2.zero;
            int count = 0;
            float bodyRadius = Mathf.Max(
                0.055f,
                _species.Visual.MaxWidth * 1.35f,
                _species.Visual.Length * 0.075f);
            for (int i = 0; i < allFish.Count; i++)
            {
                FishAgentV2 other = allFish[i];
                if (other == null || other == this || other.IsCaught)
                {
                    continue;
                }

                // Fish on sufficiently different presentation layers may overlap in the
                // 2D simulation without touching on screen. This is the same depth gate used
                // by the v25 personal-space rule.
                if (Mathf.Abs(_visualDepth - other._visualDepth) > 0.20f)
                {
                    continue;
                }

                if (other.Species == null || other.Species.Visual == null)
                {
                    continue;
                }

                float otherBodyRadius = Mathf.Max(
                    0.055f,
                    other.Species.Visual.MaxWidth * 1.35f,
                    other.Species.Visual.Length * 0.075f);
                float schoolScale = SharesSchoolWith(other) ? 0.46f : 0.58f;
                float minimum = (bodyRadius + otherBodyRadius) * schoolScale;
                Vector2 difference = _position - other.Position;
                float distance = difference.magnitude;
                if (distance >= minimum)
                {
                    continue;
                }

                if (distance <= 0.0001f)
                {
                    float angle = _seed * Mathf.PI * 2f;
                    difference = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                    distance = 1f;
                }

                Vector2 direction = difference / distance;
                float push = Mathf.Min(0.035f, (minimum - distance) * 0.22f);
                correction += direction * push;
                count++;
            }

            if (count > 0)
            {
                _position += correction / count * Mathf.Clamp(_tuning.HardOverlapPush, 0.1f, 3f);
            }
        }

        private void UpdateStartleOffset(float dt)
        {
            bool keepOffset = _startleBreakTimer > 0f || State == FishState.Startle || State == FishState.Wary;
            Vector2 target = keepOffset ? _startleTargetOffset : Vector2.zero;
            float response = 1f - Mathf.Exp(-Mathf.Max(0.01f, dt) / 0.12f);
            _startleOffset = Vector2.Lerp(_startleOffset, target, Mathf.Clamp01(response));

            if (!keepOffset && _startleOffset.sqrMagnitude < 0.0001f)
            {
                _startleOffset = Vector2.zero;
                _startleTargetOffset = Vector2.zero;
            }
        }

        private bool ScheduleStartle(Vector2 source, float strength, float delay, int generation, float timeMultiplier)
        {
            if (IsCaught || State == FishState.Bite || State == FishState.Hooked || _startleCooldown > 0f)
            {
                return false;
            }

            if (_startleDelay >= 0f && _startleStrength >= strength && _startleDelay <= delay)
            {
                return false;
            }

            _startleDelay = Mathf.Max(0f, delay);
            _startleStrength = Mathf.Clamp(strength, 0.05f, 1.20f);
            _startleSource = source;
            _startleGeneration = Mathf.Max(0, generation);
            _startleTimeMultiplier = Mathf.Max(0.10f, timeMultiplier);
            return true;
        }

        private void TickScheduledStartle(float dt, IReadOnlyList<FishAgentV2> allFish)
        {
            if (_startleDelay < 0f)
            {
                return;
            }

            _startleDelay -= dt;
            if (_startleDelay > 0f)
            {
                return;
            }

            _startleDelay = -1f;
            ActivateStartle();
            if (_startleEmit)
            {
                PropagateStartle(allFish);
                _startleEmit = false;
            }
        }

        private void ActivateStartle()
        {
            Vector2 away = _position - _startleSource;
            float distance = away.magnitude;
            if (distance <= 0.001f)
            {
                away = -HeadingVector;
            }
            else
            {
                away /= distance;
            }

            float strength = Mathf.Clamp(_startleStrength, 0.05f, 1.20f);
            float offset = _species.StartleRadius * _tuning.StartleImpulse * strength;
            float cap = Mathf.Min(_species.StartleRadius * 0.85f, Mathf.Max(0.05f, _tuning.StartleOffsetCap));
            // Store the route offset as a target. Applying it in one frame recreates the
            // immediate impulse/teleport failure that v25 explicitly removes.
            _startleTargetOffset = away * Mathf.Min(cap, offset);
            _stateTimer = Mathf.Max(_stateTimer, _species.StartleDuration * _startleTimeMultiplier);
            _cooldown = Mathf.Max(_cooldown, _species.StartleDuration * _startleTimeMultiplier * 0.8f);
            _startleBreakTimer = 0.48f + 0.34f * strength;
            State = FishState.Startle;

            // C-start is a folded-then-released event, not a speed multiplier.
            _cStartDuration = 0.30f + 0.09f * strength;
            _cStartRemaining = _cStartDuration;
            _cStartAwayAngle = Mathf.Atan2(away.y, away.x);
            float angleDelta = WrapAngle(_cStartAwayAngle - _heading);
            _cStartDirection = angleDelta >= 0f ? 1f : -1f;
            _startleEmit = true;
        }

        private void PropagateStartle(IReadOnlyList<FishAgentV2> allFish)
        {
            if (allFish == null || _startleGeneration >= _tuning.StartleMaxGenerations)
            {
                return;
            }

            for (int i = 0; i < allFish.Count; i++)
            {
                FishAgentV2 other = allFish[i];
                if (other == null || other == this || other.IsCaught ||
                    (other.State != FishState.Roam && other.State != FishState.Wary) ||
                    other._startleCooldown > 0f)
                {
                    continue;
                }

                float distance = Vector2.Distance(_position, other.Position);
                float radius = Mathf.Max(0.60f, _species.StartleRadius * 0.72f + other.Species.StartleRadius * 0.42f);
                if (distance >= radius)
                {
                    continue;
                }

                float atten = Mathf.Clamp01(1f - distance / radius);
                float strength = Mathf.Clamp(_startleStrength * (0.35f + 0.65f * atten), 0.12f, 0.90f);
                float delay = RandomRange(_tuning.StartleDelayMin, _tuning.StartleDelayMax);
                other.ScheduleStartle(_position, strength, delay, _startleGeneration + 1, 1f);
            }
        }

        private void WrapLaneIfNeeded(IReadOnlyList<FishAgentV2> allFish)
        {
            Vector2 next = _position;
            bool moved = false;
            if (_position.x < _pond.xMin - 1.8f)
            {
                next.x = _pond.xMax + 1.5f;
                moved = true;
            }
            else if (_position.x > _pond.xMax + 1.8f)
            {
                next.x = _pond.xMin - 1.5f;
                moved = true;
            }

            if (_position.y < _pond.yMin - 1.8f)
            {
                next.y = _pond.yMax + 1.5f;
                moved = true;
            }
            else if (_position.y > _pond.yMax + 1.8f)
            {
                next.y = _pond.yMin - 1.5f;
                moved = true;
            }

            if (moved)
            {
                Vector2 delta = next - _position;
                _laneOrigin += delta;
                _previousPosition += delta;
                _position = next;

                // A school wraps as one visual unit. Without shifting the follower's cached
                // position and spring target, the leader crosses the seam while followers
                // chase the old side of the pond and appear to teleport one by one.
                if (allFish != null)
                {
                    for (int i = 0; i < allFish.Count; i++)
                    {
                        FishAgentV2 follower = allFish[i];
                        if (follower == null || follower == this || follower._leader != this)
                        {
                            continue;
                        }

                        follower._position += delta;
                        follower._previousPosition += delta;
                        follower._schoolPosition += delta;
                        follower._schoolTarget += delta;
                        follower._avoidTarget = Vector2.zero;
                        follower._avoidOffset = Vector2.zero;
                    }
                }
            }
        }

        private void ReanchorAtCurrentPosition()
        {
            ReanchorAtPosition(_position);
        }

        private void ReanchorAtPosition(Vector2 anchor)
        {
            if (_species.PathType == FishPathType.Lane)
            {
                _lanePhase = RandomRange(0f, Tau);
                _pathTime = 0f;
                Vector2 perpendicular = new Vector2(-_laneDirection.y, _laneDirection.x);
                float lateral = _species.Lane.Amplitude * Mathf.Sin(_lanePhase);
                _laneOrigin = anchor - perpendicular * lateral;
            }
            else if (_species.PathType == FishPathType.Loop)
            {
                Vector2 bestCenter = _loopCenter;
                float bestError = float.PositiveInfinity;
                float bestPhase = _loopPhase;
                for (int i = 0; i < 12; i++)
                {
                    float phase = RandomRange(0f, Tau);
                    Vector2 center = anchor - new Vector2(
                        _species.Loop.A * Mathf.Cos(phase),
                        _species.Loop.B * Mathf.Sin(2f * phase));
                    center.x = Mathf.Clamp(center.x, _pond.xMin + _species.Loop.A * 0.55f + 0.6f, _pond.xMax - _species.Loop.A * 0.55f - 0.6f);
                    // Keep the re-anchor envelope identical to SetupPath. A stricter clamp
                    // here makes a Loop near the pond edge lose its current phase and snap
                    // when a startle/rejoin event rebuilds the path.
                    center.y = Mathf.Clamp(
                        center.y,
                        _pond.yMin + _species.Loop.B * 0.60f + 0.4f,
                        _pond.yMax - _species.Loop.B * 0.60f - 0.4f);
                    float error = Vector2.Distance(center, anchor - new Vector2(
                        _species.Loop.A * Mathf.Cos(phase),
                        _species.Loop.B * Mathf.Sin(2f * phase)));
                    if (error < bestError)
                    {
                        bestError = error;
                        bestCenter = center;
                        bestPhase = phase;
                    }
                }

                _loopCenter = bestCenter;
                _loopPhase = bestPhase;
                _pathTime = 0f;
            }
            else
            {
                _hoverPosition = anchor;
                _homePosition = anchor;
                _isDashing = false;
                _hasHoverAim = false;
                _hoverRemaining = RandomRange(_species.HoverDash.HoverTime.Min, _species.HoverDash.HoverTime.Max);
                _hoverMax = _hoverRemaining;
                _moodRemaining = 0;
            }

            _avoidTarget = Vector2.zero;
            _avoidOffset = Vector2.zero;
            _hasCourseAngle = false;
            _previousPosition = anchor;
            _schoolPosition = anchor;
            _schoolTarget = anchor;
            _schoolVelocity = Vector2.zero;
            _turnPrep = 0f;
            _turnPrepTarget = 0f;
        }

        private void TickCStart(float dt)
        {
            if (_cStartRemaining > 0f)
            {
                _cStartRemaining = Mathf.Max(0f, _cStartRemaining - dt);
                float u = 1f - _cStartRemaining / Mathf.Max(0.20f, _cStartDuration);
                _cStartBend = _cStartDirection * 0.46f * Mathf.Sin(Mathf.PI * Mathf.Min(u * 1.55f, 1f));
                float burst = _species.ApproachSpeed * (0.35f + 2.4f * Mathf.Clamp01((u - 0.28f) / 0.42f));
                _turnRate = SteerTowards(_position + AngleVector(_cStartAwayAngle), Mathf.Max(burst, 0.4f), 1.1f, dt, _cStartAwayAngle);
                MoveForward(burst, dt);
            }
            else
            {
                _cStartBend = Mathf.Lerp(_cStartBend, 0f, Mathf.Min(1f, dt * 7f));
            }
        }

        private void UpdateMotionTelemetry(float dt)
        {
            Vector2 delta = _position - _previousPosition;
            _speedNow = delta.magnitude / Mathf.Max(dt, 0.0001f);
            float smoothSpeed = Mathf.Min(1f, dt * 7f);
            float smoothTurn = Mathf.Min(1f, dt * 9f);
            _vSm = Mathf.Lerp(_vSm, Mathf.Min(_speedNow, 6f), smoothSpeed);
            _turnSm = Mathf.Lerp(_turnSm, _turnRate, smoothTurn);
            _vAvg = Mathf.Lerp(_vAvg, _vSm, Mathf.Min(1f, dt * 0.25f));

            MotionSignature motion = _species.Motion;
            float targetFrequency = _species.Visual.WaveFrequency;
            float stateBeat = State == FishState.Notice ? 0.22f
                : (State == FishState.Interested ? 1.8f
                : (State == FishState.Strike ? 2.35f
                : ((State == FishState.Bite || State == FishState.Hooked) ? 3.2f
                : (State == FishState.AfterBite ? 1.85f : 1f))));
            float cadence = motion != null ? Mathf.Max(0.05f, motion.Cadence) : 1f;
            float propulsionCadence = _species.PathType == FishPathType.HoverDash
                ? 0.90f + Mathf.Min(1.4f, _vSm * 0.5f)
                : (0.72f + 0.58f * _propRate) * (0.90f + 0.10f * _propDrive);
            float beatTarget = Mathf.Clamp(_propDrive, 0.38f, 1.42f);
            _beat = Mathf.Lerp(_beat, beatTarget, Mathf.Min(1f, dt * 9f));
            _phase += dt * targetFrequency * stateBeat * propulsionCadence * cadence * 5.2f;

            if (_species.Visual.ArmDrift > 0f)
            {
                float lateralAcceleration = _vSm * _turnSm;
                float forwardAcceleration = (_vSm - _vPrev) / Mathf.Max(dt, 0.0001f);
                float spring = Mathf.Max(0.1f, _species.Visual.ArmSpring);
                float damping = Mathf.Max(0.1f, _species.Visual.ArmDamping);
                _armV += (-spring * _armX - damping * _armV + lateralAcceleration * _species.Visual.ArmGain + forwardAcceleration * _species.Visual.ArmForwardGain) * dt;
                float armClamp = Mathf.Max(0.05f, _species.Visual.ArmClamp);
                _armX = Mathf.Clamp(_armX + _armV * dt, -armClamp, armClamp);

                float drag = Mathf.Clamp01(_vSm / Mathf.Max(0.7f, _vAvg * 1.6f));
                float tuckTarget = 1f - 0.58f * drag;
                float tuckRate = tuckTarget < _armTuck ? 12f : 4f;
                _armTuck = Mathf.Lerp(_armTuck, tuckTarget, Mathf.Min(1f, dt * tuckRate));
                float ambientTarget = 0.75f + 0.25f * drag;
                _armAmbient = Mathf.Lerp(_armAmbient, ambientTarget, Mathf.Min(1f, dt * 5f));
                float speedRatio = Mathf.Max(0f, _vSm) / Mathf.Max(0.35f, _vAvg);
                float flowTarget = Mathf.Clamp(speedRatio * 0.75f, 0f, 1.2f);
                _armFlow = Mathf.Lerp(_armFlow, flowTarget, Mathf.Min(1f, dt * (flowTarget > _armFlow ? 6f : 3f)));
                _driftPhase += dt * (0.75f + 3.2f * Mathf.Clamp(speedRatio, 0f, 3f));
            }
            else
            {
                _armFlow = Mathf.Lerp(_armFlow, 0f, Mathf.Min(1f, dt * 5f));
            }

            float ratio = Mathf.Max(0f, _vSm) / Mathf.Max(0.35f, _vAvg);
            float finTarget = Mathf.Clamp(1f / (1f + 0.55f * Mathf.Pow(Mathf.Max(0f, ratio), 1.7f)), 0.12f, 1f);
            _fin = Mathf.Lerp(_fin, finTarget, Mathf.Min(1f, dt * 5f));

            if (_species.PathType == FishPathType.HoverDash)
            {
                float fraction = _hoverMax > 0f ? Mathf.Clamp01(1f - _hoverRemaining / _hoverMax) : 1f;
                float desiredCharge = _isDashing ? 0f : Mathf.Pow(fraction, 0.6f);
                float chargeRate = _isDashing ? 11f : 2.6f;
                _jetCharge = Mathf.Lerp(_jetCharge, desiredCharge, Mathf.Min(1f, dt * chargeRate));
            }
            else
            {
                _jetCharge = Mathf.Lerp(_jetCharge, 0.5f, Mathf.Min(1f, dt * 4f));
            }

            float rollTarget = -_turnSm / Mathf.Max(0.01f, _species.TurnRateDeg * DegreesToRadians) * _species.Visual.Bank;
            _roll = Mathf.Lerp(_roll, Mathf.Clamp(rollTarget, -0.75f, 0.75f), Mathf.Min(1f, dt * 6f));
            _delayedTurn = Mathf.Lerp(_delayedTurn, _turnSm, Mathf.Min(1f, dt * 2.2f));
            _vPrev = _vSm;
        }

        private void UpdateDepth(float dt)
        {
            float target = _visualDepth;
            if (_species.VisualDepth.Min >= 0f && _species.VisualDepth.Max >= _species.VisualDepth.Min)
            {
                target = (_species.VisualDepth.Min + _species.VisualDepth.Max) * 0.5f;
            }
            else
            {
                target = Mathf.Clamp01(Mathf.Lerp(0.10f, 0.84f, (_species.Zone.x + _species.Zone.y) * 0.5f));
            }

            if (_leader != null && !_leader.IsCaught)
            {
                target = _leader._visualDepth;
            }

            switch (State)
            {
                case FishState.Interested:
                    target = Mathf.Min(target, 0.10f);
                    break;
                case FishState.Strike:
                case FishState.Bite:
                case FishState.Hooked:
                    target = Mathf.Min(target, 0.065f);
                    break;
                case FishState.AfterBite:
                    if (_species.AfterBite != null) target = _species.AfterBite.Depth;
                    break;
                case FishState.Startle:
                    target = Mathf.Clamp01(target + 0.10f);
                    break;
            }

            _visualDepth = Mathf.Lerp(_visualDepth, Mathf.Clamp01(target), Mathf.Min(1f, dt * 2.2f));
        }

        private void ApplyVisual(float dt, float now)
        {
            if (!_initialized || _meshRenderer == null)
            {
                return;
            }

            // The camera is on +Z and looks toward -Z, therefore the fish stays in front of
            // the water while its +Z-facing eye discs remain visible. Depth is visual-only;
            // the movement simulation still owns only the X/Y position.
            float fishZ = Mathf.Lerp(_presentation.FishSurfaceZ, _presentation.FishBottomZ, _visualDepth);
            // Optical motion is applied once to the shared underwater RenderTexture. Keep
            // simulation/world movement and optical refraction separate so the fish mesh does
            // not acquire independent jelly-like transform noise.
            transform.position = new Vector3(_position.x, _position.y, fishZ);
            transform.rotation = Quaternion.Euler(0f, 0f, _heading / DegreesToRadians);

            float normalizedTurn = _turnSm / Mathf.Max(0.01f, _species.TurnRateDeg * DegreesToRadians);
            float turnBend = Mathf.Clamp(-normalizedTurn * _tuning.TurnBendScale + _cStartBend,
                -_tuning.MaxTurnBend, _tuning.MaxTurnBend);
            float delayedBend = Mathf.Clamp(-_delayedTurn / Mathf.Max(0.01f, _species.TurnRateDeg * DegreesToRadians) * _tuning.TurnBendScale + _cStartBend,
                -_tuning.MaxTurnBend, _tuning.MaxTurnBend);

            _propertyBlock.Clear();
            _propertyBlock.SetFloat("_Amp", PrototypeWaveAmplitude * _presentation.WaveAmplitudeMultiplier * _species.Visual.WaveAmplitude * _tuning.WaveAmplitudeScale * _beat);
            _propertyBlock.SetFloat("_Phase", _phase);
            _propertyBlock.SetFloat("_Turn", turnBend);
            _propertyBlock.SetFloat("_Len", _species.Visual.Length);
            _propertyBlock.SetFloat("_Lag", _species.Visual.WaveLag);
            _propertyBlock.SetFloat("_Exp", _species.Visual.WaveExponent);
            _propertyBlock.SetFloat("_Hinge", _species.Visual.WaveHinge);
            _propertyBlock.SetFloat("_Spread", _species.Visual.WaveSpread);
            _propertyBlock.SetFloat("_Jet", _jetCharge);
            _propertyBlock.SetFloat("_Roll", _roll);
            _propertyBlock.SetFloat("_Rip", _species.Visual.FinRipple * _fin);
            _propertyBlock.SetFloat("_RipW", _species.Visual.FinWave);
            _propertyBlock.SetFloat("_RipF", _species.Visual.FinRate);
            _propertyBlock.SetFloat("_TimeOffset", _seed * 97f);
            _propertyBlock.SetFloat("_Drift", _species.Visual.ArmDrift * _armAmbient);
            _propertyBlock.SetFloat("_TurnLag", delayedBend);
            _propertyBlock.SetFloat("_ArcBody", _species.Visual.ArcBody);
            _propertyBlock.SetFloat("_ArmSwing", -_armX);
            _propertyBlock.SetFloat("_ArmTuck", _armTuck);
            // These properties are consumed by the v25 fish shader after the water-branch
            // merge. Setting them here is harmless on the compatibility shader and keeps the
            // CPU/render contract ready without touching the water-owned shader file now.
            _propertyBlock.SetFloat("_ArmFlow", _armFlow);
            _propertyBlock.SetFloat("_DriftPh", _driftPhase);
            _propertyBlock.SetFloat("_MantleJet", _species.Visual.MantleJet ? 1f : 0f);
            _propertyBlock.SetFloat("_TurnPrep", _turnPrep);
            _propertyBlock.SetFloat("_Pivot", _tuning.PivotU);
            _propertyBlock.SetFloat("_RimStrength", _presentation.RimStrength);
            _propertyBlock.SetFloat("_Depth", _visualDepth);
            _propertyBlock.SetFloat("_DepthTintStrength", _presentation.FishDepthTintStrength);
            _propertyBlock.SetFloat("_DepthDesaturation", _presentation.FishDepthDesaturation);
            _propertyBlock.SetFloat("_DepthBrightnessDrop", _presentation.FishDepthBrightnessDrop);
            _propertyBlock.SetFloat("_DepthContrast", _presentation.FishDepthContrast);
            float heroFactor = Mathf.Clamp01((_species.BaseScore - 1f) / 29f);
            _propertyBlock.SetFloat("_HeroContrast", heroFactor * _presentation.HeroContrastStrength);
            float edgeFade = EvaluateWrapEdgeFade();
            _propertyBlock.SetFloat("_EdgeFade", edgeFade);
            Vector3 lightDirection = _presentation.LightDirection.sqrMagnitude > 0.001f
                ? _presentation.LightDirection.normalized
                : new Vector3(-0.36f, 0.58f, 0.73f).normalized;
            _propertyBlock.SetVector("_LightDirection", new Vector4(lightDirection.x, lightDirection.y, lightDirection.z, 0f));
            _meshRenderer.SetPropertyBlock(_propertyBlock);

            if (_shadowTransform != null && _shadowRenderer != null)
            {
                float bottomZ = Mathf.Min(_presentation.ShadowBottomZ, fishZ - 0.01f);
                float lightZ = Mathf.Max(0.05f, Mathf.Abs(lightDirection.z));
                float verticalDistance = Mathf.Max(0.02f, fishZ - bottomZ);
                Vector2 worldShadowOffset = new Vector2(
                    -lightDirection.x / lightZ,
                    -lightDirection.y / lightZ) * verticalDistance * _presentation.ShadowDepthInfluence;
                Vector3 worldShadowDelta = new Vector3(worldShadowOffset.x, worldShadowOffset.y, bottomZ - fishZ);
                // InverseTransformVector also removes the fish's presentation scale, so the
                // projected point remains on the same bottom plane for every fish size.
                Vector3 localShadowDelta = transform.InverseTransformVector(worldShadowDelta);
                // Shallow shadows spread into the water and lose density; deep shadows stay
                // closer/tighter but remain a moderate translucent receiver instead of a dark
                // duplicate of the fish silhouette.
                float shadowScale = Mathf.Lerp(_presentation.ShadowMaxScale * 1.10f, _presentation.ShadowMinScale * 0.94f, _visualDepth);
                float softnessBase = Mathf.Max(_presentation.ShadowSoftness, 0.74f);
                float softness = Mathf.Clamp01(Mathf.Lerp(softnessBase, 0.32f, _visualDepth));
                float shadowOpacity = Mathf.Lerp(_presentation.ShadowMinOpacity * 0.16f, _presentation.ShadowMaxOpacity * 0.42f, _visualDepth);

                // The core follows the light vector to the bottom plane. A second, enlarged
                // low-alpha copy supplies a restrained soft edge without external textures.
                _shadowTransform.localPosition = localShadowDelta;
                _shadowTransform.localRotation = Quaternion.identity;
                _shadowTransform.localScale = new Vector3(shadowScale, shadowScale, 0.001f);
                _shadowPropertyBlock.Clear();
                _shadowPropertyBlock.SetColor("_ShadowColor", new Color(0.042f, 0.130f, 0.140f, shadowOpacity * edgeFade));
                _shadowPropertyBlock.SetFloat("_Softness", softness);
                _shadowPropertyBlock.SetFloat("_Depth", _visualDepth);
                _shadowRenderer.SetPropertyBlock(_shadowPropertyBlock);

                if (_shadowSoftTransform != null && _shadowSoftRenderer != null)
                {
                    float softScale = shadowScale * (1.09f + softness * 0.28f);
                    _shadowSoftTransform.localPosition = localShadowDelta + new Vector3(0f, 0f, -0.002f);
                    _shadowSoftTransform.localRotation = Quaternion.identity;
                    _shadowSoftTransform.localScale = new Vector3(softScale, softScale, 0.001f);
                    _shadowSoftPropertyBlock.Clear();
                    _shadowSoftPropertyBlock.SetColor("_ShadowColor", new Color(0.042f, 0.130f, 0.140f, shadowOpacity * (0.32f + softness * 0.28f) * edgeFade));
                    _shadowSoftPropertyBlock.SetFloat("_Softness", Mathf.Min(1f, softness + 0.22f));
                    _shadowSoftPropertyBlock.SetFloat("_Depth", _visualDepth);
                    _shadowSoftRenderer.SetPropertyBlock(_shadowSoftPropertyBlock);
                }
            }
        }

        private float RandomRange(float min, float max)
        {
            return min + (float)_random.NextDouble() * (max - min);
        }

        private int RandomInt(int min, int max)
        {
            return _random.Next(min, max + 1);
        }

        private float SampleNormal(float min, float max)
        {
            float mean = (min + max) * 0.5f;
            float sigma = Mathf.Max(0.001f, (max - min) / 6f);
            double a = Math.Max(0.000001, _random.NextDouble());
            double b = Math.Max(0.000001, _random.NextDouble());
            float normal = (float)(Math.Sqrt(-2.0 * Math.Log(a)) * Math.Cos(Tau * b));
            return Mathf.Clamp(mean + normal * sigma, min, max);
        }

        private static Vector2 AngleVector(float angle)
        {
            return new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
        }

        private static float AngleLerp(float from, float to, float amount)
        {
            return WrapAngle(from + WrapAngle(to - from) * Mathf.Clamp01(amount));
        }

        private static float WrapAngle(float angle)
        {
            while (angle > Mathf.PI) angle -= Tau;
            while (angle < -Mathf.PI) angle += Tau;
            return angle;
        }
    }
}
