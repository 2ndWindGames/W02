using System;
using UnityEngine;

namespace Fishing.V2
{
    public enum BobberPhase
    {
        Idle,
        InWater,
        Reeling,
        Casting
    }

    /// <summary>
    /// 찌의 시간 상태만 관리한다. 물고기 목록이나 세션 매니저를 직접 찾지 않는다.
    /// </summary>
    public sealed class BobberV2 : MonoBehaviour
    {
        private FishingV2TuningAsset _tuning;
        private Rect _pond;
        private Vector2 _pendingPosition;
        private float _phaseTimer;
        private float _lastCastTime = -100f;
        private int _spamCount;
        private float _biteRemaining;
        private float _biteMax;
        private Transform _visual;
        private MeshRenderer _visualRenderer;
        private MaterialPropertyBlock _visualPropertyBlock;
        private LineRenderer _ring;
        private LineRenderer _ripple;
        private LineRenderer _gauge;
        private LineRenderer _castPath;
        private float _now;
        private Vector2 _castStartPosition;
        private float _visualScale = 0.23f;
        // Presentation-only multiplier on the physical ripple. The bobber ring stays a
        // gameplay indicator; this is the water effect, so only this one follows the profile.
        private float _physicalRippleStrength = 1f;

        private const int CastPathPointCount = 18;
        private const int GaugePointCount = 49;

        private static readonly Color BobberIdleColor = new Color(229f / 255f, 160f / 255f, 58f / 255f, 1f);
        private static readonly Color BobberBiteColor = new Color(1f, 243f / 255f, 218f / 255f, 1f);
        private static readonly Color BobberOnColor = new Color(1f, 138f / 255f, 52f / 255f, 1f);
        private static readonly Color BobberLateColor = new Color(216f / 255f, 54f / 255f, 42f / 255f, 1f);

        public BobberPhase Phase { get; private set; } = BobberPhase.Idle;
        public Vector2 Position { get; private set; }
        public FishAgentV2 HitFish { get; private set; }
        public float BiteRemaining01 { get { return _biteMax > 0f ? Mathf.Clamp01(_biteRemaining / _biteMax) : 0f; } }
        public bool IsInWater { get { return Phase == BobberPhase.InWater; } }
        public int RecastSpamCount { get { return _spamCount; } }

        public event Action<Vector2, float, float> CastCompleted;
        public event Action<FishAgentV2> BiteExpired;

        public void Initialize(
            FishingV2TuningAsset tuning,
            Rect pond,
            Transform visual,
            LineRenderer ring,
            LineRenderer castPath,
            float visualScale,
            float opticalScale,
            float opticalSpeed,
            float opticalStrength)
        {
            Initialize(tuning, pond, visual, ring, castPath, visualScale, opticalScale, opticalSpeed, opticalStrength, null, null);
        }

        public void Initialize(
            FishingV2TuningAsset tuning,
            Rect pond,
            Transform visual,
            LineRenderer ring,
            LineRenderer castPath,
            float visualScale,
            float opticalScale,
            float opticalSpeed,
            float opticalStrength,
            LineRenderer ripple)
        {
            Initialize(tuning, pond, visual, ring, castPath, visualScale, opticalScale, opticalSpeed, opticalStrength, ripple, null);
        }

        public void Initialize(
            FishingV2TuningAsset tuning,
            Rect pond,
            Transform visual,
            LineRenderer ring,
            LineRenderer castPath,
            float visualScale,
            float opticalScale,
            float opticalSpeed,
            float opticalStrength,
            LineRenderer ripple,
            LineRenderer gauge)
        {
            _tuning = tuning;
            _pond = pond;
            _visual = visual;
            _visualRenderer = visual != null ? visual.GetComponent<MeshRenderer>() : null;
            _visualPropertyBlock = new MaterialPropertyBlock();
            _ring = ring;
            _ripple = ripple;
            _gauge = gauge;
            _castPath = castPath;
            _visualScale = Mathf.Max(0.05f, visualScale);
            // The signature remains compatible with the earlier presentation contract. The
            // coherent underwater composite owns optical motion now, so the bobber itself does
            // not consume these values as a separate transform/ripple offset.
            Position = pond.center;
            _castStartPosition = Position;
            Phase = BobberPhase.Idle;
            UpdateVisual();
        }

