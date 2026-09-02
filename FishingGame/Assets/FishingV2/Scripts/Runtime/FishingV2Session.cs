using System;
using System.Collections.Generic;
using UnityEngine;

namespace Fishing.V2
{
    /// <summary>
    /// 첫 수직 슬라이스용 세션 오케스트레이터.
    /// 물고기 시뮬레이션은 FishAgentV2에 데이터를 전달하고, 상태 결과만 콜백으로 받는다.
    /// </summary>
    [AddComponentMenu("Fishing V2/Fishing Session")]
    public sealed class FishingV2Session : MonoBehaviour
    {
        // Runtime-only layer used to keep the underwater scene out of the final camera.
        // The visible water composite remains on the default layer, while this layer is
        // rendered once into a transparent RenderTexture and sampled as one optical image.
        private const int UnderwaterRenderLayer = 30;

        [Header("Optional assets")]
        public FishingV2TuningAsset Tuning;
        public FishingSpotAsset Spot;
        public int RandomSeed = 20260901;
        public bool BeginOnStart = true;
        [Header("Presentation comparison")]
        public FishingV2PresentationVariant PresentationVariant = FishingV2PresentationVariant.CalmObservation;
        [Tooltip("Optional visual-only overrides for water layers, fish depth response, and shadow depth influence.")]
        public FishingV2PresentationOverrides PresentationOverrides = new FishingV2PresentationOverrides();
        [Header("Presentation assets")]
        [Tooltip("Replaceable organic substrate source sampled by WaterSurfaceV2. The prototype generator can recreate the local PNG.")]
        public Texture2D BottomSubstrateTexture;

        private readonly List<FishAgentV2> _fish = new List<FishAgentV2>();
        private readonly List<RespawnEntry> _respawns = new List<RespawnEntry>();
        private readonly Dictionary<string, int> _caught = new Dictionary<string, int>();
        private readonly Dictionary<string, Mesh> _meshCache = new Dictionary<string, Mesh>();
        private readonly Dictionary<string, FishSpeciesConfig> _speciesById = new Dictionary<string, FishSpeciesConfig>();
        private readonly HashSet<string> _releaseSpecies = new HashSet<string>();

        private System.Random _random;
        private Rect _pond;
        private float _now;
        private float _timeLeft;
        private float _lastLureTime = -100f;
        private int _score;
        private bool _initialized;
        private bool _running;
        private Transform _fishRoot;
        private BobberV2 _bobber;
        private CatchFlightV2 _catchFlight;
        private Material _fishMaterial;
        private Material _shadowMaterial;
        private Material _waterMaterial;
        private Material _simpleMaterial;
        private Material _bobberRingMaterial;
        private Material _bobberRippleMaterial;
        private LineRenderer _bobberRingLine;
        private LineRenderer _bobberRippleLine;
        private Camera _camera;
        private Camera _underwaterCamera;
        private RenderTexture _underwaterSceneTexture;
        private GameObject _waterObject;
        private GameObject _underwaterBottomObject;
        private Material _underwaterBottomMaterial;
        private int _underwaterTextureWidth;
        private int _underwaterTextureHeight;
        private Transform _environmentRoot;
        private FishingV2PresentationSettings _presentation;
        private bool _standaloneSmoke;
        private bool _standaloneSmokeCastSent;

        private sealed class RespawnEntry
        {
            public FishSpeciesConfig Species;
            public float DueAt;
        }

        public Rect Pond { get { return _pond; } }
        public float TimeLeft { get { return _timeLeft; } }
        public int Score { get { return _score; } }
        public bool IsRunning { get { return _running; } }
        public BobberV2 Bobber { get { return _bobber; } }
        public IReadOnlyList<FishAgentV2> Fish { get { return _fish; } }
        public IReadOnlyDictionary<string, int> Caught { get { return _caught; } }
        public HashSet<string> ReleaseSpecies { get { return _releaseSpecies; } }
        public FishingV2PresentationSettings Presentation { get { return _presentation; } }

        private void Awake()
        {
            string[] commandLine = Environment.GetCommandLineArgs();
            for (int i = 0; i < commandLine.Length; i++)
            {
                if (commandLine[i] == "--fishing-v2-smoke")
                {
                    _standaloneSmoke = true;
                    break;
                }
            }
            InitializeRuntime();
        }

        private void Start()
        {
            if (BeginOnStart)
            {
                BeginSession();
            }
        }

        private void Update()
        {
            if (!_initialized)
            {
                return;
            }

            float dt = Tuning.ClampDelta(Time.deltaTime);
            HandleInput();
            if (_standaloneSmoke && !_standaloneSmokeCastSent && _now > 0.5f)
            {
                CastAtPondPoint(new Vector2(0f, -1.2f));
                _standaloneSmokeCastSent = true;
            }
            SimulateStep(dt);
            if (_standaloneSmoke && _now >= 8f)
            {
                Debug.Log("Fishing V2 standalone smoke finished. fish=" + _fish.Count + ", score=" + _score);
                Application.Quit();
            }
        }

