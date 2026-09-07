using System;
using UnityEngine;

namespace Fishing.V2
{
    /// <summary>
    /// 세션 시작 연출의 시간표. 잠수는 사건이 아니라 이동이라 짧게 끊으면 "값이 바뀌었다"로
    /// 읽힌다. 물 밖에서 잠깐 보다가 2초 넘게 천천히 내려가고, 마지막에 프레이밍만 앉힌다.
    /// </summary>
    [Serializable]
    public sealed class FishingV2DivePresentationTiming
    {
        [Tooltip("찌가 날아가는 구간. 이 동안 카메라는 이미 내려오기 시작한다.")]
        [Range(0f, 2f)] public float AboveWaterSeconds = 0.55f;
        [Tooltip("착수부터 수면을 뚫기까지. 카메라가 천천히 다가가는 구간이다.")]
        [Range(0.2f, 4f)] public float DiveSeconds = 2.25f;
        [Tooltip("수면을 뚫은 뒤 줌만 계속해서 안착하는 구간.")]
        [Range(0f, 1.5f)] public float SettleSeconds = 0.60f;
        [Tooltip("Dive 구간 안에서 수면을 실제로 뚫는 지점. 여기까지는 물 밖이라 광학이 거의 안 변한다.")]
        [Range(0.1f, 0.95f)] public float CrossingFraction = 0.84f;

        public float DiveEnd { get { return AboveWaterSeconds + DiveSeconds; } }
        /// <summary>카메라가 수면을 뚫는 시각.</summary>
        public float CrossingTime
        {
            get { return AboveWaterSeconds + DiveSeconds * Mathf.Clamp(CrossingFraction, 0.05f, 0.95f); }
        }
        public float TotalSeconds { get { return AboveWaterSeconds + DiveSeconds + SettleSeconds; } }
    }

    /// <summary>
    /// 물 프로파일 하나를 화면에 올리는 주체. 세션은 여기서 나온 프로파일을 머티리얼에
    /// 바르기만 한다.
    ///
    /// 이 클래스는 물고기/게임플레이 상태를 전혀 보지 않는다. 시뮬레이션 계층 규칙과 같은
    /// 이유로 매니저도 부르지 않는다 — 값을 받아 값을 돌려주는 형태라 스크럽·리플레이가 공짜다.
    /// </summary>
    public sealed class FishingV2WaterPresentationDirector
    {
        private FishingV2WaterProfile _gameplay;
        private FishingV2WaterProfile _presentation;
        private FishingV2WaterProfile _dive;
        private FishingV2DivePresentationTiming _timing = new FishingV2DivePresentationTiming();

        private float _time;
        private bool _playing;
        // 물 밖에서 찌 던질 자리를 고르는 동안의 무기한 정지. 시계가 아니라 상태다.
        private bool _awaitingOpeningCast;
        // 지금 화면에 걸린 프로파일의 바탕. 펄스가 이 위에 얹히므로 따로 들고 있어야
        // 매 프레임 다시 얹어도 값이 누적되지 않는다.
        private FishingV2WaterProfile _baseProfile;
        private float _pulseAge = 99f;
        private float _pulseStrength;
        private const float PulseDuration = 1.35f;
        // 디버그 홀드는 입력을 잠그지 않는다. A/B 비교 중에 찌를 던져서 physical ripple까지
        // 같이 보려면 잠그면 안 된다.
        private bool _debugHold;

        public FishingV2SessionPresentationPhase Phase { get; private set; } = FishingV2SessionPresentationPhase.Gameplay;
        /// <summary>이번 Tick에서 수면을 뚫었는가. 기포와 물방울 소거가 여기 걸린다.</summary>
        public bool CrossedSurfaceThisTick { get; private set; }
        public FishingV2WaterProfile Current { get; private set; }
        public float Time { get { return _time; } }
        public float TotalSeconds { get { return _timing.TotalSeconds; } }
        public bool IsPlaying { get { return _playing; } }
        public bool IsAwaitingOpeningCast { get { return _awaitingOpeningCast; } }

