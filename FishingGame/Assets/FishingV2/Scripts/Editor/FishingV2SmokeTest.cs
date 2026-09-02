#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

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
            Scene originalScene = SceneManager.GetActiveScene();
            Scene tempScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            SceneManager.SetActiveScene(tempScene);
            GameObject sessionObject = new GameObject("FishingV2SmokeSession");
            FishingV2Session session = sessionObject.AddComponent<FishingV2Session>();
            session.BeginOnStart = false;
            session.InitializeRuntime();
            session.BeginSession();
            FishingV2TuningAsset tuning = session.Tuning;

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
            int fishCount = hasFish ? session.Fish.Count : 0;
            Application.logMessageReceived -= OnLogMessage;
            Object.DestroyImmediate(sessionObject);
            if (tuning != null) Object.DestroyImmediate(tuning);
            SceneManager.SetActiveScene(originalScene);
            EditorSceneManager.CloseScene(tempScene, true);

            Debug.Log("Fishing V2 edit-mode simulation smoke test finished. errors=" + _errors + ", fish=" + fishCount);
            if (!hasFish) Debug.LogError("Fishing V2 smoke test did not retain any active fish.");
        }

        [MenuItem("Fishing V2/Run v25 all-species 90s simulation", priority = 42)]
        public static void RunV25AllSpeciesSimulation()
        {
            Scene originalScene = SceneManager.GetActiveScene();
            Scene tempScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            SceneManager.SetActiveScene(tempScene);
            GameObject sessionObject = new GameObject("FishingV2V25ValidationSession");
            FishingV2Session session = sessionObject.AddComponent<FishingV2Session>();
            session.BeginOnStart = false;

            List<FishSpeciesConfig> configs = FishingV2Catalog.CreateValidationDefaults();
            FishingSpotAsset spot = ScriptableObject.CreateInstance<FishingSpotAsset>();
            FishSpeciesAsset[] assets = new FishSpeciesAsset[configs.Count];
            for (int i = 0; i < configs.Count; i++)
            {
                assets[i] = ScriptableObject.CreateInstance<FishSpeciesAsset>();
                assets[i].Data = configs[i];
            }

            spot.SpotId = "validation_all_species_runtime";
            spot.DisplayName = "검증 — 전체 어종 런타임";
            // The test still advances exactly 90 seconds, but the session must not expire on
            // the final frame and write PlayerData while the editor is being inspected.
            spot.SessionSeconds = 600f;
            spot.Species = assets;
            session.Spot = spot;
            session.InitializeRuntime();
            session.BeginSession();
            FishingV2TuningAsset tuning = session.Tuning;

            _errors = 0;
            Application.logMessageReceived += OnLogMessage;
            Dictionary<int, Vector2> previousPositions = new Dictionary<int, Vector2>();
            float maxVisibleStep = 0f;
            int visibleLargeJumps = 0;
            for (int frame = 0; frame < 5400; frame++)
            {
                if (frame == 30)
                {
                    session.CastAtPondPoint(new Vector2(0f, -1.2f));
                }

                session.SimulateStep(1f / 60f);
                if (session.Fish != null)
                {
                    for (int i = 0; i < session.Fish.Count; i++)
                    {
                        FishAgentV2 fish = session.Fish[i];
                        if (fish == null) continue;

                        int id = fish.GetInstanceID();
                        Vector2 previous;
                        if (!previousPositions.TryGetValue(id, out previous))
                        {
                            previous = fish.PreviousPosition;
                        }

                        Vector2 current = fish.Position;
                        float step = Vector2.Distance(previous, current);
                        bool visible = session.Pond.Contains(previous) && session.Pond.Contains(current);
                        if (visible && step > maxVisibleStep) maxVisibleStep = step;
                        if (visible && step > 0.75f) visibleLargeJumps++;
                        previousPositions[id] = current;
                    }
                }

                if (!AreFinite(session))
                {
                    _errors++;
                    Debug.LogError("Fishing V2 v25 simulation found a non-finite fish value at frame " + frame);
                    break;
                }
            }

            bool hasFish = session.Fish != null && session.Fish.Count > 0;
            int fishCount = hasFish ? session.Fish.Count : 0;
            bool hasValidationFish = false;
            if (session.Fish != null)
            {
                for (int i = 0; i < session.Fish.Count; i++)
                {
                    FishAgentV2 fish = session.Fish[i];
                    if (fish != null && fish.Species != null && fish.Species.ValidationOnly)
                    {
                        hasValidationFish = true;
                        break;
                    }
                }
            }

            bool movementStable = maxVisibleStep <= 0.75f && visibleLargeJumps == 0;

            Application.logMessageReceived -= OnLogMessage;
            Debug.Log("Fishing V2 v25 all-species simulation finished. errors=" + _errors +
                ", fish=" + fishCount +
                ", validationFish=" + hasValidationFish +
                ", maxVisibleStep=" + maxVisibleStep.ToString("F4") +
                ", visibleLargeJumps=" + visibleLargeJumps +
                ", score=" + session.Score);

            if (!hasFish) Debug.LogError("Fishing V2 v25 simulation did not retain any active fish.");
            if (!hasValidationFish) Debug.LogError("Fishing V2 v25 simulation did not spawn a validation-only species.");
            if (!movementStable) Debug.LogError("Fishing V2 v25 simulation found a visible fish jump above 0.75 world units.");

            Object.DestroyImmediate(sessionObject);
            Object.DestroyImmediate(spot);
            for (int i = 0; i < assets.Length; i++) Object.DestroyImmediate(assets[i]);
            if (tuning != null) Object.DestroyImmediate(tuning);
            SceneManager.SetActiveScene(originalScene);
            EditorSceneManager.CloseScene(tempScene, true);
        }

        private static bool AreFinite(FishingV2Session session)
        {
            if (session == null || session.Fish == null) return true;
            for (int i = 0; i < session.Fish.Count; i++)
            {
                FishAgentV2 fish = session.Fish[i];
                if (fish == null) continue;
                Vector2 p = fish.Position;
                if (float.IsNaN(p.x) || float.IsNaN(p.y) || float.IsInfinity(p.x) || float.IsInfinity(p.y) ||
                    float.IsNaN(fish.HeadingRadians) || float.IsInfinity(fish.HeadingRadians) ||
                    float.IsNaN(fish.SpeedNow) || float.IsInfinity(fish.SpeedNow) ||
                    float.IsNaN(fish.VisualDepth01) || float.IsInfinity(fish.VisualDepth01))
                {
                    return false;
                }
            }

            return true;
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