        private void LateUpdate()
        {
            if (!_initialized || _underwaterCamera == null)
            {
                return;
            }

            // Render after simulation/visual updates and before the main camera presents the
            // frame. All underwater actors share this render, so the composite applies one
            // coherent surface deformation instead of nudging each actor independently.
            EnsureUnderwaterRenderTexture();
            SyncUnderwaterCamera();
            float opticalTime = GetOpticalTime();
            if (_waterMaterial != null && _underwaterSceneTexture != null)
            {
                _waterMaterial.SetTexture("_UnderwaterSceneTex", _underwaterSceneTexture);
                if (_waterMaterial.HasProperty("_OpticalTime")) _waterMaterial.SetFloat("_OpticalTime", opticalTime);
            }
            if (_underwaterBottomMaterial != null && _underwaterBottomMaterial.HasProperty("_OpticalTime"))
            {
                _underwaterBottomMaterial.SetFloat("_OpticalTime", opticalTime);
            }
            if (_fishMaterial != null && _fishMaterial.HasProperty("_OpticalTime"))
            {
                _fishMaterial.SetFloat("_OpticalTime", opticalTime);
            }

            // Unity may materialize a LineRenderer instance after its first renderer update.
            // Configure that actual instance immediately before the underwater camera render
            // so vertex alpha survives into the shared RT instead of falling back to opaque
            // URP Unlit defaults.
            if (_bobberRingLine != null) ConfigureTransparentLineMaterial(_bobberRingLine.material);
            if (_bobberRippleLine != null) ConfigureTransparentLineMaterial(_bobberRippleLine.material);

            _underwaterCamera.Render();

            // Keep the actual LineRenderer instances configured after the camera has consumed
            // the RT as well. This is intentionally cheap and avoids a first-frame material
            // materialization restoring URP's opaque defaults.
            if (_bobberRingLine != null) ConfigureTransparentLineMaterial(_bobberRingLine.material);
            if (_bobberRippleLine != null) ConfigureTransparentLineMaterial(_bobberRippleLine.material);
        }

        /// <summary>
        /// Editor smoke test와 헤드리스 밸런스 검증에서 Unity Time에 의존하지 않고 한 스텝을 진행한다.
        /// 입력은 호출자가 CastAtPondPoint로 주입한다.
        /// </summary>
        public void SimulateStep(float dt)
        {
            if (!_initialized)
            {
                InitializeRuntime();
            }

            dt = Tuning.ClampDelta(dt);
            _now += dt;

            if (_running)
            {
                _timeLeft = Mathf.Max(0f, _timeLeft - dt);
                if (_timeLeft <= 0f)
                {
                    EndSession();
                }
            }

            if (_bobber != null) _bobber.Tick(dt, _now);
            if (_catchFlight != null) _catchFlight.Tick(dt);

            for (int i = 0; i < _fish.Count; i++)
            {
                FishAgentV2 fish = _fish[i];
                if (fish != null)
                {
                    fish.Tick(dt, _now, _bobber, _fish, OnFishReachedBobber);
                }
            }

            ProcessRespawns();
        }

        public void InitializeRuntime()
        {
            if (_initialized)
            {
                return;
            }

            if (Tuning == null)
            {
                Tuning = FishingV2TuningAsset.CreateRuntimeDefaults();
            }

            _pond = Tuning.PondRect;
            _presentation = FishingV2PresentationSettings.For(PresentationVariant);
            if (PresentationOverrides != null)
            {
                PresentationOverrides.ApplyTo(ref _presentation);
            }
            _presentation.WaterBottomTexture = BottomSubstrateTexture;
            _random = new System.Random(RandomSeed);
            BuildSpeciesTable();
            EnsureCamera();
            EnsureMaterials();
            EnsureWater();
            EnsureUnderwaterOptics();
            EnsureEnvironment();
            EnsureFishRoot();
            EnsureBobber();
            EnsureCatchFlight();
            CreateBasketMarkers();
            _initialized = true;
        }

        public void BeginSession()
        {
            InitializeRuntime();
            ClearFish();
            _respawns.Clear();
            _caught.Clear();
            foreach (FishSpeciesConfig species in _speciesById.Values)
            {
                _caught[species.SpeciesId] = 0;
            }

            _now = 0f;
            _timeLeft = Spot != null && Spot.SessionSeconds > 0f ? Spot.SessionSeconds : Tuning.SessionSeconds;
            _lastLureTime = -100f;
            _score = 0;
            _running = true;
            if (_bobber != null)
            {
                _bobber.ClearBite();
            }

            SpawnAllSpecies();
        }
    
        public void EndSession()
        {
            _running = false;
            PlayerDataV2.Instance.RegisterSessionScore(_score);
            PlayerDataV2.Instance.Save();
        }

        public void CastAtWorldPoint(Vector3 worldPoint)
        {
            Vector2 position = new Vector2(worldPoint.x, worldPoint.y);
            CastAtPondPoint(position);
        }

        public void CastAtPondPoint(Vector2 position)
        {
            if (!_running || _bobber == null)
            {
                return;
            }

            if (_bobber.HitFish != null)
            {
                _bobber.ReelImmediately();
                return;
            }

            if (_bobber.Phase == BobberPhase.Reeling || _bobber.Phase == BobberPhase.Casting)
            {
                return;
            }

            _bobber.RequestCast(position);
        }

        private void HandleInput()
        {
            if (!_running || _camera == null)
            {
                return;
            }

            if (Input.GetMouseButtonDown(0))
            {
                Vector3 mousePosition = Input.mousePosition;
                CastAtScreenPoint(new Vector2(mousePosition.x, mousePosition.y));
            }

            for (int i = 0; i < Input.touchCount; i++)
            {
                Touch touch = Input.GetTouch(i);
                if (touch.phase == TouchPhase.Began)
                {
                    CastAtScreenPoint(touch.position);
                }
            }
        }

        private void CastAtScreenPoint(Vector2 screenPoint)
        {
            Vector3 world = _camera.ScreenToWorldPoint(new Vector3(screenPoint.x, screenPoint.y, Mathf.Abs(_camera.transform.position.z)));
            CastAtPondPoint(new Vector2(world.x, world.y));
        }