        /// <summary>
        /// 게임 크롬의 알파. 물이 주인공인 구간에 점수판이 떠 있으면 그 구간이 연출이 아니라
        /// 로딩 화면으로 읽힌다. 카메라가 자리를 잡는 settle 구간에 맞춰 올라온다.
        /// </summary>
        public float HudAlpha { get; private set; } = 1f;

        /// <summary>
        /// 연출이 완전히 끝나 플레이어가 개입할 수 있는 상태인가.
        ///
        /// 세션 시계·입력·물고기의 찌 반응이 전부 이 하나에 걸린다. 카메라가 아직 내려앉는
        /// 중인데 물고기가 찌에 몰리면, 플레이어가 손을 못 대는 사이에 입질이 끝난다 —
        /// 실제로 검증 중에 잠수 도중 한 마리가 잡혔다.
        /// </summary>
        public bool IsSessionLive
        {
            get
            {
                return !_awaitingOpeningCast
                    && !_playing
                    && Phase == FishingV2SessionPresentationPhase.Gameplay;
            }
        }

        /// <summary>
        /// 연출이 입력을 쥐고 있는가. 물 밖 대기 중에는 잠그지 않는다 — 그 상태를 끝내는
        /// 행동이 바로 캐스팅이라, 여기서 입력을 막으면 세션이 시작되지 않는다.
        /// </summary>
        public bool InputLocked
        {
            get { return !_debugHold && !_awaitingOpeningCast && !IsSessionLive; }
        }

        public void Configure(
            FishingV2WaterProfile gameplay,
            FishingV2WaterProfile presentation,
            FishingV2WaterProfile dive,
            FishingV2DivePresentationTiming timing)
        {
            _gameplay = gameplay;
            _presentation = presentation;
            _dive = dive;
            if (timing != null) _timing = timing;
            if (!_playing && !_debugHold)
            {
                ForceGameplay();
            }
            else
            {
                Evaluate();
            }
        }

        /// <summary>
        /// 진행 중인 연출을 건드리지 않고 프로파일과 시간표만 갈아끼운다.
        ///
        /// Configure()는 상태를 초기화하므로 매 프레임 부를 수 없다. 애셋을 재생 중에
        /// 편집해도 바로 보이려면 상태를 보존한 채 값만 바꾸는 통로가 따로 있어야 한다.
        /// </summary>
        public void UpdateProfiles(
            FishingV2WaterProfile gameplay,
            FishingV2WaterProfile presentation,
            FishingV2WaterProfile dive,
            FishingV2DivePresentationTiming timing)
        {
            _gameplay = gameplay;
            _presentation = presentation;
            _dive = dive;
            if (timing != null) _timing = timing;

            if (_awaitingOpeningCast)
            {
                SetBaseProfile(_presentation);
            }
            else if (_playing)
            {
                Evaluate();
            }
            else if (Phase == FishingV2SessionPresentationPhase.Gameplay)
            {
                SetBaseProfile(_gameplay);
            }
            else if (Phase == FishingV2SessionPresentationPhase.AboveWater)
            {
                SetBaseProfile(_presentation);
            }
            else
            {
                SetBaseProfile(_dive);
            }
        }

        /// <summary>
        /// 세션을 물 밖 상태로 연다. 시계는 멈춰 있고, 플레이어가 찌를 던질 때까지 기다린다.
        /// 이 구간에서도 물고기 시뮬레이션은 그대로 돌아가므로 연못을 보면서 자리를 고를 수 있다.
        /// </summary>
        public void BeginOpening()
        {
            _debugHold = false;
            _playing = false;
            _awaitingOpeningCast = true;
            _time = 0f;
            _pulseAge = 99f;
            Phase = FishingV2SessionPresentationPhase.AboveWater;
            SetBaseProfile(_presentation);
            HudAlpha = 0f;
        }

        /// <summary>
        /// 착수처럼 순간적인 사건이 수면을 흔든다. 프로파일 위에 얹히는 일시적 가산이라
        /// 상태 전환과 독립이고, 나중에 BiteFocus/BigCatch도 같은 통로를 쓸 수 있다.
        /// </summary>
        public void PushSurfacePulse(float strength)
        {
            _pulseAge = 0f;
            _pulseStrength = Mathf.Clamp01(strength);
            Current = ApplyPulse(_baseProfile);
        }

