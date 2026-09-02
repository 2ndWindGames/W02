#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Fishing.EditorTools
{
    /// <summary>
    /// 셋업 자동화. 손입력이 제일 지겨운 부분(어종 4종 x 20필드)을 메뉴 한 번으로 끝낸다.
    /// 수치는 기획서 §7 그대로. 값을 바꾸고 싶으면 여기가 아니라 생성된 애셋에서 바꾼다.
    ///
    /// 메뉴를 1 → 2 → 3 순서로 누르면 씬이 돌아가는 상태까지 간다.
    /// </summary>
    public static class FishingSetupWizard
    {
        const string Root       = "Assets/Fishing";
        const string DataDir    = Root + "/Data";
        const string PrefabDir  = Root + "/Prefabs";
        const string ArtDir     = Root + "/Art/Placeholder";
        const string ShaderPath = Root + "/Shaders/FishWave.shader";
        const string MatPath    = Root + "/Art/FishWave.mat";

        // ─────────────────────────────────────────────────────────
        [MenuItem("Fishing/1. 어종 · 낚시터 애셋 생성", priority = 0)]
        public static void CreateAssets()
        {
            EnsureDir(DataDir);

            var perch = Species("perch", "초록 잡어", 1, PathType.Lane, sp =>
            {
                sp.cruiseSpeed = 1.2f; sp.laneAmp = 0.6f; sp.lanePeriod = 4f;
                sp.band = new Vector2(0.05f, 0.95f);
                sp.aliveCount = new Vector2Int(4, 6);
                sp.startleRadius = 1.5f; sp.startleTime = 0.4f;
                sp.curiosity = 0.050f; sp.lureChance = 0.45f;
                sp.approachSpeed = 2.5f; sp.biteWindow = 1.2f;
                sp.turnRateDeg = 200f;
                sp.sizeCm = new Vector2(12f, 20f);
                sp.bodyLength = 0.92f; sp.tailLag = 0.05f;
                sp.waveAmp = 1.0f; sp.waveFreq = 1.0f;
            });

            var sardine = Species("sardine", "파란 소어", 2, PathType.School, sp =>
            {
                sp.cruiseSpeed = 1.6f; sp.laneAmp = 0.45f; sp.lanePeriod = 3.4f;
                sp.band = new Vector2(0.05f, 0.45f);
                sp.aliveCount = new Vector2Int(1, 1);
                sp.schoolSize = new Vector2Int(5, 8); sp.schoolRadius = 1.2f;
                sp.startleRadius = 2.0f; sp.startleTime = 0.6f;
                sp.curiosity = 0.039f; sp.lureChance = 0.38f;
                sp.approachSpeed = 3.0f; sp.biteWindow = 1.0f;
                sp.turnRateDeg = 280f;
                sp.sizeCm = new Vector2(6f, 10f);
                sp.bodyLength = 0.55f; sp.tailLag = 0.03f;
                sp.waveAmp = 0.6f; sp.waveFreq = 1.8f;
            });

            var puffer = Species("puffer", "복어", 5, PathType.HoverDash, sp =>
            {
                sp.hoverTime = new Vector2(2f, 4f); sp.dashTime = 0.4f;
                sp.dashDistance = new Vector2(1.5f, 2.5f); sp.homeRadius = 3f;
                sp.band = new Vector2(0.2f, 0.8f);
                sp.aliveCount = new Vector2Int(1, 2);
                sp.respawnDelay = 15f;                 // 바위 옆 무한 파밍 방지
                sp.startleRadius = 1.2f; sp.startleTime = 0.8f;
                sp.curiosity = 0.063f; sp.lureChance = 0.30f;
                sp.approachSpeed = 1.5f; sp.biteWindow = 1.5f;
                sp.turnRateDeg = 95f;
                sp.sizeCm = new Vector2(15f, 25f);
                sp.bodyLength = 1.0f; sp.tailLag = 0.05f;
                sp.waveAmp = 0.1f;                     // 몸통이 뻣뻣해야 한다
                sp.waveFreq = 0.6f;
            });

            var trophy = Species("trophy", "분홍 대물", 30, PathType.Loop, sp =>
            {
                sp.loopA = 4.0f; sp.loopB = 2.5f; sp.loopPeriod = 10f;
                sp.band = new Vector2(0.5f, 0.95f);
                sp.aliveCount = new Vector2Int(1, 1);
                sp.spawnChance = 1f; sp.respawnDelay = 25f;
                sp.startleRadius = 3.0f; sp.startleTime = 1.2f;
                sp.curiosity = 0.063f;
                sp.minNoticeRadius = 3.2f;             // 멀리서 알아채고 들어온다
                sp.lureChance = 0.0f;                  // 물장구에 오지 않는다 (§5-0)
                sp.approachSpeed = 0.5f;               // 접근 6~11초 > 낚는 주기 7초
                sp.biteWindow = 0.6f;
                sp.turnRateDeg = 85f;                  // 180도 도는 데 2.1초
                sp.sizeCm = new Vector2(35f, 60f);
                sp.bodyLength = 1.70f; sp.tailLag = 0.15f;
                sp.waveAmp = 1.4f; sp.waveFreq = 0.7f;
            });

            AssignPlaceholderSprites(perch, sardine, puffer, trophy);

            var spot = AssetDatabase.LoadAssetAtPath<FishingSpotSO>(DataDir + "/Spot_Beach.asset");
            if (spot == null)
            {
                spot = ScriptableObject.CreateInstance<FishingSpotSO>();
                AssetDatabase.CreateAsset(spot, DataDir + "/Spot_Beach.asset");
            }
            spot.displayName = "해변";
            spot.sessionSeconds = 90f;
            spot.species = new[]
            {
                new FishingSpotSO.Entry { species = perch,   weight = 1f },
                new FishingSpotSO.Entry { species = sardine, weight = 1f },
                new FishingSpotSO.Entry { species = puffer,  weight = 1f },
                new FishingSpotSO.Entry { species = trophy,  weight = 1f },
            };
            EditorUtility.SetDirty(spot);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[Fishing] 어종 4종 + 낚시터 1개 생성 완료 → " + DataDir);
        }

        static FishSpeciesSO Species(string id, string name, int score, PathType path,
                                     System.Action<FishSpeciesSO> fill)
        {
            string path2 = $"{DataDir}/Fish_{id}.asset";
            var sp = AssetDatabase.LoadAssetAtPath<FishSpeciesSO>(path2);
            if (sp == null)
            {
                sp = ScriptableObject.CreateInstance<FishSpeciesSO>();
                AssetDatabase.CreateAsset(sp, path2);
            }
            sp.speciesId = id; sp.displayName = name; sp.score = score; sp.pathType = path;
            // 기본값으로 되돌린 뒤 채운다 (재실행 시 이전 값이 남지 않게)
            sp.minNoticeRadius = 0f; sp.respawnDelay = 0f; sp.spawnChance = 1f;
            fill(sp);
            EditorUtility.SetDirty(sp);
            return sp;
        }

        static void AssignPlaceholderSprites(params FishSpeciesSO[] all)
        {
            foreach (var sp in all)
            {
                var s = AssetDatabase.LoadAssetAtPath<Sprite>($"{ArtDir}/fish_{sp.speciesId}.png");
                if (s != null) sp.bodySprite = s;
                EditorUtility.SetDirty(sp);
            }
        }

        // ─────────────────────────────────────────────────────────
        [MenuItem("Fishing/2. 프리팹 생성 (Fish · Bobber)", priority = 1)]
        public static void BuildPrefabs()
        {
            EnsureDir(PrefabDir);
            EnsureMaterial();

            // ── Fish ─────────────────────────────────────────────
            var root = new GameObject("Fish");
            root.AddComponent<Fish>();
            var view = root.AddComponent<FishView>();

            var body = new GameObject("Body");
            body.transform.SetParent(root.transform, false);
            var bodySr = body.AddComponent<SpriteRenderer>();
            bodySr.sortingOrder = 10;
            bodySr.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(MatPath);

            var tailPivot = new GameObject("TailPivot");
            tailPivot.transform.SetParent(root.transform, false);
            var tail = new GameObject("Tail");
            tail.transform.SetParent(tailPivot.transform, false);
            var tailSr = tail.AddComponent<SpriteRenderer>();
            tailSr.sortingOrder = 9;
            tailPivot.SetActive(false);          // 대물만 켠다

            // 그림자는 자식이다 — 회전·스케일을 그대로 상속받아야 실루엣이 맞는다.
            // 위치만 FishView 가 월드 좌표로 따로 잡는다(수면에 남아야 하므로).
            var shadow = new GameObject("Shadow");
            shadow.transform.SetParent(root.transform, false);
            var shadowSr = shadow.AddComponent<SpriteRenderer>();
            shadowSr.sortingOrder = 5;
            shadowSr.color = new Color(0.04f, 0.22f, 0.27f, 0.28f);

            var so = new SerializedObject(view);
            so.FindProperty("_body").objectReferenceValue = bodySr;
            so.FindProperty("_tailPivot").objectReferenceValue = tailPivot.transform;
            so.FindProperty("_tail").objectReferenceValue = tailSr;
            so.FindProperty("_shadow").objectReferenceValue = shadowSr;
            so.ApplyModifiedPropertiesWithoutUndo();

            string fishPath = PrefabDir + "/Fish.prefab";
            PrefabUtility.SaveAsPrefabAsset(root, fishPath);
            Object.DestroyImmediate(root);

            // ── Bobber ───────────────────────────────────────────
            var bob = new GameObject("Bobber");
            var bobber = bob.AddComponent<Bobber>();
            var vis = new GameObject("Visual");
            vis.transform.SetParent(bob.transform, false);
            var visSr = vis.AddComponent<SpriteRenderer>();
            visSr.sortingOrder = 20;
            visSr.sprite = AssetDatabase.LoadAssetAtPath<Sprite>($"{ArtDir}/bobber.png");

            var bso = new SerializedObject(bobber);
            bso.FindProperty("_visual").objectReferenceValue = vis.transform;
            bso.ApplyModifiedPropertiesWithoutUndo();

            string bobPath = PrefabDir + "/Bobber.prefab";
            PrefabUtility.SaveAsPrefabAsset(bob, bobPath);
            Object.DestroyImmediate(bob);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[Fishing] 프리팹 생성 완료 → " + PrefabDir);
        }

        static void EnsureMaterial()
        {
            EnsureDir(Root + "/Art");
            var mat = AssetDatabase.LoadAssetAtPath<Material>(MatPath);
            if (mat != null) return;
            var sh = AssetDatabase.LoadAssetAtPath<Shader>(ShaderPath);
            if (sh == null) { Debug.LogWarning("[Fishing] FishWave.shader 를 못 찾았습니다."); return; }
            mat = new Material(sh);
            mat.SetFloat("_Amp", 0.12f);
            mat.SetFloat("_Freq", 5.2f);
            AssetDatabase.CreateAsset(mat, MatPath);
        }

        // ─────────────────────────────────────────────────────────
        [MenuItem("Fishing/3. 현재 씬에 세션 구성", priority = 2)]
        public static void BuildScene()
        {
            var spot = AssetDatabase.LoadAssetAtPath<FishingSpotSO>(DataDir + "/Spot_Beach.asset");
            var fishPrefab = AssetDatabase.LoadAssetAtPath<Fish>(PrefabDir + "/Fish.prefab");
            var bobberPrefab = AssetDatabase.LoadAssetAtPath<Bobber>(PrefabDir + "/Bobber.prefab");
            if (spot == null || fishPrefab == null || bobberPrefab == null)
            {
                EditorUtility.DisplayDialog("Fishing", "1번과 2번 메뉴를 먼저 실행하세요.", "확인");
                return;
            }

            var sessionGo = new GameObject("FishingSession");
            var session = sessionGo.AddComponent<FishingSession>();

            var fishRoot = new GameObject("FishRoot");
            fishRoot.transform.SetParent(sessionGo.transform, false);

            var bobber = (Bobber)PrefabUtility.InstantiatePrefab(bobberPrefab);
            bobber.transform.SetParent(sessionGo.transform, false);

            var flightGo = new GameObject("CatchFlight");
            flightGo.transform.SetParent(sessionGo.transform, false);
            var flight = flightGo.AddComponent<CatchFlight>();

            // 복어 앵커 — 수면 안 임의 지점 3곳
            var rocks = new Transform[3];
            for (int i = 0; i < 3; i++)
            {
                var r = new GameObject($"Rock{i}");
                r.transform.SetParent(sessionGo.transform, false);
                r.transform.position = new Vector3(-5f + i * 5f, -2f + i * 2f, 0f);
                rocks[i] = r.transform;
            }

            // 바구니 슬롯 — 수면 오른쪽 바깥
            var slots = new Transform[4];
            for (int i = 0; i < 4; i++)
            {
                var s = new GameObject($"BasketSlot{i}");
                s.transform.SetParent(flightGo.transform, false);
                s.transform.position = new Vector3(9.5f, 3.2f - i * 2.0f, 0f);
                slots[i] = s.transform;
            }

            var sso = new SerializedObject(session);
            sso.FindProperty("_spot").objectReferenceValue = spot;
            sso.FindProperty("_bobber").objectReferenceValue = bobber;
            sso.FindProperty("_fishPrefab").objectReferenceValue = fishPrefab;
            sso.FindProperty("_fishRoot").objectReferenceValue = fishRoot.transform;
            sso.FindProperty("_catchFlight").objectReferenceValue = flight;
            var rocksProp = sso.FindProperty("_rocks");
            rocksProp.arraySize = rocks.Length;
            for (int i = 0; i < rocks.Length; i++)
                rocksProp.GetArrayElementAtIndex(i).objectReferenceValue = rocks[i];
            sso.ApplyModifiedPropertiesWithoutUndo();

            var fso = new SerializedObject(flight);
            var flyer = new GameObject("FlyerTemplate");
            var flyerSr = flyer.AddComponent<SpriteRenderer>();
            flyerSr.sortingOrder = 30;
            var flyerPath = PrefabDir + "/CatchFlyer.prefab";
            PrefabUtility.SaveAsPrefabAsset(flyer, flyerPath);
            Object.DestroyImmediate(flyer);

            var shadowTpl = new GameObject("FlyerShadowTemplate");
            var shSr = shadowTpl.AddComponent<SpriteRenderer>();
            shSr.sortingOrder = 29;
            shSr.color = new Color(0.04f, 0.22f, 0.27f, 0.28f);
            shSr.sprite = AssetDatabase.LoadAssetAtPath<Sprite>($"{ArtDir}/shadow.png");
            var shPath = PrefabDir + "/CatchFlyerShadow.prefab";
            PrefabUtility.SaveAsPrefabAsset(shadowTpl, shPath);
            Object.DestroyImmediate(shadowTpl);

            fso.FindProperty("_flyerPrefab").objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<SpriteRenderer>(flyerPath);
            fso.FindProperty("_shadowPrefab").objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<SpriteRenderer>(shPath);
            var slotProp = fso.FindProperty("_basketSlots");
            slotProp.arraySize = slots.Length;
            for (int i = 0; i < slots.Length; i++)
                slotProp.GetArrayElementAtIndex(i).objectReferenceValue = slots[i];
            fso.ApplyModifiedPropertiesWithoutUndo();

            // 카메라 — 수면 9유닛이 화면에 꽉 차게
            var cam = Camera.main;
            if (cam != null)
            {
                cam.orthographic = true;
                cam.orthographicSize = 4.5f;
                cam.transform.position = new Vector3(0f, 0f, -10f);
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = new Color(0.29f, 0.72f, 0.79f);
            }

            Selection.activeGameObject = sessionGo;
            Debug.Log("[Fishing] 씬 구성 완료. 재생 후 수면을 클릭해 찌를 던지세요.");
        }

        // ─────────────────────────────────────────────────────────
        [MenuItem("Fishing/스프라이트 임포트 설정 적용", priority = 20)]
        public static void FixSpriteImport()
        {
            if (!Directory.Exists(ArtDir)) { Debug.LogWarning("[Fishing] Art 폴더 없음: " + ArtDir); return; }
            foreach (string file in Directory.GetFiles(ArtDir, "*.png"))
            {
                var ti = AssetImporter.GetAtPath(file.Replace("\\", "/")) as TextureImporter;
                if (ti == null) continue;
                ti.textureType = TextureImporterType.Sprite;
                ti.spriteImportMode = SpriteImportMode.Single;
                ti.mipmapEnabled = false;
                ti.filterMode = FilterMode.Bilinear;
                ti.spritePixelsPerUnit = 256f;
                // ⚠️ 이 스프라이트들은 Sprite Atlas 에 넣지 말 것.
                //    UV 왜곡이 [0,1] 밖으로 나가면 아틀라스의 옆 스프라이트를 샘플한다.
                ti.SaveAndReimport();
            }
            Debug.Log("[Fishing] 스프라이트 임포트 설정 적용 완료 (PPU 256, 아틀라스 미사용)");
        }

        static void EnsureDir(string dir)
        {
            if (Directory.Exists(dir)) return;
            Directory.CreateDirectory(dir);
            AssetDatabase.Refresh();
        }
    }
}
#endif