        private void OnCastCompleted(Vector2 position, float radiusMultiplier, float timeMultiplier)
        {
            if (!_running)
            {
                return;
            }

            // 찌를 걷어올린 뒤 다시 던진 것이므로 기존 관심은 거리에 관계없이 끊긴다.
            for (int i = 0; i < _fish.Count; i++)
            {
                FishAgentV2 fish = _fish[i];
                if (fish == null || fish.IsCaught) continue;
                if (fish.State == FishState.Notice || fish.State == FishState.Interested || fish.State == FishState.Linger)
                {
                    fish.ReturnToRoam(1.2f);
                }
            }

            for (int i = 0; i < _fish.Count; i++)
            {
                FishAgentV2 fish = _fish[i];
                if (fish != null) fish.ApplyStartle(position, radiusMultiplier, timeMultiplier, true);
            }

            if (!Tuning.EnableLure)
            {
                return;
            }

            float gap = _now - _lastLureTime;
            float fatigue = Mathf.Clamp01(gap / Mathf.Max(0.1f, Tuning.LureRecovery));
            fatigue = Mathf.Lerp(Tuning.LureFatigueFloor, 1f, fatigue);
            _lastLureTime = _now;

            FishAgentV2 closest = null;
            float closestDistance = float.PositiveInfinity;
            bool pulled = false;
            for (int i = 0; i < _fish.Count; i++)
            {
                FishAgentV2 fish = _fish[i];
                if (fish == null || !fish.CanBeLured(position, radiusMultiplier, out float distance))
                {
                    continue;
                }

                if (distance < closestDistance)
                {
                    closest = fish;
                    closestDistance = distance;
                }

                if (fish.Roll(fish.Species.LureChance * fatigue))
                {
                    fish.BecomeInterested();
                    pulled = true;
                }
            }

            // 조작에 대한 첫 피드백은 항상 있어야 한다. 단, 피로도가 높은 연속 착수에서는 보장을 약하게 한다.
            if (!pulled && closest != null && fatigue > Tuning.LureGuaranteeThreshold)
            {
                closest.BecomeInterested();
            }
        }

        private void OnFishReachedBobber(FishAgentV2 fish)
        {
            if (!_running || fish == null || fish.State != FishState.Interested || _bobber == null)
            {
                return;
            }

            if (!_bobber.TryTakeBite(fish))
            {
                fish.ReturnToRoam(Tuning.RejectedCooldown);
            }
        }

        private void OnBiteExpired(FishAgentV2 fish)
        {
            if (fish == null || fish.IsCaught)
            {
                return;
            }

            if (_releaseSpecies.Contains(fish.Species.SpeciesId))
            {
                _bobber.ClearBite();
                fish.Release(2.2f, 0.5f, 0.5f);
                return;
            }

            CatchFish(fish);
        }

        private void CatchFish(FishAgentV2 fish)
        {
            int index = _fish.IndexOf(fish);
            if (index < 0)
            {
                return;
            }

            FishSpeciesConfig species = fish.Species;
            fish.MarkCaught();
            _fish.RemoveAt(index);
            PromoteFollower(fish);
            _bobber.ClearBite();

            if (Tuning.EnableCatchFlight && _catchFlight != null)
            {
                Vector3 target = _catchFlight.NextBasketTarget(_pond);
                _catchFlight.Launch(fish, target, OnCatchArrived);
            }
            else
            {
                OnCatchArrived(species, fish.SizeCm);
            }

            DestroyObjectSafe(fish.gameObject);

            if (species.RespawnDelay >= 0f)
            {
                _respawns.Add(new RespawnEntry { Species = species, DueAt = _now + species.RespawnDelay });
            }

            // 자동 회수는 같은 자리에 0.3초 회수 + 0.3초 재착수한다.
            _bobber.RequestCast(_bobber.Position);
        }

        private void OnCatchArrived(FishSpeciesConfig species, float sizeCm)
        {
            if (species == null)
            {
                return;
            }

            if (!_caught.ContainsKey(species.SpeciesId)) _caught[species.SpeciesId] = 0;
            _caught[species.SpeciesId]++;
            _score += species.BaseScore;
            PlayerDataV2.Instance.RecordCatch(species, sizeCm);
        }

        private void PromoteFollower(FishAgentV2 removedLeader)
        {
            if (removedLeader == null)
            {
                return;
            }

            for (int i = 0; i < _fish.Count; i++)
            {
                FishAgentV2 follower = _fish[i];
                if (follower != null && follower.Leader == removedLeader)
                {
                    follower.BecomeIndependent();
                    return;
                }
            }
        }

        private void ProcessRespawns()
        {
            for (int i = _respawns.Count - 1; i >= 0; i--)
            {
                RespawnEntry entry = _respawns[i];
                if (_now < entry.DueAt)
                {
                    continue;
                }

                _respawns.RemoveAt(i);
                if (_running)
                {
                    SpawnAgent(entry.Species, null, Vector2.zero);
                }
            }
        }

        private void SpawnAllSpecies()
        {
            foreach (FishSpeciesConfig species in _speciesById.Values)
            {
                int count = species.SpawnCount.Sample(_random);
                for (int i = 0; i < count; i++)
                {
                    if (species.IsSchool && _random.NextDouble() >= species.School.SoloChance)
                    {
                        SpawnSchool(species);
                    }
                    else
                    {
                        SpawnAgent(species, null, Vector2.zero);
                    }
                }
            }
        }

        private void SpawnSchool(FishSpeciesConfig species)
        {
            FishAgentV2 leader = SpawnAgent(species, null, Vector2.zero);
            int count = species.School.GroupSize.Sample(_random);
            float gap = species.Visual.Length * species.School.GapMultiplier;
            for (int i = 1; i < count; i++)
            {
                int row = Mathf.CeilToInt(i / 2f);
                float side = i % 2 == 1 ? 1f : -1f;
                Vector2 formation = new Vector2(-row * gap * 0.80f, side * row * gap * 0.58f);
                SpawnAgent(species, leader, formation);
            }
        }

