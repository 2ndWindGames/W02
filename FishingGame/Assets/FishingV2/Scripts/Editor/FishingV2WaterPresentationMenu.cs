#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace Fishing.V2.EditorTools
{
    /// <summary>
    /// A/B 비교용 디버그 컨트롤. Gameplay와 Presentation을 한 클릭으로 오가야
    /// "이 수면이 정말 더 강한가"를 눈으로 판정할 수 있다.
    /// 재생 중에 눌러야 의미가 있다 — 물 프로파일은 런타임 머티리얼에 발린다.
    /// </summary>
    public static class FishingV2WaterPresentationMenu
    {
        [MenuItem("Fishing V2/Water Presentation/Force Gameplay profile", priority = 60)]
        private static void ForceGameplay()
        {
            FishingV2Session session = FindSession();
            if (session == null) return;
            session.ForceGameplayWaterProfile();
            Log(session);
        }

        [MenuItem("Fishing V2/Water Presentation/Force Presentation profile", priority = 61)]
        private static void ForcePresentation()
        {
            FishingV2Session session = FindSession();
            if (session == null) return;
            session.ForcePresentationWaterProfile();
            Log(session);
        }

        [MenuItem("Fishing V2/Water Presentation/Force Dive peak profile", priority = 62)]
        private static void ForceDivePeak()
        {
            FishingV2Session session = FindSession();
            if (session == null) return;
            session.ForceDiveWaterProfile();
            Log(session);
        }

        [MenuItem("Fishing V2/Water Presentation/Play dive transition", priority = 63)]
        private static void PlayDive()
        {
            FishingV2Session session = FindSession();
            if (session == null) return;
            session.PlayDiveTransition();
            Log(session);
        }

        private static FishingV2Session FindSession()
        {
            FishingV2Session session = Object.FindFirstObjectByType<FishingV2Session>();
            if (session == null)
            {
                Debug.LogWarning("Fishing V2 water presentation: active scene has no FishingV2Session.");
            }

            return session;
        }

        private static void Log(FishingV2Session session)
        {
            Debug.Log(
                "Fishing V2 water profile: " + session.ActiveWaterProfile.DisplayName +
                " (phase " + session.PresentationPhase + ", t=" + session.PresentationTime.ToString("0.00") + ")");
        }
    }
}
#endif
