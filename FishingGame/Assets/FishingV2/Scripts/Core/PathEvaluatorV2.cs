using UnityEngine;

namespace Fishing.V2
{
    /// <summary>
    /// Lane/Loop 경로의 순수 수식 모음이다.
    /// Hover-Dash는 상태를 적분하는 법칙이라 FishAgentV2 안에서 별도로 처리한다.
    /// </summary>
    public static class PathEvaluatorV2
    {
        // 랩이 일어나는 지점과 반대편에서 다시 나타나는 지점.
        //
        // 이 여백은 "가장 넓게 보이는 화면보다 바깥"이어야 한다. 물 밖 연출에서 화면은
        // 연못 가장자리 + 5.90까지 보이므로, 그보다 안쪽에서 랩하면 물고기가 화면 한복판에서
        // 사라졌다 나타난다 — 페이드를 아무리 정교하게 걸어도 그건 숨겨지지 않는다.
        //
        // 둘이 거의 같아야 페이드가 양쪽 모두 0에서 끝난다. 예전 값(1.8 나가고 1.5에서 등장)은
        // 등장 쪽이 0.3만큼 안쪽이라 그 자리에서 물고기가 반쯤 보인 채로 튀어나왔다.
        //
        // 이 값을 키우면 Lane 어종의 왕복 구간이 길어져 화면 안에 있는 시간이 줄어든다.
        // FishingV2Session.LaneDensityCompensation이 그만큼 마릿수를 올려 밀도를 유지한다.
        // 축을 나눈 이유: 화면은 가로로 훨씬 넓은데(2.18:1) 여백을 한 값으로 쓰면 세로가
        // 필요 이상으로 커진다. 연못 높이가 9뿐이라 y 여백 6.6은 세로 활동 범위를 2.5배로
        // 부풀리고, 그만큼의 물고기가 영영 화면 밖에서만 돈다.
        public const float WrapExitMarginX = 6.60f;
        public const float WrapReentryMarginX = 6.55f;
        public const float WrapExitMarginY = 2.60f;
        public const float WrapReentryMarginY = 2.55f;
        /// <summary>이 값들이 도입되기 전의 랩 여백. 밀도 보정의 기준이다.</summary>
        public const float LegacyWrapExitMargin = 1.80f;

        public struct LaneState
        {
            public Vector2 Origin;
            public Vector2 Direction;
            public float Phase;
            public float Time;
        }

        public struct LoopState
        {
            public Vector2 Center;
            public float Phase;
            public float Time;
        }

        public static Vector2 EvaluateLane(LaneState state, LanePathSettings settings, float time)
        {
            float period = Mathf.Max(0.05f, settings.Period);
            Vector2 direction = state.Direction.sqrMagnitude > 0.0001f ? state.Direction.normalized : Vector2.right;
            Vector2 perpendicular = new Vector2(-direction.y, direction.x);
            float along = settings.Speed * time;
            float lateral = settings.Amplitude * Mathf.Sin(Mathf.PI * 2f * time / period + state.Phase);
            return state.Origin + direction * along + perpendicular * lateral;
        }

        public static Vector2 EvaluateLoop(LoopState state, LoopPathSettings settings, float time)
        {
            float angularSpeed = Mathf.PI * 2f / Mathf.Max(0.05f, settings.Period);
            float phase = angularSpeed * time + state.Phase;
            return state.Center + new Vector2(
                settings.A * Mathf.Cos(phase),
                settings.B * Mathf.Sin(2f * phase));
        }

        public static Vector2 ReanchorLane(Vector2 current, LaneState previous, LanePathSettings settings, float newPhase)
        {
            Vector2 direction = previous.Direction.sqrMagnitude > 0.0001f ? previous.Direction.normalized : Vector2.right;
            Vector2 perpendicular = new Vector2(-direction.y, direction.x);
            float lateral = settings.Amplitude * Mathf.Sin(newPhase);
            return current - perpendicular * lateral;
        }

        public static bool TryWrapLane(ref LaneState state, ref Vector2 position, ref Vector2 previousPosition, Rect pond)
        {
            Vector2 next = position;
            bool moved = false;
            if (position.x < pond.xMin - WrapExitMarginX)
            {
                next.x = pond.xMax + WrapReentryMarginX;
                moved = true;
            }
            else if (position.x > pond.xMax + WrapExitMarginX)
            {
                next.x = pond.xMin - WrapReentryMarginX;
                moved = true;
            }

            if (position.y < pond.yMin - WrapExitMarginY)
            {
                next.y = pond.yMax + WrapReentryMarginY;
                moved = true;
            }
            else if (position.y > pond.yMax + WrapExitMarginY)
            {
                next.y = pond.yMin - WrapReentryMarginY;
                moved = true;
            }

            if (!moved)
            {
                return false;
            }

            Vector2 delta = next - position;
            state.Origin += delta;
            previousPosition += delta;
            position = next;
            return true;
        }
    }
}
