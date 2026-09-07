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
    /// Captures one reproducible GameView frame for each v25 After-Bite mode. This is an
    /// evidence helper only; it does not alter serialized fish data or production assets.
    /// </summary>
    [InitializeOnLoad]
    public static class FishingV2AfterBiteEvidenceCapture
    {
        private const string ScenePath = "Assets/FishingV2/Scenes/FishingV2Prototype.unity";
        private const string CaptureFolder = "Screenshots/AfterBite";
        private const string RequestKey = "FishingV2.AfterBiteEvidenceCaptureRequested";
        private const double InitialWaitSeconds = 1.0;
        private const double CaptureDelaySeconds = 0.28;
        private const double FinalWaitSeconds = 0.80;

        private static readonly string[] ModeLabels =
        {
            "peel",
            "pass",
            "school",
            "arc",
            "jet"
        };

        // Spot_Beach contains one representative playable species for every mode.
        private static readonly string[] RepresentativeSpecies =
        {
            "salmon",
            "tuna",
            "anchovy",
            "mahi",
            "squid"
        };

        private static readonly AfterBiteMode[] RepresentativeModes =
        {
            AfterBiteMode.Peel,
            AfterBiteMode.Pass,
            AfterBiteMode.School,
            AfterBiteMode.Arc,
            AfterBiteMode.Jet
        };

        private static int _modeIndex;
        private static double _stageStartedAt;
        private static double _captureIssuedAt;
        private static bool _captureIssued;
        private static bool _captureActive;
        private static FishingV2Session _session;
        private static FishAgentV2 _fish;
        private static string _absoluteFolder;
        private static string _reportPath;
        private static readonly List<string> CapturedRecords = new List<string>();

        static FishingV2AfterBiteEvidenceCapture()
        {
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            EditorApplication.update += BootstrapCaptureAfterDomainReload;
        }

        [MenuItem("Fishing V2/Capture After-Bite mode evidence", priority = 44)]
        public static void CaptureAfterBiteModeEvidence()
        {
            Debug.Log("Fishing V2 After-Bite evidence capture requested.");
            if (EditorApplication.isPlaying)
            {
                Debug.LogError("Fishing V2 After-Bite evidence capture requires EditMode before starting.");
                return;
            }

            EditorSceneManager.OpenScene(ScenePath);

            _absoluteFolder = Path.Combine(Application.dataPath, CaptureFolder);
            Directory.CreateDirectory(_absoluteFolder);
            _reportPath = Path.Combine(_absoluteFolder, "afterbite_capture_report.txt");

            // The evidence should show gameplay framing immediately. This is a runtime-only
            // field assignment on the loaded scene object and is not saved back to the scene.
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
                    // Clear a request if the user cancels the PlayMode transition before the
                    // capture bootstrap gets a chance to run.
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
            _modeIndex = 0;
            _stageStartedAt = EditorApplication.timeSinceStartup;
            _captureIssuedAt = 0.0;
            _captureIssued = false;
            _session = null;
            _fish = null;
            CapturedRecords.Clear();
            _absoluteFolder = Path.Combine(Application.dataPath, CaptureFolder);
            Directory.CreateDirectory(_absoluteFolder);
            _reportPath = Path.Combine(_absoluteFolder, "afterbite_capture_report.txt");
            _captureActive = true;
            EditorApplication.update -= Tick;
            EditorApplication.update += Tick;
            Debug.Log("Fishing V2 After-Bite evidence capture entered PlayMode.");
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

            if (_session == null)
            {
                _session = UnityEngine.Object.FindFirstObjectByType<FishingV2Session>();
                if (_session == null)
                {
                    return;
                }
            }

            double now = EditorApplication.timeSinceStartup;
            if (_modeIndex >= ModeLabels.Length)
            {
                if (now - _captureIssuedAt >= FinalWaitSeconds)
                {
                    Finish(true);
                }

                return;
            }

            if (!_captureIssued)
            {
                if (now - _stageStartedAt < InitialWaitSeconds)
                {
                    return;
                }

                _fish = FindRepresentativeFish(RepresentativeSpecies[_modeIndex]);
                if (_fish == null)
                {
                    Debug.LogError("Fishing V2 After-Bite evidence capture could not find " + RepresentativeSpecies[_modeIndex]);
                    Finish(false);
                    return;
                }

                AfterBiteMode expectedMode = RepresentativeModes[_modeIndex];
                if (_fish.Species.AfterBite == null || _fish.Species.AfterBite.Mode != expectedMode)
                {
                    Debug.LogError(
                        "Fishing V2 After-Bite evidence capture profile mismatch for " +
                        RepresentativeSpecies[_modeIndex] + ": expected=" + expectedMode);
                    Finish(false);
                    return;
                }

                // Use a point behind the fish so the profile produces a clear escape vector.
                _fish.BeginAfterBite(_fish.Position - _fish.HeadingVector * 1.0f);
                _stageStartedAt = now;
                _captureIssued = true;
                return;
            }

            if (now - _stageStartedAt < CaptureDelaySeconds)
            {
                return;
            }

            string stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            string fileName = "afterbite_" + ModeLabels[_modeIndex] + "_" + stamp + ".png";
            string absolutePath = Path.Combine(_absoluteFolder, fileName);
            ScreenCapture.CaptureScreenshot(absolutePath, 1);
            CapturedRecords.Add(
                "mode=" + ModeLabels[_modeIndex] +
                ";species=" + RepresentativeSpecies[_modeIndex] +
                ";state=" + _fish.State +
                ";profileMode=" + _fish.CurrentAfterBiteMode +
                ";expectedMode=" + RepresentativeModes[_modeIndex] +
                ";progress=" + _fish.AfterBiteProgress01.ToString("F3") +
                ";path=" + absolutePath.Replace('\\', '/'));
            _captureIssuedAt = now;
            _captureIssued = false;
            _modeIndex++;
            _stageStartedAt = now;
        }

        private static FishAgentV2 FindRepresentativeFish(string speciesId)
        {
            if (_session == null || _session.Fish == null)
            {
                return null;
            }

            for (int i = 0; i < _session.Fish.Count; i++)
            {
                FishAgentV2 fish = _session.Fish[i];
                if (fish != null && fish.Species != null && fish.Species.SpeciesId == speciesId)
                {
                    return fish;
                }
            }

            return null;
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
                string[] report = new string[CapturedRecords.Count + 4];
                report[0] = "Fishing V2 After-Bite evidence capture";
                report[1] = "capturedAt=" + DateTime.Now.ToString("O");
                report[2] = "scene=" + ScenePath;
                report[3] = "modes=" + string.Join(",", ModeLabels);
                for (int i = 0; i < CapturedRecords.Count; i++)
                {
                    report[i + 4] = CapturedRecords[i];
                }

                File.WriteAllLines(_reportPath, report);
                Debug.Log("Fishing V2 After-Bite evidence capture finished. report=" + _reportPath);
            }

            if (EditorApplication.isPlaying)
            {
                EditorApplication.isPlaying = false;
            }
        }
    }
}
#endif
