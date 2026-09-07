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
        // 수중 RT를 화면보다 얼마나 넓게 찍을지. 합성이 샘플을 밀어낼 여유를 만든다.
        private const float UnderwaterRenderOverscan = 1.08f;

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
        [Tooltip("연출 값 전부를 담은 애셋. 비워 두면 코드 기본값으로 동작한다. 애셋을 쓰면 플레이 모드 중에 고친 값이 종료 후에도 남는다.")]
        public FishingV2WaterPresentationAsset WaterPresentationAsset;
        [Tooltip("애셋이 없을 때 쓰는 시간표. 애셋을 지정하면 이 값은 무시된다.")]
        public FishingV2DivePresentationTiming DivePresentationTiming = new FishingV2DivePresentationTiming();
        [Header("Debug")]
        [Tooltip("재생 중 단축키: R 세션 재시작 · T 연출 건너뛰기 · 1/2/3 프로파일 고정 · 4 착수 재생")]
        public bool EnableDebugHotkeys = true;
        [Header("Presentation assets")]
        [Tooltip("Replaceable organic substrate source sampled by WaterSurfaceV2. The prototype generator can recreate the local PNG.")]
        public Texture2D BottomSubstrateTexture;
        [Tooltip("Optional in-world catch bag image. When empty, the runtime uses the primitive prototype bag.")]
        public Texture2D CatchBagTexture;

        private readonly List<FishAgentV2> _fish = new List<FishAgentV2>();
        private readonly List<RespawnEntry> _respawns = new List<RespawnEntry>();
        private readonly Dictionary<string, int> _caught = new Dictionary<string, int>();
        private readonly Dictionary<string, Mesh> _meshCache = new Dictionary<string, Mesh>();
        private readonly Dictionary<string, FishSpeciesConfig> _speciesById = new Dictionary<string, FishSpeciesConfig>();
        private readonly Dictionary<string, Texture2D> _hudIconCache = new Dictionary<string, Texture2D>();
        private readonly HashSet<string> _releaseSpecies = new HashSet<string>();
        private static readonly string[] HudSpeciesOrder = { "anchovy", "salmon", "mahi", "squid", "tuna" };

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
        private Material _basketMaterial;
        private Material _catchBagBodyMaterial;
        private GameObject _catchBagRoot;
        private Mesh _catchBagMesh;
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
        private float _surfacePhase;
        // 착수는 여러 건이 동시에 산다 - 본 착수 하나와 크라운이 뿌린 2차 착수들.
        private const int ImpactSlots = 6;
        private readonly Vector4[] _impacts = new Vector4[ImpactSlots];
        private readonly Vector4[] _impactDetail = new Vector4[ImpactSlots];
        private readonly float[] _impactAge = new float[ImpactSlots];
        private readonly float[] _impactDuration = new float[ImpactSlots];
        private float _castBubbleAge = 99f;
        private float _castBubbleStrength;
        private float _castBubbleSeed;
        private float _castBubblePassesCamera;
        private float _crossBubbleAge = 99f;
        private float _crossBubbleSeed;
        private bool _openingCastPending;
        private float _cameraImpulseAge = 99f;
        private float _cameraImpulseStrength;
        private readonly FishingV2SplashTiming _fallbackSplashTiming = new FishingV2SplashTiming();
        private float _lensDropletAge = 99f;
        private float _lensDropletStrength;
        private float _lensDropletSeed;
        private Vector2 _lensDropletOrigin = new Vector2(0.5f, 0.5f);
        private bool _castResponsePending;
        private Vector2 _pendingCastPosition;
        private float _pendingCastRadius = 1f;
        private float _pendingCastTime = 1f;
        private float _gameplayOrthographicSize;
        private Vector3 _gameplayCameraPosition;
        private float _maxCameraHeight = 1f;
        private Vector2 _maxCameraFramingShift;
        private Mesh _waterQuadMesh;
        private bool _standaloneSmoke;
        private bool _standaloneSmokeCastSent;
        private GUIStyle _hudTitleStyle;
        private GUIStyle _hudTimerStyle;
        private GUIStyle _hudLabelStyle;
        private GUIStyle _hudCountStyle;
        private GUIStyle _hudSmallStyle;
        private GUIStyle _hudPromptStyle;
        private float _hudStyleScale = -1f;

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
        public float SurfacePhase { get { return _surfacePhase; } }
        public bool AwaitingOpeningCast { get { return _waterPresentation.IsAwaitingOpeningCast; } }
        public float HudAlpha { get { return _waterPresentation.HudAlpha; } }
        /// <summary>연출이 끝나 플레이어가 개입할 수 있는 상태인가.</summary>
        public bool SessionLive { get { return _waterPresentation.IsSessionLive; } }

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
            HandleDebugHotkeys();
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
            // 애셋을 재생 중에 편집해도 바로 보이도록 매 프레임 값을 다시 읽는다.
            // 구조체 복사 몇 번이라 비용은 무시할 수 있고, 이게 튜닝 왕복을 없앤다.
            if (WaterPresentationAsset != null)
            {
                RefreshWaterProfilesFromAsset();
            }

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
            // 수면 위상도 같은 시계를 쓴다. 절대 시각에 속도를 곱하면 프로파일이 바뀌는 순간
            // 위상이 수백 라디안 건너뛰어 수면이 흐르는 대신 스크럽되고, 연출 시간축과 다른
            // 시계를 쓰면 저프레임 에디터에서 물만 혼자 빨라진다.
            _surfacePhase += dt * Mathf.Max(0f, _waterPresentation.Current.RefractionSpeed);

            for (int i = 0; i < ImpactSlots; i++)
            {
                _impactAge[i] += dt;
            }

            _castBubbleAge += dt;
            _crossBubbleAge += dt;
            _cameraImpulseAge += dt;
            _lensDropletAge += dt;

            // 카메라가 수면을 뚫는 순간. 기포가 렌즈를 스치고, 남아 있던 물방울은 물에
            // 쓸려나간다 - 완전히 잠기면 물/물 경계라 물방울이 원리적으로 안 보인다.
            if (_waterPresentation.CrossedSurfaceThisTick)
            {
                _crossBubbleAge = 0f;
                _crossBubbleSeed = (float)_random.NextDouble();
                _lensDropletAge = Mathf.Max(_lensDropletAge, Splash.LensDropletDuration - 0.18f);
            }

            // 연출이 끝나는 순간에 시계·물고기 반응이 함께 살아난다.
            if (_castResponsePending && _waterPresentation.IsSessionLive)
            {
                _castResponsePending = false;
                ApplyCastResponse(_pendingCastPosition, _pendingCastRadius, _pendingCastTime);
            }

            // 세션 시계는 연출이 끝난 뒤에 돈다. 물 밖에서 찌 던질 자리를 고르는 동안
            // 시간이 깎이면 연출이 곧 플레이어가 치르는 비용이 된다.
            if (_running && _waterPresentation.IsSessionLive)
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
            CreateCatchBag();
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
                _bobber.ResetForNewSession();
            }

            for (int i = 0; i < ImpactSlots; i++)
            {
                _impactAge[i] = 99f;
                _impactDuration[i] = 1f;
                _impacts[i] = new Vector4(0.5f, 0.5f, -1f, 0f);
                _impactDetail[i] = Vector4.zero;
            }

            _castBubbleAge = 99f;
            _crossBubbleAge = 99f;
            _cameraImpulseAge = 99f;
            _lensDropletAge = 99f;
            _castResponsePending = false;
            _openingCastPending = false;
            if (PlaySessionDivePresentation)
            {
                // 인트로가 자동 재생되지 않는다. 물 밖에서 연못을 보다가 던진 첫 찌가
                // 잠수를 연다 — 전환의 원인이 연출이 아니라 플레이어여야 한다.
                _waterPresentation.BeginOpening();
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

            // 잠수는 착수가 아니라 '던진 순간'에 시작한다. 그래야 찌가 나는 동안 카메라가
            // 이미 내려오고, 착수 시점에 렌즈가 수면에 충분히 가까워져 물이 튈 거리가 된다.
            if (_waterPresentation.IsAwaitingOpeningCast)
            {
                _openingCastPending = true;
                _waterPresentation.ReleaseDive();
            }
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

        /// <summary>
        /// 연출을 손으로 만지기 위한 단축키. 연출 값을 고칠 때 매번 재생을 껐다 켜는 것이
        /// 가장 큰 마찰이라, 재생 중에 세션을 다시 열 수 있어야 한다.
        ///
        /// 입력 잠금보다 먼저 불린다 — 잠긴 동안에도 건너뛸 수 있어야 하기 때문이다.
        /// 에디터와 개발 빌드에서만 동작한다.
        /// </summary>
        private void HandleDebugHotkeys()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (!EnableDebugHotkeys)
            {
                return;
            }

            // R — 세션을 처음부터. 타이틀로 나가는 것에 가장 가까운 동작이다.
            if (Input.GetKeyDown(KeyCode.R))
            {
                BeginSession();
                return;
            }

            // T — 연출 건너뛰고 바로 게임플레이로.
            if (Input.GetKeyDown(KeyCode.T))
            {
                ForceGameplayWaterProfile();
                return;
            }

            if (Input.GetKeyDown(KeyCode.Alpha1)) ForceGameplayWaterProfile();
            else if (Input.GetKeyDown(KeyCode.Alpha2)) ForcePresentationWaterProfile();
            else if (Input.GetKeyDown(KeyCode.Alpha3)) ForceDiveWaterProfile();
            // 4 — 착수 연출만 그 자리에서 다시 재생. 물보라/물방울/기포만 보고 싶을 때.
            else if (Input.GetKeyDown(KeyCode.Alpha4) && _bobber != null)
            {
                TriggerSplash(_bobber.Position, true);
            }
#endif
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

            // 물 밖에서 던진 첫 찌만 풀 연출이다. 게임플레이 중에는 이미 물속에서 위를
            // 보고 있으므로 크라운·2차 착수·렌즈 물방울이 전부 성립하지 않는다 - 아래에서
            // 보이는 것(파문과 말려 들어간 공기)만 남긴다.
            bool opensSession = _openingCastPending;
            _openingCastPending = false;
            TriggerSplash(position, opensSession);

            // 카메라가 자리를 잡기 전에는 물고기가 찌에 반응하지 않는다. 놀람도 유인도
            // 플레이어가 손을 댈 수 있게 된 뒤에 걸려야 조작에 대한 응답으로 읽힌다.
            if (!_waterPresentation.IsSessionLive)
            {
                _castResponsePending = true;
                _pendingCastPosition = position;
                _pendingCastRadius = radiusMultiplier;
                _pendingCastTime = timeMultiplier;
                return;
            }

            ApplyCastResponse(position, radiusMultiplier, timeMultiplier);
        }

        /// <summary>
        /// 착수에 대한 물고기 쪽 반응. 놀람·관심 해제·유인이 전부 여기 있고, 규칙 자체는
        /// 예전과 같다 — 연출 때문에 바뀐 것은 언제 부르느냐뿐이다.
        /// </summary>
        private void ApplyCastResponse(Vector2 position, float radiusMultiplier, float timeMultiplier)
        {

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

            // 연출 중에는 입질을 받지 않는다. 플레이어가 채지도 놓지도 못하는 동안 물려서
            // 잡히면 그 한 마리는 조작과 무관하게 사라진 것이 된다.
            if (!_waterPresentation.IsSessionLive)
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

        /// <summary>
        /// 랩 여백을 넓힌 만큼 Lane 어종의 마릿수를 올린다.
        ///
        /// Lane은 직선으로 나아가다 경계에서 반대편으로 옮겨지므로, 경계가 멀어지면 한 바퀴가
        /// 길어지고 화면 안에 있는 시간의 비율이 그만큼 떨어진다. 같은 마릿수를 유지하면
        /// 화면이 눈에 띄게 비어 보인다. Loop/HoverDash는 연못 안에 고정되어 있어 영향이 없다.
        /// </summary>
        private float LaneDensityCompensation
        {
            get
            {
                // 면적비다. 임의 방향으로 도는 개체군이 고정된 창 안에 있을 확률은 창 면적을
                // 활동 면적으로 나눈 값에 비례하므로, 밀도를 유지하려면 활동 면적이 커진 만큼
                // 마릿수를 올려야 한다. 처음엔 축 길이 비(기하평균)로 잡았는데 실측 화면
                // 마릿수가 23 -> 16.5로 떨어졌다 — 길이가 아니라 면적이 맞는 모델이다.
                float halfX = _pond.width * 0.5f;
                float halfY = _pond.height * 0.5f;
                float legacy = (halfX + PathEvaluatorV2.LegacyWrapExitMargin)
                    * (halfY + PathEvaluatorV2.LegacyWrapExitMargin);
                float current = (halfX + PathEvaluatorV2.WrapExitMarginX)
                    * (halfY + PathEvaluatorV2.WrapExitMarginY);
                return current / Mathf.Max(0.01f, legacy);
            }
        }

        private void SpawnAllSpecies()
        {
            float laneScale = LaneDensityCompensation;
            foreach (FishSpeciesConfig species in _speciesById.Values)
            {
                int count = species.SpawnCount.Sample(_random);
                if (species.PathType == FishPathType.Lane)
                {
                    // 확률적 반올림. 1마리짜리 어종에서 1.49를 그냥 반올림하면 1이 되어
                    // 보정이 통째로 사라진다. 기댓값이 정확히 유지되어야 한다.
                    float scaled = count * laneScale;
                    int whole = Mathf.FloorToInt(scaled);
                    count = whole + (_random.NextDouble() < scaled - whole ? 1 : 0);
                    count = Mathf.Max(1, count);
                }
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
            // 바구니는 게임 크롬이라 HUD와 같이 사라져야 한다. 찌·캐스팅 궤적과 머티리얼을
            // 공유하면 그것들까지 같이 페이드되므로 전용 인스턴스를 따로 만든다.
            _basketMaterial = CreateMaterial(FindShader("Universal Render Pipeline/Unlit", "Unlit/Color", "Standard"));
            ConfigureTransparentLineMaterial(_basketMaterial);
            SetMaterialColor(_basketMaterial, _presentation.AccentColor);
            _catchBagBodyMaterial = CreateMaterial(FindShader("Universal Render Pipeline/Unlit", "Unlit/Color", "Standard"));
            ConfigureTransparentLineMaterial(_catchBagBodyMaterial);
            if (CatchBagTexture != null)
            {
                if (_catchBagBodyMaterial.HasProperty("_BaseMap")) _catchBagBodyMaterial.SetTexture("_BaseMap", CatchBagTexture);
                if (_catchBagBodyMaterial.HasProperty("_MainTex")) _catchBagBodyMaterial.SetTexture("_MainTex", CatchBagTexture);
                SetMaterialColor(_catchBagBodyMaterial, Color.white);
            }
            else
            {
                SetMaterialColor(_catchBagBodyMaterial, new Color(0.58f, 0.38f, 0.15f, 1f));
            }
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
            // RT를 화면보다 넓게 찍는다.
            //
            // 합성은 굴절·파문·물방울로 샘플 위치를 밀어내는데, RT가 화면과 정확히 같으면
            // 화면 끝에서 민 만큼이 RT 밖을 가리키게 된다. 그러면 마지막 픽셀 줄이 늘어나
            // 물고기가 막대로 뭉개진다. 여유를 두면 밀어낸 자리에도 실제 내용이 있다.
            //
            // 대가는 유효 해상도가 그만큼 낮아지는 것뿐이라 8%면 눈에 띄지 않는다.
            _underwaterCamera.orthographicSize = _camera.orthographicSize * UnderwaterRenderOverscan;
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

        /// <summary>
        /// 착수. full이면 함몰·크라운·제트와 크라운이 뿌린 2차 착수, 렌즈 물방울까지 전부
        /// 붙는다. 아니면 아래에서 보이는 것 — 파문과 말려 들어간 공기 — 만 남는다.
        /// </summary>
        private void TriggerSplash(Vector2 worldPosition, bool full)
        {
            Vector2 uv = WorldToWaterUv(worldPosition);
            float strength = full ? 1f : 0.55f;

            // 파문은 멀리 퍼지며 옅어지므로 수명이 길어야 한다.
            FishingV2SplashTiming splash = Splash;
            SpawnImpact(0, uv, strength, full ? 1f : 0f, 1f,
                full ? splash.PrimaryDuration : splash.GameplayDuration, 0f);

            if (full)
            {
                // 크라운이 뿌린 물이 다시 떨어진다. 본 착수보다 작고 늦고 약하다.
                int secondary = Mathf.Clamp(splash.SecondaryCount, 0, ImpactSlots - 1);
                for (int i = 1; i < ImpactSlots; i++)
                {
                    if (i > secondary)
                    {
                        _impactAge[i] = 99f;
                        continue;
                    }

                    float angle = (float)(_random.NextDouble() * Mathf.PI * 2.0);
                    float distance = Mathf.Lerp(splash.SecondaryDistanceMin, splash.SecondaryDistanceMax, (float)_random.NextDouble());
                    Vector2 offset = new Vector2(Mathf.Cos(angle) / 1.92f, Mathf.Sin(angle)) * distance;
                    // 2차 착수는 작은 물방울이 떨어진 것이라 파문도 작아야 한다. 본 착수와
                    // 같은 크기로 퍼지면 어느 것이 본 착수인지 읽히지 않는다.
                    SpawnImpact(
                        i,
                        uv + offset,
                        Mathf.Lerp(splash.SecondaryStrengthMin, splash.SecondaryStrengthMax, (float)_random.NextDouble()),
                        0.30f,
                        Mathf.Lerp(splash.SecondaryRingScaleMin, splash.SecondaryRingScaleMax, (float)_random.NextDouble()),
                        splash.SecondaryDuration,
                        Mathf.Lerp(splash.SecondaryDelayMin, splash.SecondaryDelayMax, (float)_random.NextDouble()));
                }

                _lensDropletAge = 0f;
                _lensDropletStrength = 1f;
                _lensDropletSeed = (float)_random.NextDouble();
                if (_camera != null)
                {
                    // 물방울은 렌즈에 붙는 것이라 튀어나온 자리를 화면 좌표로 한 번만 기록한다.
                    Vector3 viewport = _camera.WorldToViewportPoint(new Vector3(worldPosition.x, worldPosition.y, 0f));
                    _lensDropletOrigin = new Vector2(viewport.x, viewport.y);
                }
            }
            else
            {
                for (int i = 1; i < ImpactSlots; i++)
                {
                    _impactAge[i] = 99f;
                }
            }

            // 말려 들어간 공기. 물속에서 던졌을 때 남는 유일한 '물이 튀었다'의 증거다.
            _castBubbleAge = 0f;
            _castBubbleStrength = full ? 0.85f : splash.GameplayBubbleStrength;
            // 물속에서 던지면 수면이 카메라 뒤에 있다. 기포는 렌즈를 지나쳐 화면 밖으로
            // 나가고 터지지 않는다 — 밖에서 볼 때와는 다른 궤적이다.
            _castBubblePassesCamera = full ? 0f : 1f;
            _castBubbleSeed = (float)_random.NextDouble();

            _cameraImpulseAge = 0f;
            // 물속 카메라가 수면 충격에 크게 흔들리면 이상하다. 무게만 남기고 줄인다.
            _cameraImpulseStrength = full ? 1f : splash.GameplayImpulseScale;
            _waterPresentation.PushSurfacePulse(strength);
        }

        private void SpawnImpact(int slot, Vector2 uv, float strength, float detail, float ringScale, float duration, float delay)
        {
            _impactAge[slot] = -Mathf.Max(0f, delay);
            _impactDuration[slot] = Mathf.Max(0.05f, duration);
            _impacts[slot] = new Vector4(uv.x, uv.y, -1f, Mathf.Clamp01(strength));
            _impactDetail[slot] = new Vector4(Mathf.Clamp01(detail), Mathf.Max(0.05f, ringScale), 0f, 0f);
        }

        private Vector2 WorldToWaterUv(Vector2 worldPosition)
        {
            Vector2 size = GetSurfaceSize();
            return new Vector2(
                (worldPosition.x - _pond.center.x) / Mathf.Max(0.01f, size.x) + 0.5f,
                (worldPosition.y - _pond.center.y) / Mathf.Max(0.01f, size.y) + 0.5f);
        }

        /// <summary>
        /// 애셋의 현재 값을 디렉터에 밀어 넣는다. 진행 중인 연출 상태는 보존된다.
        ///
        /// 물 쿼드 크기는 세션 시작 시의 최대 CameraHeight로 정해지므로, 애셋에서 그 값을
        /// 크게 올리면 화면 가장자리가 덮이지 않을 수 있다. 그때는 R로 세션을 다시 열면 된다.
        /// </summary>
        private void RefreshWaterProfilesFromAsset()
        {
            _gameplayWaterProfile = WaterPresentationAsset.Gameplay.ToProfile();
            _presentationWaterProfile = WaterPresentationAsset.AboveWater.ToProfile();
            _diveWaterProfile = WaterPresentationAsset.NearSurface.ToProfile();
            _waterPresentation.UpdateProfiles(
                _gameplayWaterProfile,
                _presentationWaterProfile,
                _diveWaterProfile,
                WaterPresentationAsset.Timing);
        }

        /// <summary>착수 사건들의 시간표. 애셋이 없으면 코드 기본값.</summary>
        private FishingV2SplashTiming Splash
        {
            get
            {
                return WaterPresentationAsset != null && WaterPresentationAsset.Splash != null
                    ? WaterPresentationAsset.Splash
                    : _fallbackSplashTiming;
            }
        }

        private void BuildWaterProfiles()
        {
            // 애셋이 있으면 거기서, 없으면 코드 기본값에서. 애셋 쪽이 편집 가능한 표면이고,
            // 코드 쪽은 애셋을 안 만든 씬이 그대로 돌아가게 하는 안전망이다.
            if (WaterPresentationAsset != null)
            {
                _gameplayWaterProfile = WaterPresentationAsset.Gameplay.ToProfile();
                _presentationWaterProfile = WaterPresentationAsset.AboveWater.ToProfile();
                _diveWaterProfile = WaterPresentationAsset.NearSurface.ToProfile();
            }
            else
            {
                _gameplayWaterProfile = FishingV2WaterProfile.Gameplay(_presentation);
                _presentationWaterProfile = FishingV2WaterProfile.PresentationAboveWater(_presentation);
                _diveWaterProfile = FishingV2WaterProfile.DiveTransition(_presentation);
            }

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
                WaterPresentationAsset != null && WaterPresentationAsset.Timing != null
                    ? WaterPresentationAsset.Timing
                    : DivePresentationTiming);
        }

        /// <summary>
        /// 프로파일 하나를 화면에 바른다. 상태 전환이 여기 한 곳만 지나므로 새 연출을 붙일 때
        /// 머티리얼 프로퍼티 이름을 다시 찾아다닐 필요가 없다.
        /// </summary>
        private void ApplyWaterProfile(FishingV2WaterProfile profile)
        {
            _activeWaterProfile = profile;
            // 비네트를 화면에 붙이는 계수. 카메라가 뒤로 빠진 만큼 반경을 되돌린다.
            float vignetteScale = 1f / Mathf.Max(0.05f, profile.CameraHeight);

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
                SetFloatIfPresent(_waterMaterial, "_VignetteScale", vignetteScale);
                SetFloatIfPresent(_waterMaterial, "_SurfacePhase", _surfacePhase);
                for (int i = 0; i < ImpactSlots; i++)
                {
                    float age01 = _impactAge[i] / _impactDuration[i];
                    Vector4 impact = _impacts[i];
                    impact.z = (age01 >= 0f && age01 <= 1f) ? age01 : -1f;
                    _impacts[i] = impact;
                }

                _waterMaterial.SetVectorArray("_Impacts", _impacts);
                _waterMaterial.SetVectorArray("_ImpactDetail", _impactDetail);

                // 물 밖 착수와 물속 착수는 기포가 다르게 움직이므로 수명도 따로 쓴다.
                float bubbleDuration = _castBubblePassesCamera > 0.5f
                    ? Splash.GameplayBubbleDuration
                    : Splash.CastBubbleDuration;
                float castBubble01 = _castBubbleAge / Mathf.Max(0.05f, bubbleDuration);
                SetVectorIfPresent(_waterMaterial, "_CastBubbles", new Vector4(
                    castBubble01 <= 1f ? castBubble01 : -1f,
                    _castBubbleStrength,
                    _castBubbleSeed,
                    _castBubblePassesCamera));
                SetVectorIfPresent(_waterMaterial, "_BubbleTuning", new Vector4(
                    Splash.GameplayBubbleRushSpeed, Splash.CrossBubbleRushSpeed, 0f, 0f));
                float crossBubble01 = _crossBubbleAge / Mathf.Max(0.05f, Splash.CrossBubbleDuration);
                SetVectorIfPresent(_waterMaterial, "_CrossBubbles", new Vector4(
                    crossBubble01 <= 1f ? crossBubble01 : -1f, 1f, _crossBubbleSeed, 0f));

                float droplet01 = _lensDropletAge / Mathf.Max(0.05f, Splash.LensDropletDuration);
                SetVectorIfPresent(_waterMaterial, "_LensDroplets", new Vector4(
                    droplet01 <= 1f ? droplet01 : -1f,
                    _lensDropletStrength,
                    _lensDropletSeed,
                    0f));
                SetVectorIfPresent(_waterMaterial, "_LensOrigin", new Vector4(
                    _lensDropletOrigin.x, _lensDropletOrigin.y, 0f, 0f));
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
                SetFloatIfPresent(_underwaterBottomMaterial, "_SurfacePhase", _surfacePhase);
                SetFloatIfPresent(_underwaterBottomMaterial, "_VignetteScale", vignetteScale);
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
                SetFloatIfPresent(_fishMaterial, "_SurfacePhase", _surfacePhase);
            }

            if (_bobber != null)
            {
                _bobber.SetPhysicalRippleStrength(profile.PhysicalRippleStrength);
            }

            if (_basketMaterial != null)
            {
                Color basket = _presentation.AccentColor;
                basket.a *= Mathf.Clamp01(_waterPresentation.HudAlpha);
                SetMaterialColor(_basketMaterial, basket);
            }

            if (_catchBagBodyMaterial != null)
            {
                float bagAlpha = Mathf.Clamp01(_waterPresentation.HudAlpha);
                SetMaterialColor(_catchBagBodyMaterial, CatchBagTexture != null
                    ? new Color(1f, 1f, 1f, bagAlpha)
                    : new Color(0.58f, 0.38f, 0.15f, bagAlpha));
                if (_catchBagRoot != null) _catchBagRoot.SetActive(bagAlpha > 0.01f);
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

            float orthographicSize = _gameplayOrthographicSize * Mathf.Max(0.05f, profile.CameraHeight);
            Vector3 position = _gameplayCameraPosition;
            position.x += profile.CameraFramingShift.x;
            position.y += profile.CameraFramingShift.y;

            // 착수 충격. 흔드는 것이 아니라 한 번 내려앉았다 돌아오는 단발이다 — 떨림은
            // 이 게임의 관찰 리듬을 깨고, 탑다운에서는 그냥 화면 결함처럼 보인다.
            float impulseDuration = Mathf.Max(0.05f, Splash.CameraImpulseDuration);
            if (_cameraImpulseAge < impulseDuration)
            {
                float k = _cameraImpulseAge / impulseDuration;
                float decay = (1f - k) * (1f - k);
                float dip = Mathf.Sin(k * Mathf.PI * 1.5f) * decay;
                position.y -= dip * 0.085f * _cameraImpulseStrength;
                orthographicSize *= 1f - dip * 0.014f * _cameraImpulseStrength;
            }

            _camera.orthographicSize = orthographicSize;
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

        [ContextMenu("Water presentation/Begin opening (await cast)")]
        public void BeginOpeningPresentation()
        {
            if (!EnsureDebugRuntime()) return;
            _waterPresentation.BeginOpening();
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
        /// 지금 상태를 머티리얼에 다시 바른다. 평소에는 LateUpdate가 하지만, 캡처 도구가
        /// SimulateStep으로 시뮬레이션을 직접 돌릴 때는 그 경로를 안 지나므로 필요하다.
        /// </summary>
        public void ApplyCurrentWaterState()
        {
            if (!_initialized) return;
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

        private void CreateCatchBag()
        {
            _catchBagRoot = new GameObject("FishingCatchBag");
            _catchBagRoot.transform.SetParent(transform, false);
            Vector3 safeBagPosition = _camera != null
                ? _camera.ViewportToWorldPoint(new Vector3(0.10f, 0.16f, Mathf.Abs(_camera.transform.position.z)))
                : new Vector3(_pond.xMax - 0.95f, _pond.yMin + 0.72f, 0.12f);
            _catchBagRoot.transform.position = new Vector3(safeBagPosition.x, safeBagPosition.y, 0.12f);

            if (CatchBagTexture != null)
            {
                CreateTexturedCatchBag();
                return;
            }

            _catchBagMesh = new Mesh { name = "FishingCatchBag_Body" };
            _catchBagMesh.vertices = new[]
            {
                new Vector3(-0.53f, -0.52f, 0f),
                new Vector3(0.53f, -0.52f, 0f),
                new Vector3(0.43f, 0.38f, 0f),
                new Vector3(-0.43f, 0.38f, 0f)
            };
            _catchBagMesh.triangles = new[] { 0, 1, 2, 0, 2, 3 };
            _catchBagMesh.RecalculateNormals();
            _catchBagMesh.RecalculateBounds();

            GameObject body = new GameObject("BagBody");
            body.transform.SetParent(_catchBagRoot.transform, false);
            body.AddComponent<MeshFilter>().sharedMesh = _catchBagMesh;
            body.AddComponent<MeshRenderer>().sharedMaterial = _catchBagBodyMaterial;

            CreateBagBlock("BagRim", new Vector3(0f, 0.39f, 0.02f), new Vector3(0.92f, 0.13f, 0.07f), _basketMaterial);
            CreateBagBlock("FrontPocket", new Vector3(0f, -0.14f, 0.025f), new Vector3(0.58f, 0.30f, 0.08f), _basketMaterial);
            CreateBagBlock("PocketFlap", new Vector3(0f, 0.02f, 0.07f), new Vector3(0.60f, 0.08f, 0.04f), _basketMaterial);

            GameObject handleObject = new GameObject("BagHandle");
            handleObject.transform.SetParent(_catchBagRoot.transform, false);
            LineRenderer handle = handleObject.AddComponent<LineRenderer>();
            handle.useWorldSpace = false;
            handle.sharedMaterial = _basketMaterial;
            handle.widthMultiplier = 0.065f;
            handle.numCapVertices = 3;
            handle.numCornerVertices = 2;
            handle.positionCount = 9;
            for (int i = 0; i < handle.positionCount; i++)
            {
                float t = i / (float)(handle.positionCount - 1);
                handle.SetPosition(i, new Vector3(
                    Mathf.Lerp(-0.34f, 0.34f, t),
                    0.42f + Mathf.Sin(t * Mathf.PI) * 0.42f,
                    0.04f));
            }
        }

        private void CreateTexturedCatchBag()
        {
            float aspect = CatchBagTexture.width / (float)Mathf.Max(1, CatchBagTexture.height);
            float halfHeight = 0.78f;
            float halfWidth = halfHeight * aspect;
            _catchBagMesh = new Mesh { name = "FishingCatchBag_Image" };
            _catchBagMesh.vertices = new[]
            {
                new Vector3(-halfWidth, -halfHeight, 0f),
                new Vector3(halfWidth, -halfHeight, 0f),
                new Vector3(halfWidth, halfHeight, 0f),
                new Vector3(-halfWidth, halfHeight, 0f)
            };
            _catchBagMesh.uv = new[]
            {
                new Vector2(0f, 0f),
                new Vector2(1f, 0f),
                new Vector2(1f, 1f),
                new Vector2(0f, 1f)
            };
            _catchBagMesh.triangles = new[] { 0, 1, 2, 0, 2, 3 };
            _catchBagMesh.RecalculateNormals();
            _catchBagMesh.RecalculateBounds();

            GameObject image = new GameObject("BagImage");
            image.transform.SetParent(_catchBagRoot.transform, false);
            image.AddComponent<MeshFilter>().sharedMesh = _catchBagMesh;
            image.AddComponent<MeshRenderer>().sharedMaterial = _catchBagBodyMaterial;
        }

        private void CreateBagBlock(string name, Vector3 localPosition, Vector3 localScale, Material material)
        {
            GameObject block = GameObject.CreatePrimitive(PrimitiveType.Cube);
            block.name = name;
            block.transform.SetParent(_catchBagRoot.transform, false);
            block.transform.localPosition = localPosition;
            block.transform.localScale = localScale;
            block.GetComponent<MeshRenderer>().sharedMaterial = material;
            Collider collider = block.GetComponent<Collider>();
            if (collider != null) DestroyObjectSafe(collider);
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
            DestroyObjectSafe(_basketMaterial);
            _basketMaterial = null;
            DestroyObjectSafe(_catchBagBodyMaterial);
            _catchBagBodyMaterial = null;
            DestroyObjectSafe(_catchBagMesh);
            _catchBagMesh = null;
            DestroyObjectSafe(_catchBagRoot);
            _catchBagRoot = null;
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

            float hud = _waterPresentation.HudAlpha;
            if (_waterPresentation.IsAwaitingOpeningCast)
            {
                DrawOpeningPrompt();
            }

            // 게임 크롬은 연출이 끝나면서 올라온다. 물이 주인공인 구간에 점수판이 떠 있으면
            // 그 구간이 연출이 아니라 로딩 화면으로 읽힌다.
            if (hud <= 0.01f)
            {
                GUI.color = Color.white;
                return;
            }

            float scale = Mathf.Clamp(Screen.height / 900f, 0.78f, 1.28f);
            EnsureHudStyles(scale);
            DrawCatchHud(hud, scale);
            DrawControlGuide(hud, scale);
            DrawGameplayPrompt(hud, scale);

            GUI.color = Color.white;
        }

        private void EnsureHudStyles(float scale)
        {
            if (_hudTitleStyle != null && Mathf.Abs(_hudStyleScale - scale) < 0.01f) return;
            _hudStyleScale = scale;

            _hudTitleStyle = CreateHudStyle(Mathf.RoundToInt(13f * scale), FontStyle.Bold, TextAnchor.MiddleLeft);
            _hudTimerStyle = CreateHudStyle(Mathf.RoundToInt(24f * scale), FontStyle.Bold, TextAnchor.MiddleRight);
            _hudLabelStyle = CreateHudStyle(Mathf.RoundToInt(12f * scale), FontStyle.Normal, TextAnchor.MiddleLeft);
            _hudCountStyle = CreateHudStyle(Mathf.RoundToInt(15f * scale), FontStyle.Bold, TextAnchor.MiddleCenter);
            _hudSmallStyle = CreateHudStyle(Mathf.RoundToInt(10f * scale), FontStyle.Normal, TextAnchor.MiddleCenter);
            _hudPromptStyle = CreateHudStyle(Mathf.RoundToInt(14f * scale), FontStyle.Bold, TextAnchor.MiddleCenter);
        }

        private static GUIStyle CreateHudStyle(int fontSize, FontStyle fontStyle, TextAnchor alignment)
        {
            GUIStyle style = new GUIStyle(GUI.skin.label)
            {
                fontSize = fontSize,
                fontStyle = fontStyle,
                alignment = alignment,
                clipping = TextClipping.Clip
            };
            style.normal.textColor = Color.white;
            return style;
        }

        private void DrawCatchHud(float hud, float scale)
        {
            float margin = 18f * scale;
            float width = 398f * scale;
            float height = 150f * scale;
            Rect panel = new Rect(margin, margin, width, height);
            DrawHudRect(panel, new Color(0.015f, 0.045f, 0.055f, 0.88f), hud);
            DrawHudRect(new Rect(panel.x, panel.y, 5f * scale, panel.height), _presentation.AccentColor, hud);

            GUI.color = Fade(new Color(0.76f, 0.91f, 0.91f, 1f), hud);
            GUI.Label(new Rect(panel.x + 17f * scale, panel.y + 11f * scale, 180f * scale, 23f * scale), "잔잔한 낚시터", _hudTitleStyle);
            GUI.color = Fade(Color.white, hud);
            GUI.Label(new Rect(panel.x + 226f * scale, panel.y + 5f * scale, 153f * scale, 36f * scale), FormatTime(_timeLeft), _hudTimerStyle);

            DrawHudRect(new Rect(panel.x + 17f * scale, panel.y + 40f * scale, 105f * scale, 24f * scale), new Color(0.07f, 0.17f, 0.18f, 0.92f), hud);
            GUI.color = Fade(_presentation.AccentColor, hud);
            GUI.Label(new Rect(panel.x + 23f * scale, panel.y + 40f * scale, 94f * scale, 24f * scale), "점수  " + _score, _hudTitleStyle);

            float rowY = panel.y + 72f * scale;
            float slotWidth = 72f * scale;
            for (int i = 0; i < HudSpeciesOrder.Length; i++)
            {
                string speciesId = HudSpeciesOrder[i];
                if (!_speciesById.TryGetValue(speciesId, out FishSpeciesConfig species) || species == null) continue;

                float x = panel.x + 16f * scale + i * slotWidth;
                Rect iconBack = new Rect(x, rowY, 64f * scale, 42f * scale);
                DrawHudRect(iconBack, new Color(0.045f, 0.105f, 0.115f, 0.95f), hud);
                Texture2D icon = species.HudIcon != null ? species.HudIcon : GetOrCreateHudIcon(species);
                if (icon != null)
                {
                    GUI.color = Fade(Color.white, hud);
                    float portraitScale = Mathf.Clamp(species.HudIconScale, 0.45f, 1f);
                    float portraitWidth = 60f * portraitScale * scale;
                    float portraitHeight = 34f * portraitScale * scale;
                    float portraitX = x + (64f * scale - portraitWidth) * 0.5f;
                    float portraitY = rowY + (36f * scale - portraitHeight) * 0.5f;
                    GUI.DrawTexture(
                        new Rect(portraitX, portraitY, portraitWidth, portraitHeight),
                        icon,
                        ScaleMode.StretchToFill,
                        true);
                }

                int count = _caught.TryGetValue(speciesId, out int value) ? value : 0;
                GUI.color = Fade(new Color(0.91f, 0.96f, 0.95f, 1f), hud);
                GUI.Label(new Rect(x, rowY + 37f * scale, 64f * scale, 22f * scale), "× " + count, _hudCountStyle);
                GUI.color = Fade(new Color(0.62f, 0.75f, 0.75f, 1f), hud);
                GUI.Label(new Rect(x, rowY + 57f * scale, 64f * scale, 17f * scale), species.DisplayName, _hudSmallStyle);
            }
        }

        private void DrawControlGuide(float hud, float scale)
        {
            float margin = 18f * scale;
            float width = 282f * scale;
            float height = 134f * scale;
            Rect panel = new Rect(Screen.width - width - margin, margin, width, height);
            DrawHudRect(panel, new Color(0.015f, 0.045f, 0.055f, 0.84f), hud);

            GUI.color = Fade(_presentation.AccentColor, hud);
            GUI.Label(new Rect(panel.x + 14f * scale, panel.y + 8f * scale, 100f * scale, 24f * scale), "조작", _hudTitleStyle);
            DrawControlRow(panel, 36f, "CLICK", "찌 배치 · 입질 중 즉시 회수", hud, scale);
            DrawControlRow(panel, 68f, "R", "새 세션", hud, scale);
            DrawControlRow(panel, 100f, "T", "시작 연출 건너뛰기", hud, scale);
        }

        private void DrawControlRow(Rect panel, float y, string key, string description, float hud, float scale)
        {
            Rect keyRect = new Rect(panel.x + 14f * scale, panel.y + y * scale, 54f * scale, 23f * scale);
            DrawHudRect(keyRect, new Color(0.10f, 0.21f, 0.22f, 1f), hud);
            GUI.color = Fade(new Color(1f, 0.78f, 0.37f, 1f), hud);
            GUI.Label(keyRect, key, _hudSmallStyle);
            GUI.color = Fade(new Color(0.84f, 0.92f, 0.92f, 1f), hud);
            GUI.Label(new Rect(panel.x + 78f * scale, panel.y + y * scale, 188f * scale, 23f * scale), description, _hudLabelStyle);
        }

        private void DrawGameplayPrompt(float hud, float scale)
        {
            string prompt = null;
            if (!_running) prompt = "세션 종료 · R 키로 다시 시작";
            else if ((_catchFlight == null || _catchFlight.ActiveFlightCount == 0) &&
                     (_bobber == null || !_bobber.IsInWater))
            {
                prompt = "수면을 클릭해 찌를 던지세요";
            }
            if (string.IsNullOrEmpty(prompt)) return;

            float width = 340f * scale;
            Rect panel = new Rect((Screen.width - width) * 0.5f, Screen.height - 70f * scale, width, 38f * scale);
            DrawHudRect(panel, new Color(0.01f, 0.035f, 0.045f, 0.82f), hud);
            GUI.color = Fade(new Color(0.90f, 0.96f, 0.96f, 1f), hud);
            GUI.Label(panel, prompt, _hudPromptStyle);
        }

        private static void DrawHudRect(Rect rect, Color color, float hud)
        {
            GUI.color = Fade(color, hud);
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
        }

        private Texture2D GetOrCreateHudIcon(FishSpeciesConfig species)
        {
            if (species == null || string.IsNullOrEmpty(species.SpeciesId)) return null;
            if (_hudIconCache.TryGetValue(species.SpeciesId, out Texture2D cached) && cached != null) return cached;

            Texture2D icon = BuildPrototypeHudIcon(species);
            _hudIconCache[species.SpeciesId] = icon;
            return icon;
        }

        private static Texture2D BuildPrototypeHudIcon(FishSpeciesConfig species)
        {
            const int width = 96;
            const int height = 48;
            Color32[] pixels = new Color32[width * height];
            FishVisualSpec visual = species.Visual ?? new FishVisualSpec();
            Color body = visual.BaseColor;
            Color edge = visual.EdgeColor;
            Color fin = visual.FinColor;

            bool squid = visual.Arms != null && visual.Arms.Count > 0;
            if (squid)
            {
                DrawIconEllipse(pixels, width, height, 59, 24, 24, 12, body, edge);
                for (int i = 0; i < 7; i++)
                {
                    int offset = i - 3;
                    DrawIconLine(pixels, width, height, 38, 24 + offset * 2, 8, 18 + offset * 3, fin, 2);
                }
            }
            else
            {
                DrawIconTriangle(pixels, width, height, new Vector2(31, 24), new Vector2(9, 8), new Vector2(12, 39), fin);
                DrawIconEllipse(pixels, width, height, 56, 24, 29, 12, body, edge);
                DrawIconTriangle(pixels, width, height, new Vector2(49, 17), new Vector2(36, 5), new Vector2(61, 17), fin);
            }

            DrawIconCircle(pixels, width, height, 75, 20, 4, Color.white);
            DrawIconCircle(pixels, width, height, 76, 20, 2, new Color(0.015f, 0.025f, 0.03f, 1f));

            Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                name = "HudIcon_Prototype_" + species.SpeciesId,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.DontSave
            };
            texture.SetPixels32(pixels);
            texture.Apply(false, false);
            return texture;
        }

        private static void DrawIconEllipse(Color32[] pixels, int width, int height, int cx, int cy, int rx, int ry, Color center, Color edge)
        {
            for (int y = -ry; y <= ry; y++)
            {
                for (int x = -rx; x <= rx; x++)
                {
                    float distance = x * x / (float)(rx * rx) + y * y / (float)(ry * ry);
                    if (distance > 1f) continue;
                    SetIconPixel(pixels, width, height, cx + x, cy + y, Color.Lerp(center, edge, Mathf.Clamp01(distance * 0.62f)));
                }
            }
        }

        private static void DrawIconCircle(Color32[] pixels, int width, int height, int cx, int cy, int radius, Color color)
        {
            for (int y = -radius; y <= radius; y++)
            for (int x = -radius; x <= radius; x++)
                if (x * x + y * y <= radius * radius) SetIconPixel(pixels, width, height, cx + x, cy + y, color);
        }

        private static void DrawIconTriangle(Color32[] pixels, int width, int height, Vector2 a, Vector2 b, Vector2 c, Color color)
        {
            int minX = Mathf.FloorToInt(Mathf.Min(a.x, Mathf.Min(b.x, c.x)));
            int maxX = Mathf.CeilToInt(Mathf.Max(a.x, Mathf.Max(b.x, c.x)));
            int minY = Mathf.FloorToInt(Mathf.Min(a.y, Mathf.Min(b.y, c.y)));
            int maxY = Mathf.CeilToInt(Mathf.Max(a.y, Mathf.Max(b.y, c.y)));
            float area = Cross2D(b - a, c - a);
            if (Mathf.Abs(area) < 0.001f) return;
            for (int y = minY; y <= maxY; y++)
            for (int x = minX; x <= maxX; x++)
            {
                Vector2 p = new Vector2(x + 0.5f, y + 0.5f);
                float ab = Cross2D(b - a, p - a) / area;
                float bc = Cross2D(c - b, p - b) / area;
                float ca = Cross2D(a - c, p - c) / area;
                if (ab >= 0f && bc >= 0f && ca >= 0f) SetIconPixel(pixels, width, height, x, y, color);
            }
        }

        private static void DrawIconLine(Color32[] pixels, int width, int height, int x0, int y0, int x1, int y1, Color color, int thickness)
        {
            int steps = Mathf.Max(Mathf.Abs(x1 - x0), Mathf.Abs(y1 - y0));
            for (int i = 0; i <= steps; i++)
            {
                float t = steps > 0 ? i / (float)steps : 0f;
                int x = Mathf.RoundToInt(Mathf.Lerp(x0, x1, t));
                int y = Mathf.RoundToInt(Mathf.Lerp(y0, y1, t));
                DrawIconCircle(pixels, width, height, x, y, thickness, color);
            }
        }

        private static float Cross2D(Vector2 a, Vector2 b)
        {
            return a.x * b.y - a.y * b.x;
        }

        private static void SetIconPixel(Color32[] pixels, int width, int height, int x, int y, Color color)
        {
            if (x < 0 || x >= width || y < 0 || y >= height) return;
            pixels[y * width + x] = color;
        }

        /// <summary>
        /// 물 밖 대기 화면의 유일한 UI. 이 상태를 끝내는 행동이 캐스팅이라, 무엇을 해야 하는지는
        /// 알려줘야 한다. 크롬이 아니므로 점수판과 같이 페이드되지 않는다.
        /// </summary>
        private void DrawOpeningPrompt()
        {
            float pulse = 0.72f + 0.28f * Mathf.Sin(_now * 2.6f);
            GUI.color = new Color(0.86f, 0.93f, 0.95f, 0.92f * pulse);
            GUIStyle style = new GUIStyle(GUI.skin.label);
            style.alignment = TextAnchor.MiddleCenter;
            style.fontSize = 17;
            GUI.Label(new Rect(0f, Screen.height - 96f, Screen.width, 28f), "수면을 터치해 찌를 던지세요", style);
            GUI.color = Color.white;
        }

        private static Color Fade(Color color, float alpha)
        {
            color.a *= Mathf.Clamp01(alpha);
            return color;
        }

        private static string FormatTime(float seconds)
        {
            int whole = Mathf.CeilToInt(Mathf.Max(0f, seconds));
            return string.Format("{0:00}:{1:00}", whole / 60, whole % 60);
        }
    }
}
