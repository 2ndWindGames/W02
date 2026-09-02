using System.Collections.Generic;
using UnityEngine;

namespace Fishing
{
    /// <summary>
    /// 세션 오케스트레이션 — 타이머, 스폰, 집계. 기획서 §2, §9.
    ///
    /// 물고기 30~50마리 수준이라 MonoBehaviour + Update 로 충분하다.
    /// DOTS/Job 불필요. 조기 최적화 금지.
    /// </summary>
    public class FishingSession : MonoBehaviour
    {
        [Header("구성")]
        [SerializeField] FishingSpotSO _spot;
        [SerializeField] Bobber _bobber;
        [SerializeField] Fish _fishPrefab;
        [SerializeField] Transform _fishRoot;
        [SerializeField] CatchFlight _catchFlight;

        [Header("수면 — 기준 16 x 9 유닛")]
        [SerializeField] Vector2 _waterSize = new Vector2(16f, 9f);
        [SerializeField] Vector2 _waterCenter = Vector2.zero;

        [Header("지형지물 (복어 앵커)")]
        [SerializeField] Transform[] _rocks;

        public Rect Water { get; private set; }
        public bool IsRunning { get; private set; }
        public float TimeLeft { get; private set; }
        public int Score { get; private set; }
        public IReadOnlyList<Fish> Fishes => _fishes;

        /// <summary>선별 회수 — 해금 요소(§8-5). true면 잡지 않고 놓아준다.</summary>
        public readonly HashSet<string> ReleaseSpecies = new HashSet<string>();
        /// <summary>자동 회수. 기본 ON — 손을 놓아도 돌아가는 게 이 게임의 기본 상태다.</summary>
        public bool AutoReel = true;

        readonly List<Fish> _fishes = new List<Fish>();
        readonly Stack<Fish> _pool = new Stack<Fish>();
        readonly Dictionary<string, int> _caught = new Dictionary<string, int>();
        readonly Dictionary<FishSpeciesSO, float> _respawnTimers = new Dictionary<FishSpeciesSO, float>();
        readonly Dictionary<FishSpeciesSO, int> _targetCount = new Dictionary<FishSpeciesSO, int>();

        public System.Action<FishSpeciesSO, int, int> OnTallyChanged;   // 어종, 마릿수, 총점
        public System.Action OnSessionEnded;

        void Awake()
        {
            Water = new Rect(_waterCenter - _waterSize * 0.5f, _waterSize);
            if (_bobber) _bobber.Bind(this);
        }

        void Start() => BeginSession();

        // ── 세션 ─────────────────────────────────────────────────
        public void BeginSession()
        {
            foreach (var f in _fishes) Despawn(f);
            _fishes.Clear();
            _caught.Clear();
            _respawnTimers.Clear();
            _targetCount.Clear();
            Score = 0;
            TimeLeft = _spot ? _spot.sessionSeconds : 90f;
            IsRunning = true;
            if (_bobber) _bobber.Retract();
            TopUp(initial: true);
        }

        public void EndSession()
        {
            IsRunning = false;
            if (_bobber) _bobber.Retract();
            OnSessionEnded?.Invoke();
        }

        void Update()
        {
            float dt = FishingTuning.Dt;

            if (IsRunning)
            {
                TimeLeft -= dt;
                if (TimeLeft <= 0f) { TimeLeft = 0f; EndSession(); }
            }

            if (_bobber) _bobber.Tick(dt);

            for (int i = _fishes.Count - 1; i >= 0; i--)
                _fishes[i].Tick(dt, _bobber, Water);

            if (IsRunning) TickRespawn(dt);
            HandleInput();
        }

        void HandleInput()
        {
            if (!IsRunning) return;
            bool tapped = Input.GetMouseButtonDown(0);
            if (!tapped) return;

            Camera cam = Camera.main;
            if (cam == null) return;
            Vector2 world = cam.ScreenToWorldPoint(Input.mousePosition);

            // 입질 중이면 어디를 눌러도 즉시 회수.
            // 안 눌러도 입질 시간이 끝나면 자동으로 잡히므로 이건 '의무'가 아니라 '효율'이다.
            if (_bobber.HitFish != null) { CatchFish(_bobber.HitFish); return; }
            if (Water.Contains(world)) _bobber.OnWaterTapped(world);
        }

        // ── 스폰 ─────────────────────────────────────────────────
        void TopUp(bool initial)
        {
            if (_spot == null) return;
            foreach (var entry in _spot.species)
            {
                var sp = entry.species;
                if (sp == null) continue;

                if (sp.pathType == PathType.School)
                {
                    if (CountOf(sp) == 0) SpawnSchool(sp);
                    continue;
                }
                if (sp.respawnDelay > 0f && !initial) continue;   // 지연 재스폰은 TickRespawn 이 담당

                if (initial && sp.spawnChance < 1f && Random.value > sp.spawnChance) continue;
                while (CountOf(sp) < TargetCount(sp)) Spawn(sp);
            }
        }

        void TickRespawn(float dt)
        {
            if (_spot == null) return;
            foreach (var entry in _spot.species)
            {
                var sp = entry.species;
                if (sp == null || sp.respawnDelay <= 0f) continue;
                if (CountOf(sp) >= TargetCount(sp)) { _respawnTimers[sp] = 0f; continue; }

                _respawnTimers.TryGetValue(sp, out float t);
                t += dt;
                if (t >= sp.respawnDelay) { Spawn(sp); t = 0f; }
                _respawnTimers[sp] = t;
            }
            // 지연 없는 어종은 즉시 리필
            TopUp(initial: false);
        }

