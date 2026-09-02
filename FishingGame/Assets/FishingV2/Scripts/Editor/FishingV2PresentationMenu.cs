#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Fishing.V2.EditorTools
{
    public static class FishingV2PresentationMenu
    {
        [MenuItem("Fishing V2/Presentation/Use A - Calm Observation", priority = 50)]
        private static void UseCalmObservation()
        {
            SetVariant(FishingV2PresentationVariant.CalmObservation);
        }

        [MenuItem("Fishing V2/Presentation/Use B - Casual Fishing", priority = 51)]
        private static void UseCasualFishing()
        {
            SetVariant(FishingV2PresentationVariant.CasualFishing);
        }

        private static void SetVariant(FishingV2PresentationVariant variant)
        {
            FishingV2Session session = Object.FindFirstObjectByType<FishingV2Session>();
            if (session == null)
            {
                Debug.LogWarning("Fishing V2 presentation: active scene has no FishingV2Session.");
                return;
            }

            Undo.RecordObject(session, "Change Fishing V2 presentation");
            session.PresentationVariant = variant;
            EditorUtility.SetDirty(session);
            EditorSceneManager.MarkSceneDirty(session.gameObject.scene);
            Debug.Log("Fishing V2 presentation set to " + variant + ". Press Play or restart Play Mode to apply it.");
        }
    }
}
#endif
