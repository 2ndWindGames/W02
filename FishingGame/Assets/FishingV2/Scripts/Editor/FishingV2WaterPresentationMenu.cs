#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
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

        [MenuItem("Fishing V2/Water Presentation/Begin opening (await cast)", priority = 63)]
        private static void BeginOpening()
        {
            FishingV2Session session = FindSession();
            if (session == null) return;
            session.BeginOpeningPresentation();
            Log(session);
        }

        [MenuItem("Fishing V2/Water Presentation/Play dive transition", priority = 64)]
        private static void PlayDive()
        {
            FishingV2Session session = FindSession();
            if (session == null) return;
            session.PlayDiveTransition();
            Log(session);
        }

        [MenuItem("Fishing V2/Water Presentation/Restart session (replay opening)", priority = 70)]
        private static void RestartSession()
        {
            FishingV2Session session = FindSession();
            if (session == null) return;
            if (!Application.isPlaying)
            {
                Debug.LogWarning("Fishing V2: 재생 중에만 세션을 다시 열 수 있다.");
                return;
            }

            session.BeginSession();
            Debug.Log("Fishing V2 session restarted — 수면을 클릭하면 인트로가 다시 재생된다.");
        }

        /// <summary>
        /// 코드 기본값이 그대로 들어간 연출 애셋을 만들어 씬의 세션에 물린다.
        /// 애셋을 손으로 만들고 필드를 채우는 마찰을 없애려는 것이다.
        /// </summary>
        [MenuItem("Fishing V2/Water Presentation/Create tuning asset", priority = 71)]
        private static void CreateTuningAsset()
        {
            const string folder = "Assets/FishingV2/Data";
            string path = AssetDatabase.GenerateUniqueAssetPath(folder + "/FishingV2WaterPresentation.asset");

            var asset = ScriptableObject.CreateInstance<FishingV2WaterPresentationAsset>();
            FishingV2Session session = Object.FindFirstObjectByType<FishingV2Session>();
            var settings = FishingV2PresentationSettings.For(
                session != null ? session.PresentationVariant : FishingV2PresentationVariant.CalmObservation);
            asset.PopulateFromCodeDefaults(settings);

            AssetDatabase.CreateAsset(asset, path);
            AssetDatabase.SaveAssets();

            if (session != null)
            {
                Undo.RecordObject(session, "Assign water presentation asset");
                session.WaterPresentationAsset = asset;
                EditorUtility.SetDirty(session);
                EditorSceneManager.MarkSceneDirty(session.gameObject.scene);
            }

            Selection.activeObject = asset;
            EditorGUIUtility.PingObject(asset);
            Debug.Log("Fishing V2 water presentation asset created at " + path +
                (session != null ? " and assigned to the scene session." : ". No session in scene to assign it to."));
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