        private FishAgentV2 SpawnAgent(FishSpeciesConfig species, FishAgentV2 leader, Vector2 formation)
        {
            if (species == null)
            {
                return null;
            }

            GameObject fishObject = new GameObject("FishV2_" + species.SpeciesId);
            fishObject.transform.SetParent(_fishRoot, false);
            FishAgentV2 agent = fishObject.AddComponent<FishAgentV2>();
            Mesh mesh = GetMesh(species);
            agent.Initialize(species, Tuning, _pond, _random, mesh, _fishMaterial, _shadowMaterial, leader, formation, _presentation);
            SetLayerRecursively(agent.transform, UnderwaterRenderLayer);
            _fish.Add(agent);
            return agent;
        }

        private Mesh GetMesh(FishSpeciesConfig species)
        {
            if (!_meshCache.TryGetValue(species.SpeciesId, out Mesh mesh) || mesh == null)
            {
                mesh = FishMeshBuilderV2.Build(species, _presentation);
                _meshCache[species.SpeciesId] = mesh;
            }

            return mesh;
        }

        private void ClearFish()
        {
            for (int i = _fish.Count - 1; i >= 0; i--)
            {
                FishAgentV2 fish = _fish[i];
                if (fish != null) DestroyObjectSafe(fish.gameObject);
            }

            _fish.Clear();
        }

        private void BuildSpeciesTable()
        {
            _speciesById.Clear();
            if (Spot != null && Spot.Species != null && Spot.Species.Length > 0)
            {
                for (int i = 0; i < Spot.Species.Length; i++)
                {
                    FishSpeciesAsset asset = Spot.Species[i];
                    if (asset != null && asset.Data != null && !string.IsNullOrEmpty(asset.Data.SpeciesId))
                    {
                        _speciesById[asset.Data.SpeciesId] = asset.Data;
                    }
                }
            }

            if (_speciesById.Count == 0)
            {
                List<FishSpeciesConfig> defaults = FishingV2Catalog.CreateDefaults();
                for (int i = 0; i < defaults.Count; i++)
                {
                    _speciesById[defaults[i].SpeciesId] = defaults[i];
                }
            }
        }

        private void EnsureCamera()
        {
            _camera = Camera.main;
            if (_camera == null)
            {
                GameObject cameraObject = new GameObject("FishingV2Camera");
                _camera = cameraObject.AddComponent<Camera>();
                cameraObject.tag = "MainCamera";
            }

            _camera.orthographic = true;
            _camera.orthographicSize = _pond.height * 0.5f;
            // Match the HTML prototype's top-down convention: the viewer is on +Z and
            // looks toward -Z. Eye discs are authored on the fish's +Z-facing surface.
            _camera.transform.position = new Vector3(_pond.center.x, _pond.center.y, 10f);
            _camera.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
            _camera.clearFlags = CameraClearFlags.SolidColor;
            _camera.backgroundColor = _presentation.WaterDeepColor;
        }

        private void EnsureMaterials()
        {
            _fishMaterial = CreateMaterial(FindShader("FishingV2/FishSurface", "Universal Render Pipeline/Unlit", "Standard"));
            _shadowMaterial = CreateMaterial(FindShader("FishingV2/FishShadow", "Universal Render Pipeline/Unlit", "Unlit/Color", "Standard"));
            _waterMaterial = CreateMaterial(FindShader("FishingV2/WaterSurface", "Universal Render Pipeline/Unlit", "Unlit/Color", "Standard"));
            _underwaterBottomMaterial = CreateMaterial(FindShader("FishingV2/WaterSurface", "Universal Render Pipeline/Unlit", "Unlit/Color", "Standard"));
            _simpleMaterial = CreateMaterial(FindShader("Universal Render Pipeline/Unlit", "Unlit/Color", "Standard"));
            SetMaterialColor(_simpleMaterial, _presentation.AccentColor);
            ConfigureWaterMaterial(_waterMaterial, _presentation);
            ConfigureWaterMaterial(_underwaterBottomMaterial, _presentation);
            ConfigureFishMaterial(_fishMaterial, _presentation);
            if (_waterMaterial != null && _waterMaterial.HasProperty("_UnderwaterComposite"))
            {
                _waterMaterial.SetFloat("_UnderwaterComposite", 1f);
            }
            if (_underwaterBottomMaterial != null)
            {
                if (_underwaterBottomMaterial.HasProperty("_UnderwaterComposite"))
                {
                    _underwaterBottomMaterial.SetFloat("_UnderwaterComposite", 0f);
                }
                // The bottom layer is rendered before the final composite. Let only the
                // final camera apply surface refraction so the floor is not double-warped.
                if (_underwaterBottomMaterial.HasProperty("_OpticalDistortionStrength"))
                {
                    _underwaterBottomMaterial.SetFloat("_OpticalDistortionStrength", 0f);
                }
            }
        }

        private void EnsureWater()
        {
            GameObject water = GameObject.CreatePrimitive(PrimitiveType.Quad);
            water.name = "WaterSurfaceV2";
            water.transform.SetParent(transform, false);
            // The camera is at +Z and looks toward -Z, so smaller Z values are farther away.
            // This quad is now the final water/underwater composite behind the HUD.
            water.transform.position = new Vector3(_pond.center.x, _pond.center.y, -0.20f);
            Vector2 surfaceSize = GetSurfaceSize();
            water.transform.localScale = new Vector3(surfaceSize.x, surfaceSize.y, 1f);
            MeshRenderer renderer = water.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = _waterMaterial;
            Collider collider = water.GetComponent<Collider>();
            if (collider != null) DestroyObjectSafe(collider);
            _waterObject = water;

            GameObject bottom = GameObject.CreatePrimitive(PrimitiveType.Quad);
            bottom.name = "UnderwaterBottomLayerV2";
            bottom.transform.SetParent(transform, false);
            bottom.layer = UnderwaterRenderLayer;
            bottom.transform.position = new Vector3(_pond.center.x, _pond.center.y, -0.20f);
            bottom.transform.localScale = new Vector3(surfaceSize.x, surfaceSize.y, 1f);
            MeshRenderer bottomRenderer = bottom.GetComponent<MeshRenderer>();
            bottomRenderer.sharedMaterial = _underwaterBottomMaterial;
            Collider bottomCollider = bottom.GetComponent<Collider>();
            if (bottomCollider != null) DestroyObjectSafe(bottomCollider);
            _underwaterBottomObject = bottom;
        }

