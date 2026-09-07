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
            RunV25AllSpeciesSimulationInternal(false);
        }

        [MenuItem("Fishing V2/Run v25 After-Bite release simulation", priority = 43)]
        public static void RunV25AfterBiteReleaseSimulation()
        {
            RunV25AllSpeciesSimulationInternal(true);
        }

        private static void RunV25AllSpeciesSimulationInternal(bool exerciseAfterBite)
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
            // The normal all-species gate keeps the catch/reward path intact. The dedicated
            // After-Bite menu below opts one school species into the release path so this
            // state is also exercised without changing the default simulation contract.
            if (exerciseAfterBite)
            {
                session.ReleaseSpecies.Add("anchovy");
            }
            session.InitializeRuntime();
            session.BeginSession();
            FishingV2TuningAsset tuning = session.Tuning;
            int initialFishCount = session.Fish != null ? session.Fish.Count : 0;

            _errors = 0;
            Application.logMessageReceived += OnLogMessage;
            Dictionary<int, Vector2> previousPositions = new Dictionary<int, Vector2>();
            Dictionary<int, FishAgentV2> previousAgents = new Dictionary<int, FishAgentV2>();
            float maxVisibleStep = 0f;
            int visibleLargeJumps = 0;
            string maxStepInfo = "none";
            int strikeFrames = 0;
            int hookedFrames = 0;
            int afterBiteFrames = 0;
            for (int frame = 0; frame < 5400; frame++)
            {
                if (frame == 30)
                {
                    session.CastAtPondPoint(new Vector2(0f, -1.2f));
                }

                session.SimulateStep(1f / 60f);
                HashSet<int> seenIds = new HashSet<int>();
                if (session.Fish != null)
                {
                    for (int i = 0; i < session.Fish.Count; i++)
                    {
                        FishAgentV2 fish = session.Fish[i];
                        if (fish == null) continue;

                        int id = fish.GetInstanceID();
                        seenIds.Add(id);
                        Vector2 previous;
                        FishAgentV2 previousAgent;
                        bool sameAgent = previousAgents.TryGetValue(id, out previousAgent) &&
                            object.ReferenceEquals(previousAgent, fish);
                        if (!sameAgent || !previousPositions.TryGetValue(id, out previous))
                        {
                            previous = fish.PreviousPosition;
                        }

                        Vector2 current = fish.Position;
                        float step = Vector2.Distance(previous, current);
                        bool visible = session.Pond.Contains(previous) && session.Pond.Contains(current);
                        if (visible && step > maxVisibleStep)
                        {
                            maxVisibleStep = step;
                            maxStepInfo = (fish.Species != null ? fish.Species.SpeciesId : "unknown") +
                                " state=" + fish.State +
                                " id=" + id +
                                " frame=" + frame +
                                " from=" + previous +
                                " to=" + current +
                                " agentPrevious=" + fish.PreviousPosition;
                        }
                        if (visible && step > 0.75f) visibleLargeJumps++;
                        if (fish.State == FishState.Strike) strikeFrames++;
                        if (fish.IsHooked) hookedFrames++;
                        if (fish.State == FishState.AfterBite) afterBiteFrames++;
                        previousPositions[id] = current;
                        previousAgents[id] = fish;
                    }
                }

                List<int> staleIds = new List<int>();
                foreach (int id in previousPositions.Keys)
                {
                    if (!seenIds.Contains(id)) staleIds.Add(id);
                }
                for (int i = 0; i < staleIds.Count; i++)
                {
                    previousPositions.Remove(staleIds[i]);
                    previousAgents.Remove(staleIds[i]);
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
            bool hookPathObserved = strikeFrames > 0 && hookedFrames > 0;
            bool afterBiteObserved = !exerciseAfterBite || afterBiteFrames > 0;
            int tallyCount = 0;
            foreach (int count in session.Caught.Values) tallyCount += count;
            bool arrivalAccounting = session.CatchFlightArrivals == tallyCount;
            bool respawnAccounting = session.SpawnedFishCount - initialFishCount == session.RespawnsSpawned &&
                session.RespawnsSpawned <= session.RespawnsScheduled;

            Application.logMessageReceived -= OnLogMessage;
            Debug.Log("Fishing V2 v25 all-species simulation finished. errors=" + _errors +
                ", fish=" + fishCount +
                ", validationFish=" + hasValidationFish +
                ", maxVisibleStep=" + maxVisibleStep.ToString("F4") +
                " (" + maxStepInfo + ")" +
                ", visibleLargeJumps=" + visibleLargeJumps +
                ", strikeFrames=" + strikeFrames +
                ", hookedFrames=" + hookedFrames +
                ", afterBiteFrames=" + afterBiteFrames +
                ", afterBiteTest=" + exerciseAfterBite +
                ", arrivals=" + session.CatchFlightArrivals +
                ", tally=" + tallyCount +
                ", respawns=" + session.RespawnsSpawned + "/" + session.RespawnsScheduled +
                ", spawned=" + session.SpawnedFishCount + "/initial=" + initialFishCount +
                ", score=" + session.Score);

            if (!hasFish) Debug.LogError("Fishing V2 v25 simulation did not retain any active fish.");
            if (!hasValidationFish) Debug.LogError("Fishing V2 v25 simulation did not spawn a validation-only species.");
            if (!movementStable) Debug.LogError("Fishing V2 v25 simulation found a visible fish jump above 0.75 world units.");
            if (!hookPathObserved) Debug.LogError("Fishing V2 v25 simulation did not observe both Strike and Hooked states.");
            if (!afterBiteObserved) Debug.LogError("Fishing V2 v25 After-Bite simulation did not observe the release path.");
            if (!arrivalAccounting) Debug.LogError("Fishing V2 v25 simulation catch tally did not match arrival callbacks.");
            if (!respawnAccounting) Debug.LogError("Fishing V2 v25 simulation respawn accounting was not one-for-one.");

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
                    float.IsNaN(fish.VisualDepth01) || float.IsInfinity(fish.VisualDepth01) ||
                    float.IsNaN(fish.MouthPosition.x) || float.IsNaN(fish.MouthPosition.y) ||
                    float.IsInfinity(fish.MouthPosition.x) || float.IsInfinity(fish.MouthPosition.y))
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
