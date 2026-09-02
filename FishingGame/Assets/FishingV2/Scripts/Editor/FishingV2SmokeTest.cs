#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Fishing.V2.EditorTools
{
    public static class FishingV2SmokeTest
    {
        private const string ScenePath = "Assets/FishingV2/Scenes/FishingV2Prototype.unity";
        private static double _deadline;
        private static double _playStartedAt;
        private static bool _requestedExit;
        private static bool _castSent;
        private static int _errors;

        [MenuItem("Fishing V2/Run smoke test", priority = 40)]
        public static void Run()
        {
            EditorSceneManager.OpenScene(ScenePath);
            _deadline = EditorApplication.timeSinceStartup + 8.0;
            _playStartedAt = 0.0;
            _requestedExit = false;
            _castSent = false;
            _errors = 0;
            Application.logMessageReceived += OnLogMessage;
            EditorApplication.update += Tick;
            EditorApplication.isPlaying = true;
        }

        [MenuItem("Fishing V2/Run edit-mode simulation smoke test", priority = 41)]
        public static void RunEditModeSimulation()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            GameObject sessionObject = new GameObject("FishingV2SmokeSession");
            FishingV2Session session = sessionObject.AddComponent<FishingV2Session>();
            session.BeginOnStart = false;
            session.InitializeRuntime();
            session.BeginSession();

            _errors = 0;
            Application.logMessageReceived += OnLogMessage;
            for (int frame = 0; frame < 600; frame++)
            {
                if (frame == 30)
                {
                    session.CastAtPondPoint(new Vector2(0f, -1.2f));
                }
                session.SimulateStep(1f / 60f);
            }

            bool hasFish = session.Fish != null && session.Fish.Count > 0;
            Application.logMessageReceived -= OnLogMessage;
            Debug.Log("Fishing V2 edit-mode simulation smoke test finished. errors=" + _errors + ", fish=" + (hasFish ? session.Fish.Count : 0));
            if (!hasFish) Debug.LogError("Fishing V2 smoke test did not retain any active fish.");

            // 테스트 씬의 모든 런타임 생성 오브젝트를 저장하지 않고 제거한다.
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            EditorApplication.Exit(_errors == 0 && hasFish ? 0 : 1);
        }

        private static void Tick()
        {
            if (_requestedExit)
            {
                if (!EditorApplication.isPlaying)
                {
                    Finish();
                }
                return;
            }

            if (EditorApplication.isPlaying && _playStartedAt <= 0.0)
            {
                _playStartedAt = EditorApplication.timeSinceStartup;
            }

            if (EditorApplication.isPlaying && !_castSent && EditorApplication.timeSinceStartup - _playStartedAt > 0.5)
            {
                FishingV2Session session = Object.FindFirstObjectByType<FishingV2Session>();
                if (session != null)
                {
                    session.CastAtPondPoint(new Vector2(0f, -1.2f));
                    _castSent = true;
                }
            }

            if (EditorApplication.timeSinceStartup >= _deadline)
            {
                _requestedExit = true;
                EditorApplication.isPlaying = false;
            }
        }

        private static void OnLogMessage(string condition, string stackTrace, LogType type)
        {
            if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
            {
                _errors++;
                Debug.LogError("Fishing V2 smoke test captured an error: " + condition + "\n" + stackTrace);
            }
        }

        private static void Finish()
        {
            EditorApplication.update -= Tick;
            Application.logMessageReceived -= OnLogMessage;
            Debug.Log("Fishing V2 smoke test finished. errors=" + _errors + ", castSent=" + _castSent);
            EditorApplication.Exit(_errors == 0 ? 0 : 1);
        }
    }
}
#endif
