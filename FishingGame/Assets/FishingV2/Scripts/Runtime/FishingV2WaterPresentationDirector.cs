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
        [Tooltip("물 밖에서 수면을 내려다보는 구간.")]
        [Range(0f, 2f)] public float AboveWaterSeconds = 0.70f;
        [Tooltip("수면을 지나 내려가는 구간. 천천히 잠기는 것이 요점이라 2초 이상 잡는다.")]
        [Range(0.2f, 4f)] public float DiveSeconds = 2.40f;
        [Tooltip("카메라 framing이 gameplay 위치로 마저 내려앉는 구간. 광학은 이미 Gameplay다.")]
        [Range(0f, 1.5f)] public float SettleSeconds = 0.40f;
        [Tooltip("Dive 구간 안에서 수면을 지나는 지점. 그 뒤가 길어야 '가라앉는다'로 읽힌다.")]
        [Range(0.1f, 0.8f)] public float PeakFraction = 0.40f;

        public float DiveEnd { get { return AboveWaterSeconds + DiveSeconds; } }
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
        // 디버그 홀드는 입력을 잠그지 않는다. A/B 비교 중에 찌를 던져서 physical ripple까지
        // 같이 보려면 잠그면 안 된다.
        private bool _debugHold;

        public FishingV2SessionPresentationPhase Phase { get; private set; } = FishingV2SessionPresentationPhase.Gameplay;
        public FishingV2WaterProfile Current { get; private set; }
        public float Time { get { return _time; } }
        public float TotalSeconds { get { return _timing.TotalSeconds; } }
        public bool IsPlaying { get { return _playing; } }

        /// <summary>연출이 아직 입력을 쥐고 있는가. gameplay 로직 자체는 건드리지 않는다.</summary>
        public bool InputLocked
        {
            get { return _playing && !_debugHold && Phase != FishingV2SessionPresentationPhase.Gameplay; }
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

        /// <summary>세션 시작 연출을 처음부터 재생한다.</summary>
        public void PlayIntro()
        {
            _debugHold = false;
            _playing = true;
            _time = 0f;
            Evaluate();
        }

        public void ForceGameplay()
        {
            _playing = false;
            _debugHold = false;
            _time = _timing.TotalSeconds;
            Phase = FishingV2SessionPresentationPhase.Gameplay;
            Current = _gameplay;
        }

        public void ForcePresentation()
        {
            _playing = false;
            _debugHold = true;
            _time = 0f;
            Phase = FishingV2SessionPresentationPhase.AboveWater;
            Current = _presentation;
        }

        public void ForceDivePeak()
        {
            _playing = false;
            _debugHold = true;
            _time = _timing.AboveWaterSeconds + _timing.DiveSeconds * _timing.PeakFraction;
            Phase = FishingV2SessionPresentationPhase.Diving;
            Current = _dive;
        }

        /// <summary>
        /// 시간축의 한 지점을 직접 지정한다. 프레임 시퀀스 캡처가 프레임률에 의존하지 않게 하려면
        /// 이쪽을 쓴다.
        /// </summary>
        public void ScrubTo(float time)
        {
            _playing = false;
            _debugHold = true;
            _time = Mathf.Clamp(time, 0f, _timing.TotalSeconds);
            Evaluate();
        }

        public void Tick(float dt)
        {
            if (!_playing)
            {
                return;
            }

            _time += Mathf.Max(0f, dt);
            if (_time >= _timing.TotalSeconds)
            {
                _time = _timing.TotalSeconds;
                _playing = false;
                Phase = FishingV2SessionPresentationPhase.Gameplay;
                Current = _gameplay;
                return;
            }

            Evaluate();
        }

        private void Evaluate()
        {
            float aboveWaterEnd = _timing.AboveWaterSeconds;
            float diveEnd = _timing.DiveEnd;

            FishingV2WaterProfile optics;
            if (_time < aboveWaterEnd)
            {
                Phase = FishingV2SessionPresentationPhase.AboveWater;
                optics = _presentation;
            }
            else if (_time < diveEnd)
            {
                Phase = FishingV2SessionPresentationPhase.Diving;
                float dive01 = Mathf.InverseLerp(aboveWaterEnd, diveEnd, _time);
                float peak = Mathf.Clamp(_timing.PeakFraction, 0.05f, 0.95f);
                if (dive01 < peak)
                {
                    // 수면에 닿는 구간. 부드럽게 들어간다 — 여기서 가속을 주면 천천히 잠기는
                    // 인상이 깨지고 값이 한 번 튄 것처럼 보인다.
                    float t = Mathf.InverseLerp(0f, peak, dive01);
                    optics = FishingV2WaterProfile.Blend(_presentation, _dive, Mathf.SmoothStep(0f, 1f, t));
                }
                else
                {
                    // 수면 아래로 내려가는 구간. 전체의 60%를 여기 쓴다 — 반사가 빠지고
                    // 맑기와 바닥 코스틱이 돌아오는 과정 자체가 "들어간다"의 내용이다.
                    float t = Mathf.InverseLerp(peak, 1f, dive01);
                    optics = FishingV2WaterProfile.Blend(_dive, _gameplay, Mathf.SmoothStep(0f, 1f, t));
                }
            }
            else
            {
                Phase = FishingV2SessionPresentationPhase.Gameplay;
                optics = _gameplay;
            }

            // 카메라는 광학과 다른 곡선을 탄다. 광학은 수면을 통과하는 순간 한 번 크게 흔들렸다가
            // 바로 가라앉지만, 카메라는 전체 구간에 걸쳐 한 번만 내려앉아야 한다. 두 곡선을 하나로
            // 묶으면 dive가 끝나는 지점에서 orthographicSize가 눈에 띄게 한 번 튄다.
            float cameraHeight;
            Vector2 cameraShift;
            if (_time < aboveWaterEnd)
            {
                cameraHeight = _presentation.CameraHeight;
                cameraShift = _presentation.CameraFramingShift;
            }
            else if (_time < diveEnd)
            {
                float t = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(aboveWaterEnd, diveEnd, _time));
                cameraHeight = Mathf.Lerp(_presentation.CameraHeight, _dive.CameraHeight, t);
                cameraShift = Vector2.Lerp(_presentation.CameraFramingShift, _dive.CameraFramingShift, t);
            }
            else
            {
                float t = _timing.SettleSeconds > 0.0001f
                    ? Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(diveEnd, _timing.TotalSeconds, _time))
                    : 1f;
                cameraHeight = Mathf.Lerp(_dive.CameraHeight, _gameplay.CameraHeight, t);
                cameraShift = Vector2.Lerp(_dive.CameraFramingShift, _gameplay.CameraFramingShift, t);
            }

            optics.CameraHeight = cameraHeight;
            optics.CameraFramingShift = cameraShift;
            Current = optics;
        }
    }
}
