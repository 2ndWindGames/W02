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
        [Header("Water presentation")]
        [Tooltip("세션 시작 시 '물 밖 → 수면 통과 → gameplay' 연출을 재생한다. 끄면 처음부터 Gameplay 프로파일이다.")]
        public bool PlaySessionDivePresentation = true;
        public FishingV2DivePresentationTiming DivePresentationTiming = new FishingV2DivePresentationTiming();
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
        private readonly FishingV2WaterPresentationDirector _waterPresentation = new FishingV2WaterPresentationDirector();
        private FishingV2WaterProfile _gameplayWaterProfile;
        private FishingV2WaterProfile _presentationWaterProfile;
        private FishingV2WaterProfile _diveWaterProfile;
        private FishingV2WaterProfile _activeWaterProfile;
        private float _gameplayOrthographicSize;
        private Vector3 _gameplayCameraPosition;
        private float _maxCameraHeight = 1f;
        private Vector2 _maxCameraFramingShift;
        private Mesh _waterQuadMesh;
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
        public FishingV2WaterProfile ActiveWaterProfile { get { return _activeWaterProfile; } }
        public FishingV2SessionPresentationPhase PresentationPhase { get { return _waterPresentation.Phase; } }
        public float PresentationTime { get { return _waterPresentation.Time; } }
        public float PresentationTotalSeconds { get { return _waterPresentation.TotalSeconds; } }
        /// <summary>연출이 입력을 쥐고 있는 동안 true. gameplay 로직은 그대로 돌아간다.</summary>
        public bool PresentationInputLocked { get { return _waterPresentation.InputLocked; } }

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
            // 프로파일을 먼저 바른다. 카메라 framing까지 여기서 정해져야 아래의
            // SyncUnderwaterCamera가 같은 프레임의 프레이밍을 RT에 복사한다.
            ApplyWaterProfile(_waterPresentation.Current);
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
            // 연출 시간축은 시뮬레이션과 같은 dt를 쓴다. 헤드리스 스텝에서도 결정론적으로
            // 진행되어야 프레임 시퀀스를 재현할 수 있다.
            _waterPresentation.Tick(dt);

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
            BuildWaterProfiles();
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
            ApplyWaterProfile(_waterPresentation.Current);
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

            if (PlaySessionDivePresentation)
            {
                _waterPresentation.PlayIntro();
            }
            else
            {
                _waterPresentation.ForceGameplay();
            }
            ApplyWaterProfile(_waterPresentation.Current);

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

            // 연출이 끝나기 전까지는 입력만 잠근다. 물고기 시뮬레이션·유인·입질 로직은
            // 그대로 돌아가고 있으므로, 연출이 끝나면 진행 중이던 세션을 그대로 이어받는다.
            if (_waterPresentation.InputLocked)
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
            // 승인된 gameplay framing. 연출은 항상 이 값에서 출발해서 이 값으로 돌아온다.
            _gameplayOrthographicSize = _camera.orthographicSize;
            _gameplayCameraPosition = _camera.transform.position;
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
            Vector2 quadSize = GetWaterQuadSize();
            _waterQuadMesh = CreateWaterQuadMesh(GetWaterQuadUvSpan());

            // The camera is at +Z and looks toward -Z, so smaller Z values are farther away.
            // This quad is now the final water/underwater composite behind the HUD.
            _waterObject = CreateWaterQuad("WaterSurfaceV2", quadSize, _waterMaterial, 0);
            _underwaterBottomObject = CreateWaterQuad(
                "UnderwaterBottomLayerV2",
                quadSize,
                _underwaterBottomMaterial,
                UnderwaterRenderLayer);
        }

        private GameObject CreateWaterQuad(string quadName, Vector2 size, Material material, int layer)
        {
            GameObject quad = new GameObject(quadName);
            quad.transform.SetParent(transform, false);
            quad.layer = layer;
            quad.transform.position = new Vector3(_pond.center.x, _pond.center.y, -0.20f);
            quad.transform.localScale = new Vector3(size.x, size.y, 1f);
            quad.AddComponent<MeshFilter>().sharedMesh = _waterQuadMesh;
            quad.AddComponent<MeshRenderer>().sharedMaterial = material;
            return quad;
        }

        /// <summary>
        /// 물 쿼드는 연출에서 카메라가 가장 뒤로 빠졌을 때까지 덮어야 한다. 하지만 UV 0..1은
        /// 여전히 gameplay 프레이밍의 사각형에 고정한다 — 그래야 바닥 재질·코스틱·수면 파형의
        /// 월드 주파수가 gameplay에서 이전과 정확히 같다. 쿼드만 키우고 UV를 그대로 두면
        /// 승인된 바닥 무늬가 통째로 굵어진다.
        /// </summary>
        private static Mesh CreateWaterQuadMesh(Vector2 uvSpan)
        {
            float uMin = 0.5f - 0.5f * uvSpan.x;
            float uMax = 0.5f + 0.5f * uvSpan.x;
            float vMin = 0.5f - 0.5f * uvSpan.y;
            float vMax = 0.5f + 0.5f * uvSpan.y;
            Mesh mesh = new Mesh { name = "FishingV2WaterQuad" };
            mesh.vertices = new[]
            {
                new Vector3(-0.5f, -0.5f, 0f),
                new Vector3(0.5f, -0.5f, 0f),
                new Vector3(-0.5f, 0.5f, 0f),
                new Vector3(0.5f, 0.5f, 0f)
            };
            mesh.uv = new[]
            {
                new Vector2(uMin, vMin),
                new Vector2(uMax, vMin),
                new Vector2(uMin, vMax),
                new Vector2(uMax, vMax)
            };
            mesh.triangles = new[] { 0, 2, 1, 2, 3, 1 };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>
        /// UV 0..1이 덮는 월드 사각형. 승인된 gameplay 프레이밍 기준이며 연출 중에도 변하지 않는다.
        /// 물고기 셰이더의 _PondSize도 이 값이어야 수면 필드와 물고기 광량이 같은 좌표계에 있다.
        /// </summary>
        private Vector2 GetSurfaceSize()
        {
            float baseSize = _gameplayOrthographicSize > 0f
                ? _gameplayOrthographicSize
                : (_camera != null ? _camera.orthographicSize : _pond.height * 0.5f);
            float visibleHeight = baseSize * 2f;
            float visibleWidth = _camera != null ? visibleHeight * _camera.aspect : _pond.width;
            // Leave a small world-space overscan around the camera view. Without it the last
            // raster row/column of the bottom RT can coincide with the composite quad edge and
            // become visible as a bright cyan rectangular border after UV clamping.
            const float edgeOverscan = 0.60f;
            return new Vector2(
                Mathf.Max(_pond.width, visibleWidth + edgeOverscan),
                Mathf.Max(_pond.height, visibleHeight + edgeOverscan));
        }

        private Vector2 GetWaterQuadSize()
        {
            Vector2 surfaceSize = GetSurfaceSize();
            float height = Mathf.Max(1f, _maxCameraHeight);
            return new Vector2(
                surfaceSize.x * height + Mathf.Abs(_maxCameraFramingShift.x) * 2f,
                surfaceSize.y * height + Mathf.Abs(_maxCameraFramingShift.y) * 2f);
        }

        private Vector2 GetWaterQuadUvSpan()
        {
            Vector2 surfaceSize = GetSurfaceSize();
            Vector2 quadSize = GetWaterQuadSize();
            return new Vector2(
                quadSize.x / Mathf.Max(0.01f, surfaceSize.x),
                quadSize.y / Mathf.Max(0.01f, surfaceSize.y));
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

        // ---------------------------------------------------------------------------------
        // Water presentation profiles
        // ---------------------------------------------------------------------------------

        private void BuildWaterProfiles()
        {
            _gameplayWaterProfile = FishingV2WaterProfile.Gameplay(_presentation);
            _presentationWaterProfile = FishingV2WaterProfile.PresentationAboveWater(_presentation);
            _diveWaterProfile = FishingV2WaterProfile.DiveTransition(_presentation);

            // 물 쿼드 크기를 정하기 전에 알아야 하는 값이라 프로파일과 같이 뽑는다.
            _maxCameraHeight = Mathf.Max(
                _gameplayWaterProfile.CameraHeight,
                Mathf.Max(_presentationWaterProfile.CameraHeight, _diveWaterProfile.CameraHeight));
            _maxCameraFramingShift = new Vector2(
                Mathf.Max(
                    Mathf.Abs(_gameplayWaterProfile.CameraFramingShift.x),
                    Mathf.Max(
                        Mathf.Abs(_presentationWaterProfile.CameraFramingShift.x),
                        Mathf.Abs(_diveWaterProfile.CameraFramingShift.x))),
                Mathf.Max(
                    Mathf.Abs(_gameplayWaterProfile.CameraFramingShift.y),
                    Mathf.Max(
                        Mathf.Abs(_presentationWaterProfile.CameraFramingShift.y),
                        Mathf.Abs(_diveWaterProfile.CameraFramingShift.y))));

            _waterPresentation.Configure(
                _gameplayWaterProfile,
                _presentationWaterProfile,
                _diveWaterProfile,
                DivePresentationTiming);
        }

        /// <summary>
        /// 프로파일 하나를 화면에 바른다. 상태 전환이 여기 한 곳만 지나므로 새 연출을 붙일 때
        /// 머티리얼 프로퍼티 이름을 다시 찾아다닐 필요가 없다.
        /// </summary>
        private void ApplyWaterProfile(FishingV2WaterProfile profile)
        {
            _activeWaterProfile = profile;

            if (_waterMaterial != null)
            {
                SetFloatIfPresent(_waterMaterial, "_OpticalDistortionStrength", profile.RefractionStrength);
                SetFloatIfPresent(_waterMaterial, "_OpticalDistortionScale", profile.RefractionScale);
                SetFloatIfPresent(_waterMaterial, "_OpticalDistortionSpeed", profile.RefractionSpeed);
                SetFloatIfPresent(_waterMaterial, "_RefractionCoefficient", profile.RefractionCoefficient);
                SetFloatIfPresent(_waterMaterial, "_SurfaceRippleStrength", profile.SurfaceRippleStrength);
                SetFloatIfPresent(_waterMaterial, "_SurfaceShapeStrength", profile.SurfaceShapeStrength);
                SetFloatIfPresent(_waterMaterial, "_SurfaceHighlightStrength", profile.SurfaceHighlightStrength);
                SetFloatIfPresent(_waterMaterial, "_SurfaceSpecularStrength", profile.SurfaceSpecularStrength);
                SetColorIfPresent(_waterMaterial, "_SurfaceSpecularColor", profile.SurfaceSpecularColor);
                SetFloatIfPresent(_waterMaterial, "_SurfaceReflectionStrength", profile.SurfaceReflectionStrength);
                SetColorIfPresent(_waterMaterial, "_SurfaceReflectionColor", profile.SurfaceReflectionColor);
                SetFloatIfPresent(_waterMaterial, "_AbsorptionStrength", profile.WaterAbsorptionStrength);
                SetFloatIfPresent(_waterMaterial, "_UnderwaterClarity", profile.UnderwaterClarity);
                UpdateUnderwaterSceneMapping();
            }

            if (_underwaterBottomMaterial != null)
            {
                // 바닥 레이어는 RT 안에서 먼저 그려진다. 굴절은 최종 합성에서 한 번만 걸어야
                // 이중으로 휘지 않으므로 여기서는 강도를 0으로 두고, 같은 수면 필드를 공유하도록
                // scale/speed/shape만 맞춘다.
                SetFloatIfPresent(_underwaterBottomMaterial, "_OpticalDistortionStrength", 0f);
                SetFloatIfPresent(_underwaterBottomMaterial, "_OpticalDistortionScale", profile.RefractionScale);
                SetFloatIfPresent(_underwaterBottomMaterial, "_OpticalDistortionSpeed", profile.RefractionSpeed);
                SetFloatIfPresent(_underwaterBottomMaterial, "_RefractionCoefficient", profile.RefractionCoefficient);
                SetFloatIfPresent(_underwaterBottomMaterial, "_SurfaceShapeStrength", profile.SurfaceShapeStrength);
                SetFloatIfPresent(_underwaterBottomMaterial, "_LargeCausticStrength", profile.LargeCausticStrength);
                SetFloatIfPresent(_underwaterBottomMaterial, "_MidCausticStrength", profile.MidCausticStrength);
                SetFloatIfPresent(_underwaterBottomMaterial, "_MicroSurfaceStrength", profile.MicroSurfaceStrength);
            }

            if (_fishMaterial != null)
            {
                SetFloatIfPresent(_fishMaterial, "_OpticalDistortionScale", profile.RefractionScale);
                SetFloatIfPresent(_fishMaterial, "_OpticalDistortionSpeed", profile.RefractionSpeed);
                SetFloatIfPresent(_fishMaterial, "_SurfaceShapeStrength", profile.SurfaceShapeStrength);
                SetFloatIfPresent(_fishMaterial, "_WaterLightInfluence", profile.FishLightInfluence);
            }

            if (_bobber != null)
            {
                _bobber.SetPhysicalRippleStrength(profile.PhysicalRippleStrength);
            }

            ApplyCameraPresentation(profile);
        }

        /// <summary>
        /// 물 쿼드의 UV(월드 고정)와 수중 RT의 UV(카메라 고정)를 잇는 아핀 변환을 카메라의
        /// 투영에서 직접 뽑아 셰이더에 넘긴다.
        ///
        /// 두 좌표계는 원래부터 같지 않았다. 카메라가 Euler(0,180,0)이라 transform.right가
        /// (-1,0,0)이고, 그래서 월드 +X가 화면 왼쪽에 그려진다. 합성이 RT를 월드 고정 UV로
        /// 샘플링하는 동안 수중 레이어만 좌우가 뒤집혀 나왔고, 직접 렌더되는 바구니 마커와
        /// 어긋나 있었다 — 화면 왼쪽을 클릭하면 찌가 오른쪽에 뜨는 상태였다.
        ///
        /// 코너 두 개를 카메라로 투영해서 매핑을 구하면 반전·줌·프레이밍 이동이 한 번에 맞고,
        /// 나중에 카메라 규약이 또 바뀌어도 이 함수가 따라간다.
        /// </summary>
        private void UpdateUnderwaterSceneMapping()
        {
            if (_waterMaterial == null)
            {
                return;
            }

            Camera projection = _underwaterCamera != null ? _underwaterCamera : _camera;
            if (projection == null)
            {
                return;
            }

            Vector2 half = GetSurfaceSize() * 0.5f;
            Vector3 minCorner = new Vector3(_pond.center.x - half.x, _pond.center.y - half.y, 0f);
            Vector3 maxCorner = new Vector3(_pond.center.x + half.x, _pond.center.y + half.y, 0f);
            Vector3 minViewport = projection.WorldToViewportPoint(minCorner);
            Vector3 maxViewport = projection.WorldToViewportPoint(maxCorner);

            // sceneUV = offset + quadUV * scale. 축이 뒤집혀 있으면 scale이 음수로 나온다.
            Vector2 scale = new Vector2(maxViewport.x - minViewport.x, maxViewport.y - minViewport.y);
            SetVectorIfPresent(_waterMaterial, "_UnderwaterUvScale", new Vector4(scale.x, scale.y, 0f, 0f));
            SetVectorIfPresent(_waterMaterial, "_UnderwaterUvOffset", new Vector4(minViewport.x, minViewport.y, 0f, 0f));
        }

        /// <summary>
        /// 탑다운 직교 카메라에서 "물에서 얼마나 떨어져 있는가"는 orthographicSize다.
        /// z를 밀어봐야 직교 투영은 화면이 그대로라, 높이를 z로 흉내내지 않는다.
        /// </summary>
        private void ApplyCameraPresentation(FishingV2WaterProfile profile)
        {
            if (_camera == null || _gameplayOrthographicSize <= 0f)
            {
                return;
            }

            _camera.orthographicSize = _gameplayOrthographicSize * Mathf.Max(0.05f, profile.CameraHeight);
            Vector3 position = _gameplayCameraPosition;
            position.x += profile.CameraFramingShift.x;
            position.y += profile.CameraFramingShift.y;
            _camera.transform.position = position;
            // RT 카메라를 같은 호출 안에서 맞춰 둔다. 안 그러면 디버그/스크럽으로 프로파일만
            // 바꿨을 때 수중 RT가 이전 프레이밍으로 남아 화면 가장자리에 테두리가 생긴다.
            SyncUnderwaterCamera();
            // 카메라가 움직였으므로 쿼드 UV -> RT UV 매핑도 다시 잡는다.
            UpdateUnderwaterSceneMapping();
        }

        // ---------------------------------------------------------------------------------
        // Debug / A-B controls
        // ---------------------------------------------------------------------------------

        /// <summary>
        /// 디버그 컨트롤은 이미 살아 있는 런타임에만 붙는다. 에디트 모드에서 부르면
        /// InitializeRuntime()이 런타임 오브젝트를 씬에 만들어 저장 대상으로 남는다.
        /// </summary>
        private bool EnsureDebugRuntime()
        {
            if (_initialized)
            {
                return true;
            }

            if (!Application.isPlaying)
            {
                Debug.LogWarning("Fishing V2 water presentation: 재생 중에만 프로파일을 바꿀 수 있다.");
                return false;
            }

            InitializeRuntime();
            return _initialized;
        }

        [ContextMenu("Water presentation/Force Gameplay profile")]
        public void ForceGameplayWaterProfile()
        {
            if (!EnsureDebugRuntime()) return;
            _waterPresentation.ForceGameplay();
            ApplyWaterProfile(_waterPresentation.Current);
        }

        [ContextMenu("Water presentation/Force Presentation profile")]
        public void ForcePresentationWaterProfile()
        {
            if (!EnsureDebugRuntime()) return;
            _waterPresentation.ForcePresentation();
            ApplyWaterProfile(_waterPresentation.Current);
        }

        [ContextMenu("Water presentation/Force Dive peak profile")]
        public void ForceDiveWaterProfile()
        {
            if (!EnsureDebugRuntime()) return;
            _waterPresentation.ForceDivePeak();
            ApplyWaterProfile(_waterPresentation.Current);
        }

        [ContextMenu("Water presentation/Play dive transition")]
        public void PlayDiveTransition()
        {
            if (!EnsureDebugRuntime()) return;
            _waterPresentation.PlayIntro();
            ApplyWaterProfile(_waterPresentation.Current);
        }

        /// <summary>
        /// 연출 시간축의 한 지점을 직접 지정한다. 프레임 시퀀스 캡처가 프레임률과 무관해진다.
        /// </summary>
        public void ScrubDivePresentation(float time)
        {
            if (!EnsureDebugRuntime()) return;
            _waterPresentation.ScrubTo(time);
            ApplyWaterProfile(_waterPresentation.Current);
        }

        private static void SetFloatIfPresent(Material material, string property, float value)
        {
            if (material != null && material.HasProperty(property)) material.SetFloat(property, value);
        }

        private static void SetColorIfPresent(Material material, string property, Color value)
        {
            if (material != null && material.HasProperty(property)) material.SetColor(property, value);
        }

        private static void SetVectorIfPresent(Material material, string property, Vector4 value)
        {
            if (material != null && material.HasProperty(property)) material.SetVector(property, value);
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
            DestroyObjectSafe(_waterQuadMesh);
            _waterQuadMesh = null;
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
            GUI.color = new Color(0.62f, 0.78f, 0.82f, 0.72f);
            GUI.Label(
                new Rect(Screen.width - 250f, 34f, 238f, 20f),
                "water · " + _waterPresentation.Phase + " · " + _activeWaterProfile.DisplayName);

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
