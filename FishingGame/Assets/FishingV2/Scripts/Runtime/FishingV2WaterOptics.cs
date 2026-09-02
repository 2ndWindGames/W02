using UnityEngine;

namespace Fishing.V2
{
    /// <summary>
    /// Legacy scalar water-field helper retained for offline/debug compatibility. The runtime
    /// presentation now uses WaterSurfaceV2's coherent underwater RenderTexture composite;
    /// fish, shadow, and bobber code no longer calls this helper for visual offsets.
    /// </summary>
    public static class FishingV2WaterOptics
    {
        public static Vector2 SampleDisplacement(Vector2 worldPosition, float time, float scale, float speed)
        {
            float safeScale = Mathf.Max(0.05f, scale);
            float safeSpeed = Mathf.Max(0f, speed);
            float t = time * safeSpeed;
            float x = worldPosition.x * 0.42f * safeScale + worldPosition.y * 0.19f * safeScale + t * 0.17f;
            float y = worldPosition.y * 0.54f * safeScale - worldPosition.x * 0.15f * safeScale - t * 0.13f;
            float displacementX = Mathf.Sin(x) + 0.42f * Mathf.Sin(x * 1.73f + y * 0.28f + 0.70f);
            float displacementY = Mathf.Cos(y) + 0.42f * Mathf.Cos(y * 1.61f - x * 0.24f - 0.40f);
            return new Vector2(displacementX, displacementY) * 0.5f;
        }

        public static float SampleLightField(Vector2 worldPosition, float time, float scale, float speed)
        {
            float safeScale = Mathf.Max(0.05f, scale);
            float safeSpeed = Mathf.Max(0f, speed);
            float t = time * safeSpeed;
            float phaseA = worldPosition.x * 0.35f * safeScale + worldPosition.y * 0.22f * safeScale + t * 0.16f;
            float phaseB = worldPosition.y * 0.61f * safeScale - worldPosition.x * 0.12f * safeScale - t * 0.11f + 1.70f;
            return Mathf.Clamp01(0.5f + Mathf.Sin(phaseA) * 0.28f + Mathf.Sin(phaseB) * 0.18f);
        }
    }
}