        private Vector2 GetSurfaceSize()
        {
            float visibleHeight = _camera != null ? _camera.orthographicSize * 2f : _pond.height;
            float visibleWidth = _camera != null ? visibleHeight * _camera.aspect : _pond.width;
            // Leave a small world-space overscan around the camera view. Without it the last
            // raster row/column of the bottom RT can coincide with the composite quad edge and
            // become visible as a bright cyan rectangular border after UV clamping.
            const float edgeOverscan = 0.60f;
            return new Vector2(
                Mathf.Max(_pond.width, visibleWidth + edgeOverscan),
                Mathf.Max(_pond.height, visibleHeight + edgeOverscan));
        }

        private static float GetOpticalTime()
        {
#if UNITY_EDITOR
            // Editor GameView capture can stop advancing Unity's scaled and unscaled clocks
            // while the window is not foregrounded. Use the editor wall clock for visual-only
            // optics in that context so an optics-only evidence run still has a real time axis.
            return (float)UnityEditor.EditorApplication.timeSinceStartup;
#else
            return Time.unscaledTime;
#endif
        }

        private void EnsureUnderwaterOptics()
        {
            int underwaterMask = 1 << UnderwaterRenderLayer;
            _camera.cullingMask &= ~underwaterMask;

            if (_underwaterCamera == null)
            {
                GameObject cameraObject = new GameObject("FishingV2UnderwaterCamera");
                cameraObject.transform.SetParent(transform, false);
                cameraObject.layer = UnderwaterRenderLayer;
                _underwaterCamera = cameraObject.AddComponent<Camera>();
                _underwaterCamera.enabled = false;
            }

            _underwaterCamera.cullingMask = underwaterMask;
            _underwaterCamera.clearFlags = CameraClearFlags.SolidColor;
            _underwaterCamera.backgroundColor = new Color(0f, 0f, 0f, 0f);
            _underwaterCamera.allowHDR = false;
            _underwaterCamera.allowMSAA = false;
            _underwaterCamera.useOcclusionCulling = false;
            SyncUnderwaterCamera();
            EnsureUnderwaterRenderTexture();
            if (_waterMaterial != null && _underwaterSceneTexture != null)
            {
                _waterMaterial.SetTexture("_UnderwaterSceneTex", _underwaterSceneTexture);
            }
        }

        private void SyncUnderwaterCamera()
        {
            if (_underwaterCamera == null || _camera == null)
            {
                return;
            }

            _underwaterCamera.transform.SetPositionAndRotation(_camera.transform.position, _camera.transform.rotation);
            _underwaterCamera.orthographic = _camera.orthographic;
            _underwaterCamera.orthographicSize = _camera.orthographicSize;
            _underwaterCamera.fieldOfView = _camera.fieldOfView;
            _underwaterCamera.aspect = _camera.aspect;
            _underwaterCamera.rect = _camera.rect;
            _underwaterCamera.nearClipPlane = _camera.nearClipPlane;
            _underwaterCamera.farClipPlane = _camera.farClipPlane;
            _underwaterCamera.depth = _camera.depth - 1f;
        }

        private void EnsureUnderwaterRenderTexture()
        {
            if (_underwaterCamera == null || _camera == null)
            {
                return;
            }

            int width = Mathf.Max(1, _camera.pixelWidth);
            int height = Mathf.Max(1, _camera.pixelHeight);
            if (width <= 1) width = Mathf.Max(1, Screen.width);
            if (height <= 1) height = Mathf.Max(1, Screen.height);

            if (_underwaterSceneTexture != null &&
                _underwaterTextureWidth == width &&
                _underwaterTextureHeight == height &&
                _underwaterSceneTexture.IsCreated())
            {
                _underwaterCamera.targetTexture = _underwaterSceneTexture;
                return;
            }

            if (_underwaterSceneTexture != null)
            {
                _underwaterSceneTexture.Release();
                DestroyObjectSafe(_underwaterSceneTexture);
            }

            _underwaterTextureWidth = width;
            _underwaterTextureHeight = height;
            _underwaterSceneTexture = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32)
            {
                name = "FishingV2_UnderwaterSceneRT",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                useMipMap = false,
                autoGenerateMips = false,
                antiAliasing = 1
            };
            _underwaterSceneTexture.Create();
            _underwaterCamera.targetTexture = _underwaterSceneTexture;
        }

        private void EnsureEnvironment()
        {
            if (_environmentRoot != null)
            {
                return;
            }

            _environmentRoot = FishingV2EnvironmentV2.Build(
                transform,
                _pond,
                _shadowMaterial,
                _presentation.EnvironmentSilhouetteStrength);
            SetLayerRecursively(_environmentRoot, UnderwaterRenderLayer);
        }