        /// <summary>목표 개체 수는 세션당 한 번만 굴린다. 매 프레임 굴리면 스폰이 덜덜거린다.</summary>
        int TargetCount(FishSpeciesSO sp)
        {
            if (!_targetCount.TryGetValue(sp, out int n))
            {
                n = Random.Range(sp.aliveCount.x, sp.aliveCount.y + 1);
                _targetCount[sp] = n;
            }
            return n;
        }

        int CountOf(FishSpeciesSO sp)
        {
            int n = 0;
            foreach (var f in _fishes) if (f.Species == sp) n++;
            return n;
        }

        Fish Spawn(FishSpeciesSO sp)
        {
            Fish f = _pool.Count > 0 ? _pool.Pop() : Instantiate(_fishPrefab, _fishRoot);
            f.gameObject.SetActive(true);
            f.Init(this, sp, Water);
            var view = f.GetComponent<FishView>();
            if (view != null) view.Setup(sp, Water);
            _fishes.Add(f);
            return f;
        }

        void SpawnSchool(FishSpeciesSO sp)
        {
            int n = Random.Range(sp.schoolSize.x, sp.schoolSize.y + 1);
            Fish leader = Spawn(sp);
            leader.MakeLeader(leader.LaneParams, leader.PathTime);
            for (int i = 1; i < n; i++)
            {
                Fish m = Spawn(sp);
                m.ShareLane(leader.LaneParams, leader.PathTime);
                float a = Random.value * Mathf.PI * 2f;
                float r = Random.Range(0.25f, sp.schoolRadius);
                m.MakeFollower(new Vector2(Mathf.Cos(a) * r, Mathf.Sin(a) * r * 0.65f));
            }
        }

        void Despawn(Fish f)
        {
            f.gameObject.SetActive(false);
            _pool.Push(f);
        }

        public Fish FindLeader(FishSpeciesSO sp)
        {
            foreach (var f in _fishes) if (f.Species == sp && f.IsLeader) return f;
            return null;
        }

        // ── 회수 ─────────────────────────────────────────────────
        /// <summary>입질 시간이 끝났다. 자동 회수 또는 선별 놓아주기.</summary>
        public void OnBiteWindowExpired(Fish f)
        {
            _bobber.ClearHit();
            if (AutoReel && !ReleaseSpecies.Contains(f.Species.speciesId))
            {
                CatchFish(f);
            }
            else
            {
                // 놓아줌 — 조용히 떠난다. 찌는 그 자리에 유지되고 대물은 계속 온다.
                f.ReturnToRoam(2.2f);
                _bobber.Startle(f.Pos, FishingTuning.ReleaseStartleScale,
                                FishingTuning.ReleaseStartleScale, false);
            }
        }

        /// <summary>획득. 찌를 걷어올렸다 다시 던지므로 큰 소란이 인다 — §1-3</summary>
        public void CatchFish(Fish f)
        {
            if (f == null || !_fishes.Contains(f)) return;

            // 리더가 잡히면 남은 개체 하나를 승격 (무리가 통째로 사라지지 않게)
            if (f.IsLeader)
            {
                foreach (var o in _fishes)
                    if (o != f && o.Species == f.Species) { o.MakeLeader(f.LaneParams, f.PathTime); break; }
            }

            _fishes.Remove(f);
            _bobber.ClearHit();

            var sp = f.Species;
            float sizeCm = f.SizeCm;
            // 원본은 즉시 숨긴다. 안 그러면 비행 연출이 도는 0.85초 동안 물속에 그대로 남아 보인다.
            f.gameObject.SetActive(false);

            // 집계는 '바구니에 들어간 순간' 오른다. 비행이 곧 보상 연출이라 결과가 따라와야 한다. (§6-5)
            if (_catchFlight != null)
                _catchFlight.Launch(f, () => ApplyCatch(sp, sizeCm), () => _pool.Push(f));
            else { ApplyCatch(sp, sizeCm); _pool.Push(f); }

            _bobber.ReelAndRecast();
            PlayCatchSfx();
        }

        void ApplyCatch(FishSpeciesSO sp, float sizeCm)
        {
            _caught.TryGetValue(sp.speciesId, out int n);
            _caught[sp.speciesId] = n + 1;
            Score += sp.score;
            PlayerData.Instance.RecordCatch(sp, sizeCm);
            OnTallyChanged?.Invoke(sp, n + 1, Score);
        }

        public int CaughtCount(string speciesId) => _caught.TryGetValue(speciesId, out int n) ? n : 0;

        // ── 유틸 ─────────────────────────────────────────────────
        public Vector2 ClampToWater(Vector2 p, float margin) => new Vector2(
            Mathf.Clamp(p.x, Water.xMin + margin, Water.xMax - margin),
            Mathf.Clamp(p.y, Water.yMin + margin, Water.yMax - margin));

        public Vector2 RandomRockPoint(float fallbackY)
        {
            if (_rocks != null && _rocks.Length > 0)
            {
                Transform r = _rocks[Random.Range(0, _rocks.Length)];
                return (Vector2)r.position + Random.insideUnitCircle * 0.6f;
            }
            return new Vector2(Random.Range(Water.xMin + 1f, Water.xMax - 1f), fallbackY);
        }

        // 연출·사운드 훅 — 구현되면 여기에 연결한다
        public void SpawnRipple(Vector2 pos, float maxRadius) { }
        public void PlayCastSfx() { }
        public void PlayBiteSfx() { }
        public void PlayCatchSfx() { }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.cyan;
            Vector3 c = _waterCenter;
            Gizmos.DrawWireCube(c, new Vector3(_waterSize.x, _waterSize.y, 0f));
        }
    }
}
