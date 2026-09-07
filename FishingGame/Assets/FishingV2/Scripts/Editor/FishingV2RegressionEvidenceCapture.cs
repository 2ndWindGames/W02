#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Fishing.V2.EditorTools
{
    /// <summary>
    /// Captures the normal cast, catch-flight, arrival, and respawn path from the prototype
    /// scene. This is an evidence helper only; it does not alter serialized gameplay data.
    /// </summary>
    [InitializeOnLoad]
    public static class FishingV2RegressionEvidenceCapture
    {
        private const string ScenePath = "Assets/FishingV2/Scenes/FishingV2Prototype.unity";
        private const string CaptureFolder = "Screenshots/Regression";
        private const string RequestKey = "FishingV2.RegressionEvidenceCaptureRequested";
        private const double StartupWaitSeconds = 0.90;
        private const double TimeoutSeconds = 25.0;
        private const double FinishWaitSeconds = 0.80;

        private enum CapturePhase
        {
            WaitToCast,
            WaitForWater,
            WaitForCatchFlight,
            WaitForArrival,
            WaitForRespawn,
            Finish
        }

        private static CapturePhase _phase;
        private static double _startedAt;
        private static double _phaseStartedAt;
        private static double _finishStartedAt;
        private static bool _captureActive;
        private static bool _flightCaptured;
        private static bool _arrivalCaptured;
        private static bool _respawnCaptured;
        private static int _initialArrivals;
        private static int _initialRespawns;
        private static FishingV2Session _session;
        private static string _absoluteFolder;
        private static string _reportPath;
        private static readonly List<string> CapturedRecords = new List<string>();

        static FishingV2RegressionEvidenceCapture()
        {
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            EditorApplication.update += BootstrapCaptureAfterDomainReload;
        }

        [MenuItem("Fishing V2/Capture normal regression evidence", priority = 45)]
        public static void CaptureNormalRegressionEvidence()
        {
            Debug.Log("Fishing V2 normal regression evidence capture requested.");
            if (EditorApplication.isPlaying)
            {
                Debug.LogError("Fishing V2 normal regression evidence capture requires EditMode before starting.");
                return;
            }

            EditorSceneManager.OpenScene(ScenePath);
            _absoluteFolder = Path.Combine(Application.dataPath, CaptureFolder);
            Directory.CreateDirectory(_absoluteFolder);
            _reportPath = Path.Combine(_absoluteFolder, "regression_capture_report.txt");

            // Start directly in gameplay framing. This is a runtime-only assignment on the
            // loaded scene object and is deliberately not saved back to the scene asset.
            FishingV2Session sceneSession = UnityEngine.Object.FindFirstObjectByType<FishingV2Session>();
            if (sceneSession != null)
            {
                sceneSession.PlaySessionDivePresentation = false;
            }

            SessionState.SetBool(RequestKey, true);
            EditorApplication.isPlaying = true;
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange change)
        {
            if (change == PlayModeStateChange.EnteredPlayMode)
            {
                BootstrapCaptureAfterDomainReload();
                return;
            }

            if (change == PlayModeStateChange.EnteredEditMode)
            {
                if (_captureActive)
                {
                    Finish(false);
                }
                else
                {
                    SessionState.EraseBool(RequestKey);
                }
            }
        }

        private static void BootstrapCaptureAfterDomainReload()
        {
            if (_captureActive || !SessionState.GetBool(RequestKey, false) || !EditorApplication.isPlaying)
            {
                return;
            }

            SessionState.EraseBool(RequestKey);
            _phase = CapturePhase.WaitToCast;
            _startedAt = EditorApplication.timeSinceStartup;
            _phaseStartedAt = _startedAt;
            _finishStartedAt = 0.0;
            _captureActive = true;
            _flightCaptured = false;
            _arrivalCaptured = false;
            _respawnCaptured = false;
            _initialArrivals = 0;
            _initialRespawns = 0;
            _session = null;
            CapturedRecords.Clear();
            _absoluteFolder = Path.Combine(Application.dataPath, CaptureFolder);
            Directory.CreateDirectory(_absoluteFolder);
            _reportPath = Path.Combine(_absoluteFolder, "regression_capture_report.txt");
            EditorApplication.update -= Tick;
            EditorApplication.update += Tick;
            Debug.Log("Fishing V2 normal regression evidence capture entered PlayMode.");
        }

        private static void Tick()
        {
            if (!_captureActive)
            {
                return;
            }

            if (!EditorApplication.isPlaying)
            {
                Finish(false);
                return;
            }

            double now = EditorApplication.timeSinceStartup;
            if (_session == null)
            {
                _session = UnityEngine.Object.FindFirstObjectByType<FishingV2Session>();
                if (_session != null)
                {
                    _initialArrivals = _session.CatchFlightArrivals;
                    _initialRespawns = _session.RespawnsSpawned;
                }
                else if (now - _startedAt >= TimeoutSeconds)
                {
                    Debug.LogError("Fishing V2 normal regression evidence capture could not find a session.");
                    Finish(false);
                    return;
                }
            }

            if (_session == null)
            {
                return;
            }

            if (now - _startedAt >= TimeoutSeconds && _phase != CapturePhase.Finish)
            {
                Debug.LogError(
                    "Fishing V2 normal regression evidence capture timed out: " +
                    "flight=" + _flightCaptured +
                    ", arrival=" + _arrivalCaptured +
                    ", respawn=" + _respawnCaptured);
                Finish(false);
                return;
            }

            switch (_phase)
            {
                case CapturePhase.WaitToCast:
                    if (now - _phaseStartedAt < StartupWaitSeconds)
                    {
                        return;
                    }

                    _session.CastAtPondPoint(new Vector2(0f, -1.2f));
                    _phase = CapturePhase.WaitForWater;
                    _phaseStartedAt = now;
                    break;

                case CapturePhase.WaitForWater:
                    if (_session.Bobber == null || !_session.Bobber.IsInWater)
                    {
                        return;
                    }

                    Capture("cast");
                    _phase = CapturePhase.WaitForCatchFlight;
                    _phaseStartedAt = now;
                    break;

                case CapturePhase.WaitForCatchFlight:
                    if (_session.ActiveCatchFlightCount > 0)
                    {
                        Capture("catchflight");
                        _flightCaptured = true;
                        _phase = CapturePhase.WaitForArrival;
                        _phaseStartedAt = now;
                        break;
                    }

                    if (_session.CatchFlightArrivals > _initialArrivals)
                    {
                        // Keep the logical path observable even if a render update skipped the
                        // short active-flight window on a slow editor frame.
                        _phase = CapturePhase.WaitForArrival;
                        _phaseStartedAt = now;
                    }
                    break;

                case CapturePhase.WaitForArrival:
                    if (_session.CatchFlightArrivals <= _initialArrivals)
                    {
                        return;
                    }

                    if (!_arrivalCaptured)
                    {
                        Capture("arrival");
                        _arrivalCaptured = true;
                    }

                    _phase = CapturePhase.WaitForRespawn;
                    _phaseStartedAt = now;
                    break;

                case CapturePhase.WaitForRespawn:
                    if (_session.RespawnsSpawned <= _initialRespawns)
                    {
                        return;
                    }

                    if (!_respawnCaptured)
                    {
                        Capture("respawn");
                        _respawnCaptured = true;
                    }

                    _phase = CapturePhase.Finish;
                    _finishStartedAt = now;
                    break;

                case CapturePhase.Finish:
                    if (now - _finishStartedAt >= FinishWaitSeconds)
                    {
                        Finish(_flightCaptured && _arrivalCaptured && _respawnCaptured);
                    }
                    break;
            }
        }

        private static void Capture(string label)
        {
            string stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            string fileName = "regression_" + label + "_" + stamp + ".png";
            string absolutePath = Path.Combine(_absoluteFolder, fileName);
            ScreenCapture.CaptureScreenshot(absolutePath, 1);

            string bobberPhase = _session.Bobber != null ? _session.Bobber.Phase.ToString() : "none";
            string hitSpecies = _session.Bobber != null && _session.Bobber.HitFish != null &&
                _session.Bobber.HitFish.Species != null
                ? _session.Bobber.HitFish.Species.SpeciesId
                : "none";
            CapturedRecords.Add(
                "capture=" + label +
                ";bobber=" + bobberPhase +
                ";hitSpecies=" + hitSpecies +
                ";activeCatchFlights=" + _session.ActiveCatchFlightCount +
                ";arrivals=" + _session.CatchFlightArrivals +
                ";respawns=" + _session.RespawnsSpawned +
                ";path=" + absolutePath.Replace('\\', '/'));
        }

        private static void Finish(bool success)
        {
            if (!_captureActive && !success)
            {
                SessionState.EraseBool(RequestKey);
                return;
            }

            _captureActive = false;
            EditorApplication.update -= Tick;
            SessionState.EraseBool(RequestKey);
            if (success)
            {
                string[] report = new string[CapturedRecords.Count + 8];
                report[0] = "Fishing V2 normal regression evidence capture";
                report[1] = "capturedAt=" + DateTime.Now.ToString("O");
                report[2] = "scene=" + ScenePath;
                report[3] = "flightVisualCaptured=" + _flightCaptured;
                report[4] = "arrivalCaptured=" + _arrivalCaptured;
                report[5] = "respawnCaptured=" + _respawnCaptured;
                report[6] = "captures=" + CapturedRecords.Count;
                report[7] = "---";
                for (int i = 0; i < CapturedRecords.Count; i++)
                {
                    report[i + 8] = CapturedRecords[i];
                }

                File.WriteAllLines(_reportPath, report);
                Debug.Log("Fishing V2 normal regression evidence capture finished. report=" + _reportPath);
            }

            if (EditorApplication.isPlaying)
            {
                EditorApplication.isPlaying = false;
            }
        }
    }
}
#endif