        private static void ConfigureWaterMaterial(Material material, FishingV2PresentationSettings presentation)
        {
            if (material == null) return;
            if (material.HasProperty("_DeepColor")) material.SetColor("_DeepColor", presentation.WaterDeepColor);
            if (material.HasProperty("_ShallowColor")) material.SetColor("_ShallowColor", presentation.WaterShallowColor);
            if (material.HasProperty("_ClearWaterColor")) material.SetColor("_ClearWaterColor", presentation.WaterClearColor);
            if (material.HasProperty("_DeepWaterColor")) material.SetColor("_DeepWaterColor", presentation.WaterDeepNavyColor);
            if (material.HasProperty("_BottomBleedColor")) material.SetColor("_BottomBleedColor", presentation.WaterBottomBleedColor);
            if (material.HasProperty("_ClearColorStrength")) material.SetFloat("_ClearColorStrength", presentation.WaterClearColorStrength);
            if (material.HasProperty("_DepthColorStrength")) material.SetFloat("_DepthColorStrength", presentation.WaterDepthColorStrength);
            if (material.HasProperty("_BottomColorBleed")) material.SetFloat("_BottomColorBleed", presentation.WaterBottomColorBleed);
            if (material.HasProperty("_OpticalDistortionStrength")) material.SetFloat("_OpticalDistortionStrength", presentation.WaterOpticalDistortionStrength);
            if (material.HasProperty("_OpticalDistortionScale")) material.SetFloat("_OpticalDistortionScale", presentation.WaterOpticalDistortionScale);
            if (material.HasProperty("_OpticalDistortionSpeed")) material.SetFloat("_OpticalDistortionSpeed", presentation.WaterOpticalDistortionSpeed);
            if (material.HasProperty("_OpticalTime")) material.SetFloat("_OpticalTime", 0f);
            if (material.HasProperty("_OpticalCausticFloorBias")) material.SetFloat("_OpticalCausticFloorBias", presentation.WaterOpticalCausticFloorBias);
            if (material.HasProperty("_BottomDarkColor")) material.SetColor("_BottomDarkColor", presentation.WaterBottomDarkColor);
            if (material.HasProperty("_BottomLightColor")) material.SetColor("_BottomLightColor", presentation.WaterBottomLightColor);
            if (material.HasProperty("_BottomVisibility")) material.SetFloat("_BottomVisibility", presentation.WaterBottomVisibility);
            if (material.HasProperty("_BottomGrainStrength")) material.SetFloat("_BottomGrainStrength", presentation.WaterBottomGrainStrength);
            if (material.HasProperty("_BottomVariationScale")) material.SetFloat("_BottomVariationScale", presentation.WaterBottomVariationScale);
            if (presentation.WaterBottomTexture != null && material.HasProperty("_BottomTexture")) material.SetTexture("_BottomTexture", presentation.WaterBottomTexture);
            if (material.HasProperty("_BottomTextureScale")) material.SetFloat("_BottomTextureScale", presentation.WaterBottomTextureScale);
            if (material.HasProperty("_BottomTextureBlend")) material.SetFloat("_BottomTextureBlend", presentation.WaterBottomTexture != null ? presentation.WaterBottomTextureBlend : 0f);
            if (material.HasProperty("_BottomTextureContrast")) material.SetFloat("_BottomTextureContrast", presentation.WaterBottomTextureContrast);
            if (material.HasProperty("_BottomSecondarySampleStrength")) material.SetFloat("_BottomSecondarySampleStrength", presentation.WaterBottomSecondarySampleStrength);
            if (material.HasProperty("_LargeCausticColor")) material.SetColor("_LargeCausticColor", presentation.WaterLargeCausticColor);
            if (material.HasProperty("_LargeCausticStrength")) material.SetFloat("_LargeCausticStrength", presentation.WaterLargeCausticStrength);
            if (material.HasProperty("_LargeCausticScale")) material.SetFloat("_LargeCausticScale", presentation.WaterLargeCausticScale);
            if (material.HasProperty("_LargeCausticSpeed")) material.SetFloat("_LargeCausticSpeed", presentation.WaterLargeCausticSpeed);
            if (material.HasProperty("_MidCausticColor")) material.SetColor("_MidCausticColor", presentation.WaterMidCausticColor);
            if (material.HasProperty("_MidCausticStrength")) material.SetFloat("_MidCausticStrength", presentation.WaterMidCausticStrength);
            if (material.HasProperty("_MidCausticScale")) material.SetFloat("_MidCausticScale", presentation.WaterMidCausticScale);
            if (material.HasProperty("_MidCausticSpeed")) material.SetFloat("_MidCausticSpeed", presentation.WaterMidCausticSpeed);
            if (material.HasProperty("_MicroSurfaceColor")) material.SetColor("_MicroSurfaceColor", presentation.WaterMicroSurfaceColor);
            if (material.HasProperty("_MicroSurfaceStrength")) material.SetFloat("_MicroSurfaceStrength", presentation.WaterMicroSurfaceStrength);
            if (material.HasProperty("_ClearZoneStrength")) material.SetFloat("_ClearZoneStrength", presentation.WaterClearZoneStrength);
            if (material.HasProperty("_ClearZoneRadius")) material.SetFloat("_ClearZoneRadius", presentation.WaterClearZoneRadius);
            if (material.HasProperty("_EdgeFogStrength")) material.SetFloat("_EdgeFogStrength", presentation.WaterEdgeFogStrength);
            if (material.HasProperty("_EdgeFogRadius")) material.SetFloat("_EdgeFogRadius", presentation.WaterEdgeFogRadius);
        }

        private void ConfigureFishMaterial(Material material, FishingV2PresentationSettings presentation)
        {
            if (material == null)
            {
                return;
            }

            Vector2 surfaceSize = GetSurfaceSize();
            if (material.HasProperty("_OpticalTime")) material.SetFloat("_OpticalTime", 0f);
            if (material.HasProperty("_OpticalDistortionScale")) material.SetFloat("_OpticalDistortionScale", presentation.WaterOpticalDistortionScale);
            if (material.HasProperty("_OpticalDistortionSpeed")) material.SetFloat("_OpticalDistortionSpeed", presentation.WaterOpticalDistortionSpeed);
            if (material.HasProperty("_OpticalDistortionStrength")) material.SetFloat("_OpticalDistortionStrength", presentation.WaterOpticalDistortionStrength);
            if (material.HasProperty("_PondCenter")) material.SetVector("_PondCenter", new Vector4(_pond.center.x, _pond.center.y, 0f, 0f));
            if (material.HasProperty("_PondSize")) material.SetVector("_PondSize", new Vector4(surfaceSize.x, surfaceSize.y, 0f, 0f));
        }

