using UnityEngine;

namespace Fishing
{
    /// <summary>
    /// 어종에 속하지 않는 전역 상수. 기획서 §7 공통 · §11.
    /// 전부 웹 프로토타입에서 세션을 수십 번 돌려 얻은 실측 기준값이다.
    /// </summary>
    public static class FishingTuning
    {
        // ── 인지 / 관심 ────────────────────────────────────────────
        /// <summary>인지 반경 = 놀람 반경 x 이 값. 낮출수록 찌 위치 선택이 중요해진다.</summary>
        public const float NoticeMultiplier = 1.9f;
        /// <summary>호기심 판정 주기(초).</summary>
        public const float CuriosityTick = 1f;
        /// <summary>발견 동작 길이(초) — 궤도를 멈추고 머리를 홱 트는 구간. §5-1</summary>
        public const float NoticeDuration = 0.30f;
        /// <summary>발견 동작 중 선회율 배율.</summary>
        public const float NoticeTurnBoost = 3f;
        /// <summary>발견 동작 중 이동 속도 배율(접근 속도 대비).</summary>
        public const float NoticeSpeedScale = 0.35f;
        /// <summary>목표가 이 거리 안이면 선회율을 올린다. 미사일이 목표를 맴도는 문제 방지.</summary>
        public const float CloseTurnBoostRange = 1.6f;
        public const float CloseTurnBoostMax = 1.6f;
        /// <summary>접근이 이 시간을 넘기면 포기하고 궤도 복귀(초).</summary>
        public const float ApproachTimeout = 20f;
        /// <summary>도착 판정 거리(유닛).</summary>
        public const float ArriveRadius = 0.5f;
        /// <summary>자리가 차 있어서 돌아섰을 때의 재관심 쿨다운(초).</summary>
        public const float RejectedCooldown = 3f;

        // ── 놀람 ──────────────────────────────────────────────────
        /// <summary>놀람 임펄스 크기 (놀람 반경 대비).</summary>
        public const float StartleImpulse = 0.45f;
        /// <summary>⚠️ 누적 오프셋 상한 (놀람 반경 대비). 이게 없으면 연속 착수 시 화면 밖으로 날아간다.</summary>
        public const float StartleOffsetCap = 0.85f;
        /// <summary>궤도 이탈 오프셋의 절대 상한(유닛). 범용 안전장치.</summary>
        public const float MaxPathOffset = 3.5f;
        /// <summary>오프셋 감쇠 — 1초에 이 비율만 남는다.</summary>
        public const float OffsetDecayPerSecond = 0.06f;
        /// <summary>놓아줌(Release) 시 놀람 배율.</summary>
        public const float ReleaseStartleScale = 0.5f;

        // ── 재착수 페널티 — §1-2 ──────────────────────────────────
        /// <summary>이 시간 안에 다시 던지면 연속으로 친다(초).</summary>
        public const float RecastSpamWindow = 3f;
        /// <summary>연속 재착수 시 놀람 지속 배율 상한.</summary>
        public const float RecastTimeMulMax = 2.5f;
        /// <summary>⚠️ 반경은 조금만 키운다. 2.5배로 키우면 대물 반경이 연못 절반이 된다.</summary>
        public const float RecastRadiusMulMax = 1.35f;

        // ── 유인 — §5-0 ──────────────────────────────────────────
        /// <summary>직전 착수로부터 이 시간이 지나야 유인이 완전히 회복된다(초).</summary>
        public const float LureRecovery = 2.5f;
        /// <summary>연속 착수 시 유인 확률 하한 배율.</summary>
        public const float LureFatigueFloor = 0.55f;
        /// <summary>이 값 이상 회복됐을 때만 "최소 1마리 보장"이 작동한다.</summary>
        public const float LureGuaranteeThreshold = 0.6f;

        // ── 찌 ───────────────────────────────────────────────────
        public const float ReelDuration = 0.3f;
        public const float CastDuration = 0.3f;
        /// <summary>무는 위치 = 찌에서 이만큼 물러난 곳. 큰 물고기일수록 뒤에서 문다.</summary>
        public const float BiteBackBase = 0.16f;
        public const float BiteBackPerLength = 0.52f;

        // ── 획득 연출 — §6-5 ──────────────────────────────────────
        public const float CatchLiftDuration = 0.30f;
        public const float CatchFlyDuration = 0.55f;
        public const float CatchArcHeight = 1.7f;

        // ── 파동 셰이더 — §6-1 ────────────────────────────────────
        /// <summary>스펙의 상대값(1.0/0.6/0.1/1.4)을 실제 UV 오프셋으로 환산하는 계수.
        /// 그대로 쓰면 몸이 바나나처럼 휜다. 프로토타입 실측값.</summary>
        public const float WaveAmpScale = 0.40f;

        // ── 프레임 ────────────────────────────────────────────────
        /// <summary>프레임 드랍 시 dt가 튀어 물고기가 순간이동하는 것을 막는다.</summary>
        public const float MaxDeltaTime = 0.05f;

        public static float Dt => Mathf.Min(Time.deltaTime, MaxDeltaTime);
    }
}