        private void SetBaseProfile(FishingV2WaterProfile profile)
        {
            _baseProfile = profile;
            Current = ApplyPulse(profile);
        }

        /// <summary>
        /// 빠르게 솟았다 길게 잦아드는 포락선. 대칭이면 물이 튄 게 아니라 값이 한 번
        /// 오르내린 것으로 보인다.
        /// </summary>
        private FishingV2WaterProfile ApplyPulse(FishingV2WaterProfile profile)
        {
            if (_pulseAge >= PulseDuration || _pulseStrength <= 0.0001f)
            {
                return profile;
            }

            float t = _pulseAge / PulseDuration;
            float attack = Mathf.Clamp01(t / 0.10f);
            float decay = (1f - t) * (1f - t);
            float envelope = attack * decay * _pulseStrength;

            // 수면이 거칠어지는 것 자체는 여기서 하지 않는다. 전역으로 올리면 화면 구석의
            // 잔잔하던 물까지 같이 파도쳐서 "안 보이던 파도가 갑자기 나타난" 것으로 보인다.
            // 착수 교란은 셰이더의 SplashChop이 착수 지점 주변에만 얹는다.
            //
            // 여기 남는 것은 실제로 찌에 붙어 있는 것들뿐이다. 물리적 파문은 찌가 만드는
            // 것이고, 굴절 속도는 그 자리의 물이 잠깐 빨라지는 것이라 국소 교란과 결이 같다.
            profile.PhysicalRippleStrength += 0.55f * envelope;
            profile.RefractionSpeed += 0.10f * envelope;
            return profile;
        }

        /// <summary>첫 찌가 수면에 닿았다. 여기서부터 잠수 시간축이 흐른다.</summary>
        public void ReleaseDive()
        {
            if (!_awaitingOpeningCast)
            {
                return;
            }

            _awaitingOpeningCast = false;
            _debugHold = false;
            _playing = true;
            _time = 0f;
            Evaluate();
        }

        /// <summary>세션 시작 연출을 처음부터 재생한다.</summary>
        public void PlayIntro()
        {
            _debugHold = false;
            _awaitingOpeningCast = false;
            _playing = true;
            _time = 0f;
            Evaluate();
        }

        public void ForceGameplay()
        {
            _playing = false;
            _debugHold = false;
            _awaitingOpeningCast = false;
            _time = _timing.TotalSeconds;
            Phase = FishingV2SessionPresentationPhase.Gameplay;
            SetBaseProfile(_gameplay);
            HudAlpha = 1f;
        }

        public void ForcePresentation()
        {
            _playing = false;
            _debugHold = true;
            _awaitingOpeningCast = false;
            _time = 0f;
            Phase = FishingV2SessionPresentationPhase.AboveWater;
            SetBaseProfile(_presentation);
            HudAlpha = 0f;
        }

        public void ForceDivePeak()
        {
            _playing = false;
            _debugHold = true;
            _awaitingOpeningCast = false;
            _time = _timing.CrossingTime;
            Phase = FishingV2SessionPresentationPhase.Diving;
            SetBaseProfile(_dive);
            HudAlpha = 0f;
        }

        /// <summary>
        /// 시간축의 한 지점을 직접 지정한다. 프레임 시퀀스 캡처가 프레임률에 의존하지 않게 하려면
        /// 이쪽을 쓴다.
        /// </summary>
        public void ScrubTo(float time)
        {
            _playing = false;
            _debugHold = true;
            _awaitingOpeningCast = false;
            _time = Mathf.Clamp(time, 0f, _timing.TotalSeconds);
            Evaluate();
        }

