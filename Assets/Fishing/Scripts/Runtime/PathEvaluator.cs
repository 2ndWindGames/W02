using UnityEngine;

namespace Fishing
{
    public enum PathType { Lane, School, HoverDash, Loop }

    /// <summary>
    /// 유영 궤도 평가 — 기획서 §4.
    ///
    /// 전부 순수 함수다. 상태를 두지 않으므로 결정론적이고,
    /// 나중에 자동낚시·리플레이·밸런스 시뮬레이션을 그대로 얹을 수 있다.
    /// 스플라인 에셋은 만들지 않는다. 파라메트릭 함수 + 시드로 충분하다.
    /// </summary>
    public static class PathEvaluator
    {
        /// <summary>Lane / School 개체가 들고 다니는 경로 파라미터.</summary>
        [System.Serializable]
        public struct LaneParams
        {
            public Vector2 origin;     // 경로 원점
            public Vector2 dir;        // 진행 방향 (단위 벡터)
            public float speed;        // 유닛/초
            public float amp;          // 사인 진폭
            public float period;       // 사인 주기(초)
            public float phase;        // 개체별 위상
        }

        /// <summary>
        /// Lane — 화면을 가로지르며 얕게 사인파를 그린다. 예측 최상.
        /// p = origin + dir * v * t + perp * A * sin(2pi t / T + phase)
        /// </summary>
        public static Vector2 Lane(in LaneParams p, float t)
        {
            Vector2 perp = new Vector2(-p.dir.y, p.dir.x);
            float lat = p.amp * Mathf.Sin(Mathf.PI * 2f * t / Mathf.Max(p.period, 0.01f) + p.phase);
            return p.origin + p.dir * (p.speed * t) + perp * lat;
        }

        /// <summary>
        /// Loop — 리사주 8자. 주기적이라 "몇 초 뒤 여기로 돌아온다"가 성립한다.
        /// p = center + (a cos(wt), b sin(2wt))
        /// </summary>
        public static Vector2 Loop(Vector2 center, float a, float b, float period, float phase, float t)
        {
            float w = Mathf.PI * 2f / Mathf.Max(period, 0.01f);
            float ang = w * t + phase;
            return center + new Vector2(a * Mathf.Cos(ang), b * Mathf.Sin(2f * ang));
        }

        /// <summary>
        /// School 추종 개체 — 리더의 진행 방향을 기준으로 한 고정 포메이션 + 미세 노이즈.
        /// 자기 경로 파라미터는 리더가 사라졌을 때의 폴백으로만 쓴다.
        /// </summary>
        public static Vector2 SchoolFollow(Vector2 leaderPos, Vector2 leaderDir, Vector2 formationOffset,
                                           Vector2 noiseSeed, float time)
        {
            float a = Mathf.Atan2(leaderDir.y, leaderDir.x);
            float ca = Mathf.Cos(a), sa = Mathf.Sin(a);
            Vector2 rotated = new Vector2(
                formationOffset.x * ca - formationOffset.y * sa,
                formationOffset.x * sa + formationOffset.y * ca);
            Vector2 noise = new Vector2(
                Mathf.Sin(time * 0.7f + noiseSeed.x) * 0.12f,
                Mathf.Sin(time * 0.9f + noiseSeed.y) * 0.12f);
            return leaderPos + rotated + noise;
        }

        /// <summary>
        /// 화면 밖 랩 — 목표 좌표를 직접 지정한다.
        ///
        /// ⚠️ 경로 원점에 폭을 누적 가산(ox += W)하면 안 된다.
        /// 위치는 다음 프레임에야 갱신되므로 조건이 여러 프레임 참이 되고,
        /// 그때마다 더해져서 물고기가 화면 밖으로 날아간다. (기획서 §11-1)
        /// </summary>
        public static bool WrapOrigin(ref LaneParams p, Vector2 pathPos, Rect water, float margin = 1.8f)
        {
            float nx = pathPos.x, ny = pathPos.y;
            bool moved = false;

            if (pathPos.x < water.xMin - margin) { nx = water.xMax + margin - 0.3f; moved = true; }
            else if (pathPos.x > water.xMax + margin) { nx = water.xMin - margin + 0.3f; moved = true; }

            if (pathPos.y < water.yMin - margin) { ny = water.yMax + margin - 0.3f; moved = true; }
            else if (pathPos.y > water.yMax + margin) { ny = water.yMin - margin + 0.3f; moved = true; }

            if (moved) p.origin += new Vector2(nx - pathPos.x, ny - pathPos.y);
            return moved;
        }
    }
}