        private void EnsureFishRoot()
        {
            GameObject root = new GameObject("FishRootV2");
            root.transform.SetParent(transform, false);
            root.layer = UnderwaterRenderLayer;
            _fishRoot = root.transform;
        }

        private void EnsureBobber()
        {
            GameObject bobberObject = new GameObject("BobberV2");
            bobberObject.transform.SetParent(transform, false);
            bobberObject.layer = UnderwaterRenderLayer;
            _bobber = bobberObject.AddComponent<BobberV2>();

            GameObject visualObject = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            visualObject.name = "BobberVisual";
            visualObject.transform.SetParent(transform, false);
            visualObject.layer = UnderwaterRenderLayer;
            visualObject.transform.localScale = Vector3.one * 0.23f;
            MeshRenderer visualRenderer = visualObject.GetComponent<MeshRenderer>();
            visualRenderer.sharedMaterial = _simpleMaterial;
            Collider visualCollider = visualObject.GetComponent<Collider>();
            if (visualCollider != null) DestroyObjectSafe(visualCollider);

            GameObject ringObject = new GameObject("BobberRing");
            ringObject.transform.SetParent(transform, false);
            ringObject.layer = UnderwaterRenderLayer;
            LineRenderer ring = ringObject.AddComponent<LineRenderer>();
            ring.useWorldSpace = false;
            ring.loop = true;
            ring.positionCount = 40;
            ring.widthMultiplier = 0.018f;
            _bobberRingMaterial = CreateTransparentLineMaterial();
            ring.sharedMaterial = _bobberRingMaterial;
            ConfigureTransparentLineMaterial(ring.sharedMaterial);
            _bobberRingLine = ring;
            for (int i = 0; i < ring.positionCount; i++)
            {
                float angle = i / (float)ring.positionCount * Mathf.PI * 2f;
                ring.SetPosition(i, new Vector3(Mathf.Cos(angle) * 0.40f, Mathf.Sin(angle) * 0.40f, 0f));
            }

            GameObject rippleObject = new GameObject("BobberPhysicalRipple");
            rippleObject.transform.SetParent(transform, false);
            rippleObject.layer = UnderwaterRenderLayer;
            LineRenderer ripple = rippleObject.AddComponent<LineRenderer>();
            ripple.useWorldSpace = false;
            ripple.loop = true;
            ripple.positionCount = 40;
            ripple.widthMultiplier = 0.026f;
            _bobberRippleMaterial = CreateTransparentLineMaterial();
            ripple.sharedMaterial = _bobberRippleMaterial;
            ConfigureTransparentLineMaterial(ripple.sharedMaterial);
            _bobberRippleLine = ripple;
            ripple.startColor = new Color(0.32f, 0.78f, 0.76f, 0.22f);
            ripple.endColor = ripple.startColor;
            for (int i = 0; i < ripple.positionCount; i++)
            {
                float angle = i / (float)ripple.positionCount * Mathf.PI * 2f;
                ripple.SetPosition(i, new Vector3(Mathf.Cos(angle) * 0.68f, Mathf.Sin(angle) * 0.68f, 0f));
            }

            GameObject castPathObject = new GameObject("BobberCastPath");
            castPathObject.transform.SetParent(transform, false);
            castPathObject.layer = UnderwaterRenderLayer;
            LineRenderer castPath = castPathObject.AddComponent<LineRenderer>();
            castPath.useWorldSpace = true;
            castPath.loop = false;
            castPath.positionCount = 18;
            castPath.widthMultiplier = 0.022f;
            castPath.material = _simpleMaterial;
            castPath.startColor = new Color(1f, 0.72f, 0.30f, 0.78f);
            castPath.endColor = new Color(1f, 0.50f, 0.18f, 0.22f);

            _bobber.Initialize(
                Tuning,
                _pond,
                visualObject.transform,
                ring,
                castPath,
                _presentation.BobberScale,
                _presentation.WaterOpticalDistortionScale,
                _presentation.WaterOpticalDistortionSpeed,
                _presentation.WaterBobberOpticalStrength,
                ripple);
            _bobber.CastCompleted += OnCastCompleted;
            _bobber.BiteExpired += OnBiteExpired;
        }

        private void EnsureCatchFlight()
        {
            GameObject flightObject = new GameObject("CatchFlightV2");
            flightObject.transform.SetParent(transform, false);
            flightObject.layer = UnderwaterRenderLayer;
            _catchFlight = flightObject.AddComponent<CatchFlightV2>();
            _catchFlight.Initialize(Tuning, _fishMaterial);
        }

        private void CreateBasketMarkers()
        {
            for (int i = 0; i < 5; i++)
            {
                GameObject marker = GameObject.CreatePrimitive(PrimitiveType.Cube);
                marker.name = "BasketSlot_" + i;
                marker.transform.position = new Vector3(_pond.xMax - 0.35f, _pond.yMin + 0.55f + i * 0.72f, 0.10f);
                marker.transform.localScale = new Vector3(0.20f, 0.42f, 0.04f);
                marker.GetComponent<MeshRenderer>().sharedMaterial = _simpleMaterial;
                Collider collider = marker.GetComponent<Collider>();
                if (collider != null) DestroyObjectSafe(collider);
            }
        }

        private static Shader FindShader(params string[] names)
        {
            for (int i = 0; i < names.Length; i++)
            {
                Shader shader = Shader.Find(names[i]);
                if (shader != null) return shader;
            }

            return null;
        }