        public void Tick(float dt)
        {
            _pulseAge += Mathf.Max(0f, dt);
            CrossedSurfaceThisTick = false;
            float previousTime = _time;

            if (!_playing)
            {
                // 재생이 끝난 뒤에도 펄스는 살아 있어야 한다 — 착수는 게임플레이 중에도 있다.
                Current = ApplyPulse(_baseProfile);
                return;
            }

            _time += Mathf.Max(0f, dt);
            float crossing = _timing.CrossingTime;
            if (previousTime < crossing && _time >= crossing)
            {
                CrossedSurfaceThisTick = true;
            }

            if (_time >= _timing.TotalSeconds)
            {
                _time = _timing.TotalSeconds;
                _playing = false;
                Phase = FishingV2SessionPresentationPhase.Gameplay;
                SetBaseProfile(_gameplay);
                HudAlpha = 1f;
                return;
            }

            Evaluate();
        }

        private void Evaluate()
        {
            float aboveWaterEnd = _timing.AboveWaterSeconds;
            float diveEnd = _timing.DiveEnd;
            float crossing = _timing.CrossingTime;

            // 광학은 수면을 뚫기 전까지 거의 변하지 않는다. 물 밖에서 가까이 다가간다고
            // 물이 맑아지지는 않는다 — 맑아지는 것은 통과한 뒤의 일이다.
            FishingV2WaterProfile optics;
            if (_time < crossing)
            {
                Phase = _time < aboveWaterEnd
                    ? FishingV2SessionPresentationPhase.AboveWater
                    : FishingV2SessionPresentationPhase.Diving;
                float t = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0f, crossing, _time));
                optics = FishingV2WaterProfile.Blend(_presentation, _dive, t);
            }
            else
            {
                Phase = _time < diveEnd
                    ? FishingV2SessionPresentationPhase.Diving
                    : FishingV2SessionPresentationPhase.Gameplay;
                // 통과 뒤에는 항목마다 다른 시점에 정리된다. 전부 같은 곡선이면 값 하나를
                // 당긴 것으로 보인다 — 수면이 먼저 사라지고, 물이 맑아지고, 바닥 빛이 온다.
                float t = Mathf.InverseLerp(crossing, _timing.TotalSeconds, _time);
                float surfaceT = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.00f, 0.42f, t));
                float volumeT = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.14f, 0.78f, t));
                float floorT = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.34f, 1.00f, t));
                optics = FishingV2WaterProfile.BlendStaggered(_dive, _gameplay, surfaceT, volumeT, floorT);
            }

            // 카메라는 클릭한 순간부터 끊김 없이 하나의 곡선을 탄다. 찌가 나는 동안 이미
            // 내려오기 시작해야 착수 시점에 렌즈가 수면에 가깝고, 죽은 구간도 안 생긴다.
            float cameraT;
            if (_time < aboveWaterEnd)
            {
                cameraT = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0f, aboveWaterEnd, _time)) * 0.34f;
            }
            else if (_time < diveEnd)
            {
                cameraT = 0.34f + 0.66f * Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(aboveWaterEnd, diveEnd, _time));
            }
            else
            {
                cameraT = 1f;
            }

            float cameraHeight = Mathf.Lerp(_presentation.CameraHeight, _dive.CameraHeight, cameraT);
            Vector2 cameraShift = Vector2.Lerp(_presentation.CameraFramingShift, _dive.CameraFramingShift, cameraT);
            if (_time >= diveEnd)
            {
                float settle = _timing.SettleSeconds > 0.0001f
                    ? Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(diveEnd, _timing.TotalSeconds, _time))
                    : 1f;
                cameraHeight = Mathf.Lerp(_dive.CameraHeight, _gameplay.CameraHeight, settle);
                cameraShift = Vector2.Lerp(_dive.CameraFramingShift, _gameplay.CameraFramingShift, settle);
            }

            optics.CameraHeight = cameraHeight;
            optics.CameraFramingShift = cameraShift;
            _baseProfile = optics;
            Current = ApplyPulse(optics);

            // 크롬은 마지막에 올라온다. 통과한 뒤부터 시작해서 안착과 함께 도착한다.
            HudAlpha = _time < diveEnd
                ? 0f
                : Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(diveEnd, _timing.TotalSeconds, _time));
        }
    }
}