        /// <summary>
        /// 물 프로파일이 물리적 파문의 세기를 정한다. 금색 상호작용 링은 gameplay 지표라
        /// 여기 영향을 받지 않는다.
        /// </summary>
        public void SetPhysicalRippleStrength(float strength)
        {
            _physicalRippleStrength = Mathf.Max(0f, strength);
        }

        /// <summary>
        /// 새 세션을 위해 찌를 처음 상태로 되돌린다. 이걸 안 하면 세션을 다시 시작했을 때
        /// 이전 찌가 물에 뜬 채로 "던질 자리를 고르는" 화면이 시작된다.
        /// </summary>
        public void ResetForNewSession()
        {
            ClearBite();
            Phase = BobberPhase.Idle;
            Position = _pond.center;
            _pendingPosition = Position;
            _castStartPosition = Position;
            _phaseTimer = 0f;
            _spamCount = 0;
            _lastCastTime = -100f;
            UpdateVisual();
        }

        public void RequestCast(Vector2 position)
        {
            _pendingPosition = ClampToPond(position, 0.5f);
            if (Phase == BobberPhase.InWater)
            {
                Phase = BobberPhase.Reeling;
                _phaseTimer = _tuning.ReelDuration;
            }
            else if (Phase == BobberPhase.Idle)
            {
                _castStartPosition = Position;
                Phase = BobberPhase.Casting;
                // Keep the initial cast in the Casting phase for the configured duration.
                // A zero timer completed the cast on the very next frame, making the bobber
                // appear to teleport instead of visibly travelling to the target.
                _phaseTimer = _tuning != null ? _tuning.CastDuration : 0.30f;
            }
        }

        public void Tick(float dt, float now)
        {
            if (_tuning == null)
            {
                return;
            }

            dt = _tuning.ClampDelta(dt);
            _now = now;

            if (Phase == BobberPhase.Reeling)
            {
                _phaseTimer -= dt;
                if (_phaseTimer <= 0f)
                {
                    HitFish = null;
                    _castStartPosition = Position;
                    Phase = BobberPhase.Casting;
                    _phaseTimer = _tuning.CastDuration;
                }
            }
            else if (Phase == BobberPhase.Casting)
            {
                _phaseTimer -= dt;
                if (_phaseTimer <= 0f)
                {
                    CompleteCast();
                }
            }
            else if (Phase == BobberPhase.InWater && HitFish != null)
            {
                _biteRemaining -= dt;
                if (_biteRemaining <= 0f)
                {
                    FishAgentV2 expired = HitFish;
                    if (BiteExpired != null) BiteExpired(expired);
                }
            }

            UpdateVisual();
        }

        public bool TryTakeBite(FishAgentV2 fish)
        {
            if (Phase != BobberPhase.InWater || HitFish != null || fish == null)
            {
                return false;
            }

            FishSpeciesConfig species = fish.Species;
            FeedSignature feed = species != null && species.Feed != null ? species.Feed : new FeedSignature();
            float contactRadius = Mathf.Max(0.08f, feed.Arrival * 0.72f);
            if (!fish.TryGetMouthContact(Position, contactRadius, out Vector2 contact) &&
                Vector2.Distance(fish.MouthPosition, Position) > contactRadius)
            {
                return false;
            }

            HitFish = fish;
            float biteWindow = _tuning != null && _tuning.EnableAutomaticReel
                ? _tuning.AutoReelSeconds
                : (species != null ? species.BiteWindow : 1f);
            _biteMax = Mathf.Max(0.05f, biteWindow);
            _biteRemaining = _biteMax;
            Vector2 direction = fish.HeadingVector;
            // Lock the authored mouth to the bobber. The old body-length offset made the
            // fish visibly bite beside the lure and ignored species-specific mouth offsets.
            fish.EnterBite(Position - direction * fish.MouthOffset);
            return true;
        }

