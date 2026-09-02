using UnityEngine;

namespace Fishing.V2
{
    /// <summary>
    /// Lane/Loop 경로의 순수 수식 모음이다.
    /// Hover-Dash는 상태를 적분하는 법칙이라 FishAgentV2 안에서 별도로 처리한다.
    /// </summary>
    public static class PathEvaluatorV2
    {
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
            if (position.x < pond.xMin - 1.8f)
            {
                next.x = pond.xMax + 1.5f;
                moved = true;
            }
            else if (position.x > pond.xMax + 1.8f)
            {
                next.x = pond.xMin - 1.5f;
                moved = true;
            }

            if (position.y < pond.yMin - 1.8f)
            {
                next.y = pond.yMax + 1.5f;
                moved = true;
            }
            else if (position.y > pond.yMax + 1.8f)
            {
                next.y = pond.yMin - 1.5f;
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
