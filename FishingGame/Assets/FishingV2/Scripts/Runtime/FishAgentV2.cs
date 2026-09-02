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
        private float _cStartBend;
        private float _cStartDirection;
        private float _cStartAwayAngle;
        private float _armX;
        private float _armV;
        private float _armTuck = 1f;
        private float _armAmbient = 1f;
        private float _delayedTurn;
        private float _seed;

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
        public float VisualDepth01 { get { return _visualDepth; } }
        public float WorldDepthZ { get { return transform.position.z; } }
        public bool IsCaught { get { return State == FishState.Caught; } }
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

            SetupPath();
            _previousPosition = _position;

            _meshFilter = gameObject.GetComponent<MeshFilter>();
            if (_meshFilter == null) _meshFilter = gameObject.AddComponent<MeshFilter>();
            _meshRenderer = gameObject.GetComponent<MeshRenderer>();
            if (_meshRenderer == null) _meshRenderer = gameObject.AddComponent<MeshRenderer>();
            _meshFilter.sharedMesh = mesh != null ? mesh : FishMeshBuilderV2.BuildFallback("FishV2_Fallback");
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
            _cooldown = Mathf.Max(0f, _cooldown - dt);

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
                    TickInterested(dt, now, bobber, onReachedBobber);
                    break;
                case FishState.Bite:
                    TickBite(dt, bobber);
                    break;
                case FishState.Linger:
                    TickLinger(dt, now);
                    break;
            }

            TickCStart(dt);
            UpdateMotionTelemetry(dt);
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
            if (IsCaught || State == FishState.Bite)
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
        }

        public void EnterBite(Vector2 bitePosition)
        {
            if (IsCaught)
            {
                return;
            }

            State = FishState.Bite;
            _stateTimer = 0f;
            _approachTimer = 0f;
            _bitePosition = bitePosition;
            _biteHeading = _heading;
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
            ReanchorAtCurrentPosition();
        }

        public void ApplyStartle(Vector2 source, float radiusMultiplier, float timeMultiplier, bool resetInterest)
        {
            if (IsCaught || State == FishState.Bite)
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
            _startleOffset += away * strength;
            float cap = Mathf.Min(_species.StartleRadius * 0.85f, Mathf.Max(0.05f, _tuning.StartleOffsetCap));
            if (_startleOffset.magnitude > cap)
            {
                _startleOffset = _startleOffset.normalized * cap;
            }

            _stateTimer = Mathf.Max(_stateTimer, _species.StartleDuration * Mathf.Max(0.1f, timeMultiplier));
            _cooldown = Mathf.Max(_cooldown, _species.StartleDuration * Mathf.Max(0.1f, timeMultiplier) * 0.8f);
            State = FishState.Startle;

            // C-start는 속도만 올리는 도피가 아니라, 접혔다가 펴지며 튀는 시각적 사건이다.
            _cStartRemaining = 0.34f;
            _cStartAwayAngle = Mathf.Atan2(away.y, away.x);
            float angleDelta = WrapAngle(_cStartAwayAngle - _heading);
            _cStartDirection = angleDelta >= 0f ? 1f : -1f;
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

            if (follower)
            {
                float cos = Mathf.Cos(_leader._heading);
                float sin = Mathf.Sin(_leader._heading);
                Vector2 rotatedOffset = new Vector2(
                    _formationOffset.x * cos - _formationOffset.y * sin,
                    _formationOffset.x * sin + _formationOffset.y * cos);
                basePosition = _leader._position + rotatedOffset + new Vector2(
                    Mathf.Sin(now * 0.7f + _formationNoise.x) * 0.12f,
                    Mathf.Sin(now * 0.9f + _formationNoise.y) * 0.12f);
            }
            else
            {
                basePosition = TickPath(dt, now);
            }

            UpdateAvoidance(basePosition, allFish, dt);
            _position = basePosition + _avoidOffset + _startleOffset;
            _startleOffset *= Mathf.Pow(Mathf.Clamp(_tuning.StartleOffsetDecay, 0.001f, 0.999f), dt);

            if (!follower && _species.PathType == FishPathType.Lane)
            {
                WrapLaneIfNeeded();
            }

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
                    }
                }
            }
        }

        private Vector2 TickPath(float dt, float now)
        {
            if (_species.PathType == FishPathType.Lane)
            {
                _pathTime += dt;
                return EvaluateLane(_pathTime);
            }

            if (_species.PathType == FishPathType.Loop)
            {
                float drift = _species.Loop.DriftSpeed;
                _loopCenter += new Vector2(
                    Mathf.Sin(now * 0.13f + _loopPhase) * drift * dt,
                    Mathf.Cos(now * 0.11f + _loopPhase * 1.7f) * drift * 0.55f * dt);
                _loopCenter.x = Mathf.Clamp(_loopCenter.x, _pond.xMin - _species.Loop.A * 0.55f, _pond.xMax + _species.Loop.A * 0.55f);
                _loopCenter.y = Mathf.Clamp(_loopCenter.y, _pond.yMin - _species.Loop.B * 0.30f, _pond.yMax + _species.Loop.B * 0.30f);
                _pathTime += dt;
                return EvaluateLoop(_pathTime);
            }

            _position = _hoverPosition;
            TickHover(dt, now);
            _hoverPosition = _position;
            return _position;
        }

        private Vector2 EvaluateLane(float time)
        {
            float period = Mathf.Max(0.05f, _species.Lane.Period);
            float along = _species.Lane.Speed * time;
            float lateral = _species.Lane.Amplitude * Mathf.Sin(Tau * time / period + _lanePhase);
            Vector2 perpendicular = new Vector2(-_laneDirection.y, _laneDirection.x);
            return _laneOrigin + _laneDirection * along + perpendicular * lateral;
        }

        private Vector2 EvaluateLoop(float time)
        {
            float angularSpeed = Tau / Mathf.Max(0.05f, _species.Loop.Period);
            float phase = angularSpeed * time + _loopPhase;
            return _loopCenter + new Vector2(
                _species.Loop.A * Mathf.Cos(phase),
                _species.Loop.B * Mathf.Sin(2f * phase));
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

            _stateTimer += dt;
            Vector2 toBobber = bobber.Position - _position;
            float distance = toBobber.magnitude;
            float speed = Mathf.Max(_species.ApproachSpeed * 0.35f, _speedNow * 0.6f);
            _turnRate = SteerTowards(bobber.Position, speed, 0.9f, dt);
            MoveForward(speed, dt);

            if (_stateTimer >= _tuning.NoticeDuration)
            {
                State = FishState.Interested;
                _stateTimer = 0f;
                _approachTimer = 0f;
                _approachStage = -1;
            }
        }

        private void TickInterested(float dt, float now, BobberV2 bobber, Action<FishAgentV2> onReachedBobber)
        {
            if (bobber == null || !bobber.IsInWater)
            {
                BeginLinger();
                return;
            }

            _stateTimer += dt;
            _approachTimer += dt;
            float distance = Vector2.Distance(_position, bobber.Position);
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

            ApproachResult result = StepApproach(style, dt, now, bobber.Position, distance);
            float speed = result.Speed;
            if (_tuning.EnableSpeedFit)
            {
                float fitSpeed = _species.TurnRateDeg * DegreesToRadians * Mathf.Max(0.22f, distance) * _tuning.SpeedFitK;
                speed = Mathf.Max(result.Speed * 0.22f, Mathf.Min(result.Speed, fitSpeed));
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

            if (distance < _tuning.ArriveRadius)
            {
                if (onReachedBobber != null)
                {
                    onReachedBobber(this);
                }
                return;
            }

            if (_stateTimer > _tuning.ApproachTimeout)
            {
                BeginLinger();
            }
        }

        private void TickBite(float dt, BobberV2 bobber)
        {
            _position = Vector2.Lerp(_position, _bitePosition, Mathf.Min(1f, dt * 9f));
            _heading = _biteHeading;
            _turnRate = 0f;
        }

        private void TickLinger(float dt, float now)
        {
            _stateTimer += dt;
            float speed = _species.ApproachSpeed * 0.28f;
            float targetAngle = _heading + Mathf.Sin(now * 1.3f + _phase) * 1.2f;
            _turnRate = SteerTowards(_position + AngleVector(targetAngle), speed, 1f, dt);
            MoveForward(speed, dt);
            if (_stateTimer > _lingerDuration)
            {
                ReturnToRoam(3f);
            }
        }

        private void BeginLinger()
        {
            State = FishState.Linger;
            _stateTimer = 0f;
            _lingerDuration = RandomRange(_tuning.LingerMin, _tuning.LingerMax);
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
            for (int i = 0; i < allFish.Count; i++)
            {
                FishAgentV2 other = allFish[i];
                if (other == null || other == this || other.IsCaught || SharesSchoolWith(other))
                {
                    continue;
                }

                Vector2 difference = referencePosition - other.Position;
                float distanceSquared = difference.sqrMagnitude;
                if (distanceSquared < 0.000001f || distanceSquared > radius * radius)
                {
                    continue;
                }

                Vector2 toOther = other.Position - referencePosition;
                if (Vector2.Dot(HeadingVector, toOther) <= 0f)
                {
                    continue;
                }

                float distance = Mathf.Sqrt(distanceSquared);
                float otherLength = other.Species != null && other.Species.Visual != null ? other.Species.Visual.Length : 1f;
                float yieldWeight = otherLength / Mathf.Max(0.01f, _species.Visual.Length + otherLength);
                float weight = (1f - distance / radius) * yieldWeight / Mathf.Max(distance, 0.30f);
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

        private void WrapLaneIfNeeded()
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
            }
        }

        private void ReanchorAtCurrentPosition()
        {
            if (_species.PathType == FishPathType.Lane)
            {
                _lanePhase = RandomRange(0f, Tau);
                _pathTime = 0f;
                Vector2 perpendicular = new Vector2(-_laneDirection.y, _laneDirection.x);
                float lateral = _species.Lane.Amplitude * Mathf.Sin(_lanePhase);
                _laneOrigin = _position - perpendicular * lateral;
            }
            else if (_species.PathType == FishPathType.Loop)
            {
                Vector2 bestCenter = _loopCenter;
                float bestError = float.PositiveInfinity;
                float bestPhase = _loopPhase;
                for (int i = 0; i < 12; i++)
                {
                    float phase = RandomRange(0f, Tau);
                    Vector2 center = _position - new Vector2(
                        _species.Loop.A * Mathf.Cos(phase),
                        _species.Loop.B * Mathf.Sin(2f * phase));
                    center.x = Mathf.Clamp(center.x, _pond.xMin + _species.Loop.A * 0.55f + 0.6f, _pond.xMax - _species.Loop.A * 0.55f - 0.6f);
                    center.y = Mathf.Clamp(center.y, _pond.yMin + _species.Loop.B + 0.6f, _pond.yMax - _species.Loop.B - 0.6f);
                    float error = Vector2.Distance(center, _position - new Vector2(
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
                _hoverPosition = _position;
                _homePosition = _position;
                _isDashing = false;
                _hasHoverAim = false;
                _hoverRemaining = RandomRange(_species.HoverDash.HoverTime.Min, _species.HoverDash.HoverTime.Max);
                _hoverMax = _hoverRemaining;
                _moodRemaining = 0;
            }

            _avoidTarget = Vector2.zero;
            _avoidOffset = Vector2.zero;
            _hasCourseAngle = false;
            _previousPosition = _position;
        }

        private void TickCStart(float dt)
        {
            if (_cStartRemaining > 0f)
            {
                _cStartRemaining = Mathf.Max(0f, _cStartRemaining - dt);
                float u = 1f - _cStartRemaining / 0.34f;
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

            float targetFrequency = _species.Visual.WaveFrequency;
            float stateBeat = State == FishState.Notice ? 0.22f : State == FishState.Interested ? 1.8f : 1f;
            if (State == FishState.Bite) stateBeat = 3.2f;
            _phase += dt * targetFrequency * stateBeat * (0.9f + Mathf.Min(1.4f, _vSm * 0.5f)) * 5.2f;

            if (_species.Visual.ArmDrift > 0f)
            {
                float lateralAcceleration = _vSm * _turnSm;
                float forwardAcceleration = (_vSm - _vPrev) / Mathf.Max(dt, 0.0001f);
                float spring = Mathf.Max(0.1f, _species.Visual.ArmSpring);
                float damping = Mathf.Max(0.1f, _species.Visual.ArmDamping);
                _armV += (-spring * _armX - damping * _armV + lateralAcceleration * _species.Visual.ArmGain + forwardAcceleration * _species.Visual.ArmForwardGain) * dt;
                _armX = Mathf.Clamp(_armX + _armV * dt, -0.85f, 0.85f);

                float drag = Mathf.Clamp01(_vSm / Mathf.Max(0.7f, _vAvg * 1.6f));
                float tuckTarget = 1f - 0.82f * drag;
                float tuckRate = tuckTarget < _armTuck ? 11f : 3.4f;
                _armTuck = Mathf.Lerp(_armTuck, tuckTarget, Mathf.Min(1f, dt * tuckRate));
                _armAmbient = Mathf.Lerp(_armAmbient, 1f - 0.80f * drag, Mathf.Min(1f, dt * 4f));
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
            _propertyBlock.SetFloat("_Pivot", _tuning.PivotU);
            _propertyBlock.SetFloat("_RimStrength", _presentation.RimStrength);
            _propertyBlock.SetFloat("_Depth", _visualDepth);
            _propertyBlock.SetFloat("_DepthTintStrength", _presentation.FishDepthTintStrength);
            _propertyBlock.SetFloat("_DepthDesaturation", _presentation.FishDepthDesaturation);
            _propertyBlock.SetFloat("_DepthBrightnessDrop", _presentation.FishDepthBrightnessDrop);
            _propertyBlock.SetFloat("_DepthContrast", _presentation.FishDepthContrast);
            float heroFactor = Mathf.Clamp01((_species.BaseScore - 1f) / 29f);
            _propertyBlock.SetFloat("_HeroContrast", heroFactor * _presentation.HeroContrastStrength);
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
                _shadowPropertyBlock.SetColor("_ShadowColor", new Color(0.042f, 0.130f, 0.140f, shadowOpacity));
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
                    _shadowSoftPropertyBlock.SetColor("_ShadowColor", new Color(0.042f, 0.130f, 0.140f, shadowOpacity * (0.32f + softness * 0.28f)));
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