        public void ClearBite()
        {
            HitFish = null;
            _biteRemaining = 0f;
            _biteMax = 0f;
        }

        public void ReelImmediately()
        {
            if (HitFish != null && BiteExpired != null)
            {
                BiteExpired(HitFish);
            }
        }

        public void SetNow(float now)
        {
            _now = now;
            UpdateVisual();
        }

        private void CompleteCast()
        {
            Position = _pendingPosition;
            Phase = BobberPhase.InWater;
            ClearBite();

            float elapsed = _now - _lastCastTime;
            if (elapsed < _tuning.RecastSpamWindow)
            {
                _spamCount++;
            }
            else
            {
                _spamCount = 0;
            }

            _lastCastTime = _now;
            float timeMultiplier = Mathf.Min(_tuning.RecastTimeMultiplierMax, 1f + _spamCount * 0.5f);
            float radiusMultiplier = Mathf.Min(_tuning.RecastRadiusMultiplierMax, 1f + _spamCount * 0.12f);
            if (CastCompleted != null) CastCompleted(Position, radiusMultiplier, timeMultiplier);
        }

        private void UpdateVisual()
        {
            Vector2 visualPosition = Position;
            float castProgress = 0f;
            float castArc = 0f;
            if (Phase == BobberPhase.Casting)
            {
                float duration = Mathf.Max(0.01f, _tuning != null ? _tuning.CastDuration : 0.55f);
                castProgress = 1f - Mathf.Clamp01(_phaseTimer / duration);
                float eased = castProgress * castProgress * (3f - 2f * castProgress);
                visualPosition = Vector2.Lerp(_castStartPosition, _pendingPosition, eased);
                castArc = Mathf.Clamp(0.40f + Vector2.Distance(_castStartPosition, _pendingPosition) * 0.12f, 0.40f, 1.15f);
                visualPosition.y += Mathf.Sin(castProgress * Mathf.PI) * castArc;
            }

            if (_visual != null)
            {
                // 첫 캐스팅 전에는 찌가 화면에 없어야 한다. Idle은 그 구간에만 나온다.
                bool visible = Phase != BobberPhase.Idle;
                if (_visual.gameObject.activeSelf != visible) _visual.gameObject.SetActive(visible);
                float dip = HitFish != null ? 1f : 0f;
                // The water is an opaque background at -Z; keep the bobber on the +Z-facing side.
                Vector3 world = new Vector3(visualPosition.x, visualPosition.y, 0.46f - dip * 0.05f);
                world.x += Mathf.Sin(_now * 17f) * dip * 0.022f;
                _visual.position = world;
                _visual.localScale = Vector3.one * _visualScale * (1f - dip * 0.34f);

                if (_visualRenderer != null)
                {
                    if (_visualPropertyBlock == null)
                    {
                        // MaterialPropertyBlock is runtime-only and may be missing when a
                        // BobberV2 object is rehydrated across an editor PlayMode transition.
                        _visualPropertyBlock = new MaterialPropertyBlock();
                    }
                    _visualPropertyBlock.Clear();
                    Color bobberColor = ResolveBobberColor();
                    _visualPropertyBlock.SetColor("_BaseColor", bobberColor);
                    _visualPropertyBlock.SetColor("_Color", bobberColor);
                    _visualRenderer.SetPropertyBlock(_visualPropertyBlock);
                }
            }

            if (_ring != null)
            {
                _ring.enabled = Phase == BobberPhase.InWater;
                Vector2 ringPosition = Position;
                _ring.transform.position = new Vector3(ringPosition.x, ringPosition.y, 0.42f);
                _ring.transform.localScale = Vector3.one * (1f + Mathf.Sin(_now * 2.4f) * 0.06f);
                Color ringColor = ResolveBobberColor();
                ringColor.a = HitFish == null ? 0.65f : 0.85f;
                _ring.startColor = ringColor;
                _ring.endColor = ringColor;
            }

            UpdateGauge();

            if (_ripple != null)
            {
                // The physical ripple is intentionally separate from the gold interaction
                // ring. It remains low-alpha and receives the same final RT refraction rather
                // than adding a second per-object optical offset.
                bool showRipple = Phase == BobberPhase.InWater;
                _ripple.enabled = showRipple;
                _ripple.transform.position = new Vector3(Position.x, Position.y, 0.40f);
                float pulse = 0.5f + 0.5f * Mathf.Sin(_now * 2.1f + 0.6f);
                _ripple.transform.localScale = Vector3.one * (0.74f + pulse * 0.42f);
                // 세기를 1 위로 올릴 때만 선이 굵어진다. 1.0에서는 기존 값 그대로다.
                _ripple.widthMultiplier = 0.026f * Mathf.Lerp(1f, 1.55f, Mathf.Clamp01(_physicalRippleStrength - 1f));
                Color rippleColor = new Color(
                    0.32f,
                    0.78f,
                    0.76f,
                    Mathf.Clamp01((0.36f + pulse * 0.18f) * _physicalRippleStrength));
                _ripple.startColor = rippleColor;
                _ripple.endColor = rippleColor;
            }

            if (_castPath != null)
            {
                bool showPath = Phase == BobberPhase.Casting;
                _castPath.enabled = showPath;
                if (showPath)
                {
                    int count = Mathf.Max(2, CastPathPointCount);
                    if (_castPath.positionCount != count) _castPath.positionCount = count;
                    for (int i = 0; i < count; i++)
                    {
                        float t = i / (float)(count - 1);
                        Vector2 point = Vector2.Lerp(_castStartPosition, _pendingPosition, t);
                        point.y += Mathf.Sin(t * Mathf.PI) * castArc;
                        _castPath.SetPosition(i, new Vector3(point.x, point.y, 0.44f));
                    }
                }
            }
        }