        private static Material CreateMaterial(Shader shader)
        {
            if (shader == null)
            {
                return null;
            }

            return new Material(shader);
        }

        private static Material CreateTransparentLineMaterial()
        {
            Material material = CreateMaterial(FindShader("FishingV2/WaterRipple", "Universal Render Pipeline/Unlit", "Unlit/Color", "Standard"));
            if (material == null)
            {
                return null;
            }

            ConfigureTransparentLineMaterial(material);
            return material;
        }

        private static void ConfigureTransparentLineMaterial(Material material)
        {
            if (material == null)
            {
                return;
            }

            if (material.shader != null && material.shader.name == "FishingV2/WaterRipple")
            {
                return;
            }

            // LineRenderer vertex colors carry the role-specific tint and alpha. Keep the
            // material neutral so the cyan physical ripple is not multiplied by the gold
            // interaction material, then put the URP Unlit surface on the transparent path.
            SetMaterialColor(material, Color.white);
            // These are the standard URP Unlit surface controls. Set them directly instead
            // of relying on HasProperty during shader import; the inspector can report the
            // properties before the shader variant has finished importing.
            material.SetFloat("_Surface", 1f);
            material.SetFloat("_Blend", 0f);
            material.SetFloat("_AlphaClip", 0f);
            material.SetFloat("_ZWrite", 0f);
            material.SetFloat("_Cull", 0f);
            material.SetFloat("_SrcBlend", 5f);
            material.SetFloat("_DstBlend", 10f);
            material.SetOverrideTag("RenderType", "Transparent");
            material.renderQueue = 3000;
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        }

        private static void SetMaterialColor(Material material, Color color)
        {
            if (material == null) return;
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
            if (material.HasProperty("_Color")) material.SetColor("_Color", color);
        }

        private static void SetLayerRecursively(Transform root, int layer)
        {
            if (root == null)
            {
                return;
            }

            root.gameObject.layer = layer;
            for (int i = 0; i < root.childCount; i++)
            {
                SetLayerRecursively(root.GetChild(i), layer);
            }
        }

        private static void DestroyObjectSafe(UnityEngine.Object target)
        {
            if (target == null) return;
            if (Application.isPlaying) UnityEngine.Object.Destroy(target);
            else UnityEngine.Object.DestroyImmediate(target);
        }

        private void OnDestroy()
        {
            DestroyObjectSafe(_bobberRingMaterial);
            _bobberRingMaterial = null;
            DestroyObjectSafe(_bobberRippleMaterial);
            _bobberRippleMaterial = null;
            _bobberRingLine = null;
            _bobberRippleLine = null;

            if (_underwaterSceneTexture != null)
            {
                _underwaterSceneTexture.Release();
                DestroyObjectSafe(_underwaterSceneTexture);
                _underwaterSceneTexture = null;
            }

            if (_underwaterCamera != null)
            {
                DestroyObjectSafe(_underwaterCamera.gameObject);
                _underwaterCamera = null;
            }
        }

        private void OnGUI()
        {
            if (!_initialized) return;

            bool isCasual = _presentation.Variant == FishingV2PresentationVariant.CasualFishing;
            float panelWidth = isCasual ? 236f : 190f;
            float panelHeight = isCasual ? 92f + _caught.Count * 18f : 72f;

            GUI.color = isCasual
                ? new Color(0.01f, 0.035f, 0.045f, 0.90f)
                : new Color(0.01f, 0.035f, 0.045f, 0.40f);
            GUI.DrawTexture(new Rect(10f, 10f, panelWidth, panelHeight), Texture2D.whiteTexture);

            GUI.color = _presentation.AccentColor;
            GUI.Label(new Rect(16f, 14f, panelWidth - 22f, 22f), "낚시터  ·  " + FormatTime(_timeLeft));
            GUI.color = new Color(0.88f, 0.95f, 0.96f, 1f);
            GUI.Label(new Rect(16f, 38f, panelWidth - 22f, 22f), "점수 " + _score + "  ·  물고기 " + _fish.Count);

            int row = 0;
            if (_presentation.ShowSpeciesCounters)
            {
                foreach (KeyValuePair<string, int> entry in _caught)
                {
                    string label = entry.Key;
                    if (_speciesById.TryGetValue(entry.Key, out FishSpeciesConfig species) && species != null)
                    {
                        label = species.DisplayName;
                    }

                    GUI.color = new Color(0.72f, 0.84f, 0.86f, 1f);
                    GUI.Label(new Rect(16f, 66f + row * 18f, panelWidth - 22f, 18f), label + "  " + entry.Value);
                    row++;
                }
            }

            GUI.color = new Color(_presentation.AccentColor.r, _presentation.AccentColor.g, _presentation.AccentColor.b, 0.78f);
            GUI.Label(new Rect(Screen.width - 190f, 14f, 178f, 22f), _presentation.DisplayName);

            float promptY = _presentation.ShowSpeciesCounters ? 74f + _caught.Count * 18f : 82f;
            GUI.color = new Color(0.78f, 0.88f, 0.89f, 0.95f);
            if (!_running)
            {
                GUI.Label(new Rect(16f, promptY, 380f, 24f), "세션 종료 — 잡은 물고기는 유지됩니다");
            }
            else if (_bobber == null || !_bobber.IsInWater)
            {
                GUI.Label(new Rect(16f, promptY, 460f, 24f), "수면을 클릭하면 찌를 던집니다 · 입질 중 클릭하면 즉시 회수");
            }

            GUI.color = Color.white;
        }

        private static string FormatTime(float seconds)
        {
            int whole = Mathf.CeilToInt(Mathf.Max(0f, seconds));
            return string.Format("{0:00}:{1:00}", whole / 60, whole % 60);
        }
    }
}
