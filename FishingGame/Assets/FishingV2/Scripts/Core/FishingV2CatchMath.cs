using UnityEngine;

namespace Fishing.V2
{
    /// <summary>
    /// v25 획득 연출에 필요한 순수 2D 계산이다. Bobber/Session이 물 표현 구현과
    /// 얽히지 않도록 접촉·궤적·바운스 수식을 별도 계약으로 둔다.
    /// </summary>
    public static class FishingV2CatchMath
    {
        public static bool TrySweptMouthContact(
            Vector2 previousRoot,
            Vector2 currentRoot,
            float heading,
            float mouthOffset,
            Vector2 bobber,
            float radius,
            out Vector2 contact)
        {
            Vector2 direction = new Vector2(Mathf.Cos(heading), Mathf.Sin(heading));
            Vector2 previousMouth = previousRoot + direction * mouthOffset;
            Vector2 currentMouth = currentRoot + direction * mouthOffset;
            Vector2 movement = currentMouth - previousMouth;
            float movementSquared = movement.sqrMagnitude;
            float hitT = movementSquared > 0.0000001f
                ? Mathf.Clamp01(Vector2.Dot(bobber - previousMouth, movement) / movementSquared)
                : 0f;
            contact = previousMouth + movement * hitT;
            return Vector2.Distance(contact, bobber) <= Mathf.Max(0.001f, radius);
        }

        public static Vector2 InwardNormal(Vector2 from, Vector2 to, Vector2 pondCenter)
        {
            Vector2 delta = to - from;
            if (delta.sqrMagnitude < 0.0000001f)
            {
                return Vector2.up;
            }

            delta.Normalize();
            Vector2 normal = new Vector2(-delta.y, delta.x);
            Vector2 midpoint = (from + to) * 0.5f;
            if (Vector2.Dot(pondCenter - midpoint, normal) < 0f)
            {
                normal = -normal;
            }

            return normal;
        }

        public static float FlightBow(float distance)
        {
            return Mathf.Min(1.15f, Mathf.Max(0f, distance) * 0.16f);
        }

        /// <summary>
        /// s is normalized basket-settle time. The two bounce intervals are explicit so the
        /// second bounce cannot be swallowed by a single decaying envelope.
        /// </summary>
        public static float BasketBounceHeight(
            float s,
            float firstHeight = 0.46f,
            float secondHeight = 0.19f,
            float firstEnd = 0.46f,
            float secondEnd = 0.78f)
        {
            s = Mathf.Clamp01(s);
            firstEnd = Mathf.Clamp(firstEnd, 0.05f, 0.90f);
            secondEnd = Mathf.Clamp(secondEnd, firstEnd + 0.05f, 0.99f);
            if (s < firstEnd)
            {
                return Mathf.Max(0f, firstHeight) * Mathf.Sin(s / firstEnd * Mathf.PI);
            }

            if (s < secondEnd)
            {
                return Mathf.Max(0f, secondHeight) * Mathf.Sin((s - firstEnd) / (secondEnd - firstEnd) * Mathf.PI);
            }

            return 0f;
        }

        public static float LiftScale(float rise, float sink, float liftK = 1.5f)
        {
            rise = Mathf.Clamp(rise, 0f, 4f);
            sink = Mathf.Clamp01(sink);
            liftK = Mathf.Max(0f, liftK);
            float scale = 1f + liftK * (0.30f * Mathf.Min(rise, 1f) + 0.22f * Mathf.Max(rise - 1f, 0f));
            return Mathf.Max(0.02f, scale * (1f - sink));
        }
    }
}