        private Color ResolveBobberColor()
        {
            if (HitFish == null || Phase != BobberPhase.InWater)
            {
                return BobberIdleColor;
            }

            float elapsed = Mathf.Max(0f, _biteMax - _biteRemaining);
            float held = Mathf.Clamp01(elapsed / Mathf.Max(0.001f, _biteMax));
            Color color = Color.Lerp(BobberOnColor, BobberLateColor, held);
            if (elapsed < 0.14f)
            {
                float flash = 1f - Mathf.Clamp01(elapsed / 0.14f);
                color = Color.Lerp(color, BobberBiteColor, flash);
            }

            return color;
        }

        private void UpdateGauge()
        {
            if (_gauge == null)
            {
                return;
            }

            bool visible = Phase == BobberPhase.InWater && HitFish != null && _biteMax > 0f && _biteRemaining > 0f;
            _gauge.enabled = visible;
            if (!visible)
            {
                return;
            }

            _gauge.transform.position = new Vector3(Position.x, Position.y, 0.40f);
            float remaining = BiteRemaining01;
            int visibleSegments = Mathf.Clamp(Mathf.CeilToInt(remaining * (GaugePointCount - 1)), 1, GaugePointCount - 1);
            float arc = remaining * Mathf.PI * 2f;
            Color color = ResolveBobberColor();
            color.a = 0.30f + 0.55f * remaining;
            _gauge.startColor = color;
            _gauge.endColor = color;

            // Keep one preallocated line and collapse the unused tail to the current end.
            // This preserves the HTML contract of a shrinking arc without reallocating or
            // rebuilding gauge geometry every frame.
            for (int i = 0; i < GaugePointCount; i++)
            {
                int segment = Mathf.Min(i, visibleSegments);
                float t = segment / (float)(GaugePointCount - 1);
                float angle = Mathf.PI * 0.5f - t * arc;
                _gauge.SetPosition(i, new Vector3(Mathf.Cos(angle) * 0.29f, Mathf.Sin(angle) * 0.29f, 0f));
            }
        }

        private Vector2 ClampToPond(Vector2 position, float margin)
        {
            return new Vector2(
                Mathf.Clamp(position.x, _pond.xMin + margin, _pond.xMax - margin),
                Mathf.Clamp(position.y, _pond.yMin + margin, _pond.yMax - margin));
        }
    }
}
