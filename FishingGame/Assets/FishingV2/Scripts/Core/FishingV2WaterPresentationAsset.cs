using System;
using UnityEngine;

namespace Fishing.V2
{
    /// <summary>
    /// 물 프로파일 하나를 인스펙터에서 편집 가능한 형태로 담는다.
    ///
    /// FishingV2WaterProfile은 런타임 구조체(블렌드 대상)라 [Range]도 툴팁도 붙지 않는다.
    /// 이쪽이 편집용 표면이고, ToProfile()이 런타임 값으로 넘겨준다.
    /// </summary>
    [Serializable]
    public sealed class FishingV2WaterProfileSettings
    {
        public string DisplayName = "Profile";

        [Header("굴절 — 수면 너머가 얼마나 일렁여 보이는가")]
        [Tooltip("굴절을 켜고 끄는 스위치에 가깝습니다. 세기 조절은 아래 계수로 하세요.")]
        [Range(0f, 1f)] public float RefractionStrength = 0.68f;
        [Tooltip("실제로 만지게 될 값입니다. 0.0035가 게임플레이 기본값이고, 0.01을 넘기면 " +
                 "수중 이미지가 눈에 띄게 흔들려요. 0.03쯤 가면 젤리처럼 보이니 그 전에서 멈추는 게 좋습니다.")]
        [Range(0f, 0.04f)] public float RefractionCoefficient = 0.0035f;
        [Tooltip("수면 무늬의 크기입니다. 낮추면 무늬가 커지고 올리면 잘게 부서져요. " +
                 "세 프로파일에서 같은 값으로 두는 걸 권합니다. 전환 도중에 이 값이 바뀌면 " +
                 "무늬가 통째로 확대·축소되면서 화면이 늘어났다 줄어드는 것처럼 보입니다.")]
        [Range(0.25f, 2.5f)] public float RefractionScale = 1.08f;
        [Tooltip("수면이 흐르는 속도예요. 위상을 누적해서 쓰기 때문에 이 값을 도중에 바꿔도 " +
                 "무늬가 튀지 않고 빨라지거나 느려지기만 합니다.")]
        [Range(0f, 1f)] public float RefractionSpeed = 0.18f;

        [Header("보이는 수면")]
        [Tooltip("물결 능선에 얹히는 빛의 양입니다. 아래 '물결 세기'가 0이면 거의 티가 안 나요.")]
        [Range(0f, 3f)] public float SurfaceRippleStrength = 0.85f;
        [Tooltip("바람이 만드는 물결의 세기입니다. 0이면 수면이 매끈하고, 올릴수록 능선이 또렷해져요. " +
                 "0.8을 넘기면 물이 아니라 천 주름처럼 보이기 시작합니다. 찌가 떨어져서 생기는 " +
                 "파문은 여기와 무관해요 — 그건 그때그때 일어나는 사건입니다.")]
        [Range(0f, 2f)] public float SurfaceShapeStrength = 0f;
        [Tooltip("넓게 퍼진 수면광의 배수입니다. 능선이 아니라 화면 전체에 걸리는 부드러운 빛이라 " +
                 "너무 올리면 물결이 아니라 구름처럼 뭉쳐 보여요.")]
        [Range(0f, 4f)] public float SurfaceHighlightStrength = 1f;
        [Tooltip("능선 위에 맺히는 반짝임입니다. 물 밖에서만 쓰고 게임플레이에서는 0으로 둡니다.")]
        [Range(0f, 2f)] public float SurfaceSpecularStrength = 0f;
        [Tooltip("물 밖에 있다는 걸 알려주는 가장 큰 단서입니다. 하늘빛이 수면에 얹히면서 " +
                 "그만큼 아래를 가려요. 물속으로 들어가면 0이 되어야 합니다.")]
        [Range(0f, 2f)] public float SurfaceReflectionStrength = 0f;
        [Tooltip("수면에 비치는 하늘색. 너무 밝으면 물이 뿌옇게 들뜹니다.")]
        public Color SurfaceReflectionColor = new Color(0.38f, 0.64f, 0.76f, 1f);
        [Tooltip("반짝임의 색. 흰색에 가까울수록 금속처럼 보이니 살짝 푸른 쪽이 안전합니다.")]
        public Color SurfaceSpecularColor = new Color(0.72f, 0.88f, 0.94f, 1f);

        [Header("바닥에 드리우는 빛")]
        [Tooltip("바닥을 넓게 훑는 빛입니다. 바닥 색을 직접 곱하는 항이라 조금만 올려도 " +
                 "물 색과 물고기까지 같이 밝아지니 주의하세요.")]
        [Range(0f, 3f)] public float LargeCausticStrength = 0.68f;
        [Tooltip("좀 더 잘게 갈라진 바닥 빛입니다.")]
        [Range(0f, 3f)] public float MidCausticStrength = 0.36f;
        [Tooltip("수면의 아주 잔잔한 떨림입니다. 없으면 물이 정지한 그림처럼 보여요.")]
        [Range(0f, 3f)] public float MicroSurfaceStrength = 0.14f;

        [Header("물의 두께")]
        [Tooltip("가장자리와 깊은 곳이 얼마나 탁해지는가. 1이 기본이고 올리면 물이 무거워집니다.")]
        [Range(0f, 3f)] public float WaterAbsorptionStrength = 1f;
        [Tooltip("1이면 또렷하고, 낮출수록 수중이 뿌예집니다. 물 밖에서 수면 너머를 볼 때 " +
                 "0.6~0.7 정도가 자연스러워요. 0.4 아래로 내리면 물이 아니라 안개가 됩니다.")]
        [Range(0f, 1f)] public float UnderwaterClarity = 1f;

        [Header("그 밖에")]
        [Tooltip("수면광이 물고기 몸에 얼마나 비치는가. 값만 바꾸고 물고기 위치는 건드리지 않습니다.")]
        [Range(0f, 4f)] public float FishLightInfluence = 1f;
        [Tooltip("찌 주위에 퍼지는 파문 링의 세기입니다. 금색 상호작용 링은 게임 표시라 " +
                 "여기 영향을 받지 않아요.")]
        [Range(0f, 3f)] public float PhysicalRippleStrength = 1f;

        [Header("카메라 — 위에서 내려다보는 시점이라 '높이'는 곧 화면 배율입니다")]
        [Tooltip("1.0이 게임플레이 화면입니다. 올리면 카메라가 물러나 물고기가 작아지고, " +
                 "잠수하면서 이 값이 1로 줄어드는 것이 '다가간다'는 느낌을 만듭니다. " +
                 "주의: 물 표면 판의 크기는 세션이 시작될 때 정해집니다. 재생 중에 이 값을 " +
                 "크게 올리면 화면 가장자리가 비어 보일 수 있어요. R로 세션을 다시 열면 맞춰집니다.")]
        [Range(0.5f, 2.5f)] public float CameraHeight = 1f;
        [Tooltip("화면을 살짝 밀어 프레이밍만 바꿉니다. 크게 주면 연못 밖이 보이니 조금씩.")]
        public Vector2 CameraFramingShift = Vector2.zero;

        public FishingV2WaterProfile ToProfile()
        {
            return new FishingV2WaterProfile
            {
                DisplayName = string.IsNullOrEmpty(DisplayName) ? "Profile" : DisplayName,
                RefractionStrength = RefractionStrength,
                RefractionCoefficient = RefractionCoefficient,
                RefractionScale = RefractionScale,
                RefractionSpeed = RefractionSpeed,
                SurfaceRippleStrength = SurfaceRippleStrength,
                SurfaceShapeStrength = SurfaceShapeStrength,
                SurfaceHighlightStrength = SurfaceHighlightStrength,
                SurfaceSpecularStrength = SurfaceSpecularStrength,
                SurfaceReflectionStrength = SurfaceReflectionStrength,
                SurfaceReflectionColor = SurfaceReflectionColor,
                SurfaceSpecularColor = SurfaceSpecularColor,
                LargeCausticStrength = LargeCausticStrength,
                MidCausticStrength = MidCausticStrength,
                MicroSurfaceStrength = MicroSurfaceStrength,
                WaterAbsorptionStrength = WaterAbsorptionStrength,
                UnderwaterClarity = UnderwaterClarity,
                FishLightInfluence = FishLightInfluence,
                PhysicalRippleStrength = PhysicalRippleStrength,
                CameraHeight = CameraHeight,
                CameraFramingShift = CameraFramingShift
            };
        }

        public void CopyFrom(FishingV2WaterProfile profile)
        {
            DisplayName = profile.DisplayName;
            RefractionStrength = profile.RefractionStrength;
            RefractionCoefficient = profile.RefractionCoefficient;
            RefractionScale = profile.RefractionScale;
            RefractionSpeed = profile.RefractionSpeed;
            SurfaceRippleStrength = profile.SurfaceRippleStrength;
            SurfaceShapeStrength = profile.SurfaceShapeStrength;
            SurfaceHighlightStrength = profile.SurfaceHighlightStrength;
            SurfaceSpecularStrength = profile.SurfaceSpecularStrength;
            SurfaceReflectionStrength = profile.SurfaceReflectionStrength;
            SurfaceReflectionColor = profile.SurfaceReflectionColor;
            SurfaceSpecularColor = profile.SurfaceSpecularColor;
            LargeCausticStrength = profile.LargeCausticStrength;
            MidCausticStrength = profile.MidCausticStrength;
            MicroSurfaceStrength = profile.MicroSurfaceStrength;
            WaterAbsorptionStrength = profile.WaterAbsorptionStrength;
            UnderwaterClarity = profile.UnderwaterClarity;
            FishLightInfluence = profile.FishLightInfluence;
            PhysicalRippleStrength = profile.PhysicalRippleStrength;
            CameraHeight = profile.CameraHeight;
            CameraFramingShift = profile.CameraFramingShift;
        }
    }

    /// <summary>
    /// 착수 한 번에 딸린 사건들의 시간표. 전부 초 단위이고, 서로 겹쳐서 재생된다.
    /// </summary>
    [Serializable]
    public sealed class FishingV2SplashTiming
    {
        [Header("찌가 떨어지는 순간")]
        [Tooltip("파문이 퍼져 사라지기까지 걸리는 시간입니다. 길게 잡을수록 멀리까지 퍼져요.")]
        [Range(0.2f, 4f)] public float PrimaryDuration = 1.90f;
        [Tooltip("게임플레이 중에 다시 던졌을 때의 파문 수명. 90초에 열 번 넘게 보게 되니 " +
                 "인트로보다 짧게 두는 편이 낫습니다.")]
        [Range(0.2f, 4f)] public float GameplayDuration = 1.55f;

        [Header("2차 파문 — 튀어오른 물이 다시 떨어지면서 생깁니다")]
        [Tooltip("몇 방울이 떨어지는가. 인트로에서만 나옵니다.")]
        [Range(0, 5)] public int SecondaryCount = 5;
        [Tooltip("작은 물방울이라 본 파문보다 금방 사그라듭니다.")]
        [Range(0.1f, 2f)] public float SecondaryDuration = 1.05f;
        [Tooltip("본 착수 뒤 몇 초에 떨어지기 시작하는가. 최소와 최대의 간격이 넓어야 " +
                 "우수수 떨어지는 느낌이 나고, 좁으면 한꺼번에 툭 떨어져 어색합니다.")]
        [Range(0f, 1.5f)] public float SecondaryDelayMin = 0.42f;
        [Range(0f, 2f)] public float SecondaryDelayMax = 0.85f;
        [Tooltip("찌에서 얼마나 멀리 흩어지는가. 화면 크기 기준이라 0.1이면 화면 폭의 10%쯤입니다.")]
        [Range(0.02f, 0.5f)] public float SecondaryDistanceMin = 0.105f;
        [Range(0.02f, 0.6f)] public float SecondaryDistanceMax = 0.260f;
        [Tooltip("2차 파문의 세기. 본 파문보다 확실히 약해야 무엇이 원인인지 읽힙니다.")]
        [Range(0f, 1f)] public float SecondaryStrengthMin = 0.42f;
        [Range(0f, 1f)] public float SecondaryStrengthMax = 0.64f;
        [Tooltip("파문의 크기 배율입니다. 작은 물방울이 떨어진 것이니 본 착수보다 작아야 해요. " +
                 "1에 가깝게 두면 어느 것이 본 착수인지 구분이 안 됩니다.")]
        [Range(0.1f, 1f)] public float SecondaryRingScaleMin = 0.38f;
        [Range(0.1f, 1f)] public float SecondaryRingScaleMax = 0.52f;

        [Header("렌즈에 튀는 물방울 — 물 밖에서 던졌을 때만 생깁니다")]
        [Tooltip("물방울이 맺혔다가 흘러내려 사라지기까지. 물방울마다 수명이 달라서 " +
                 "여기는 가장 오래 남는 것까지 담을 수 있을 만큼 넉넉해야 합니다. " +
                 "짧게 잡으면 늦게 생긴 물방울이 도중에 잘려 다 같이 사라지는 것처럼 보여요.")]
        [Range(0.3f, 4f)] public float LensDropletDuration = 2.10f;

        [Header("기포")]
        [Tooltip("물 밖에서 던졌을 때, 찌가 끌고 들어간 공기가 떠올라 수면에서 터지기까지의 시간입니다.")]
        [Range(0.3f, 4f)] public float CastBubbleDuration = 1.85f;
        [Tooltip("물속에서 다시 던졌을 때의 기포 수명입니다. 이때는 수면이 카메라 뒤에 있어서 " +
                 "기포가 터지지 않고 렌즈를 지나쳐 화면 밖으로 나가요. 그래서 물 밖과 따로 둡니다.")]
        [Range(0.3f, 4f)] public float GameplayBubbleDuration = 2.20f;
        [Tooltip("물속 기포가 카메라 쪽으로 다가오는 속도입니다. 이 값을 낮추면 느긋하게 떠오르고, " +
                 "2를 넘기면 휙 스쳐 지나가듯 보여요. 빠르다고 느껴지면 여기를 먼저 내려보세요.")]
        [Range(0.2f, 4f)] public float GameplayBubbleRushSpeed = 1.45f;
        [Tooltip("카메라가 수면을 뚫는 순간 기포가 화면 밖으로 퍼지는 시간입니다.")]
        [Range(0.2f, 3f)] public float CrossBubbleDuration = 0.85f;
        [Tooltip("그 기포들이 바깥으로 밀려나는 속도. 앞으로 나아갈 때 옆을 스쳐 가는 느낌이라 " +
                 "빠른 편이 자연스럽지만, 너무 높이면 한 프레임 만에 사라집니다.")]
        [Range(0.5f, 5f)] public float CrossBubbleRushSpeed = 2.85f;

        [Header("착수 충격")]
        [Tooltip("카메라가 한 번 내려앉았다 돌아오는 데 걸리는 시간입니다. 흔드는 게 아니라 단발이에요.")]
        [Range(0.05f, 1.5f)] public float CameraImpulseDuration = 0.42f;
        [Tooltip("게임플레이 중 캐스팅의 충격 배율입니다. 이미 물속에 있는 카메라가 " +
                 "수면 충격에 크게 흔들리면 어색해서 기본값을 낮게 뒀어요.")]
        [Range(0f, 1f)] public float GameplayImpulseScale = 0.33f;
        [Tooltip("게임플레이 중 캐스팅에서 나오는 기포의 양입니다. 물속에서는 렌즈 물방울이 " +
                 "없으니, 던졌다는 걸 알려주는 건 파문과 이 기포뿐입니다.")]
        [Range(0f, 2f)] public float GameplayBubbleStrength = 0.62f;
    }

    /// <summary>
    /// 물 연출 값 전부를 담는 애셋.
    ///
    /// 왜 컴포넌트가 아니라 애셋인가 — C# 상수와 컴포넌트 필드는 둘 다 튜닝을 막는다.
    /// 상수는 재컴파일이 필요하고, 컴포넌트 필드는 씬에 직렬화되어 코드 기본값을 바꿔도
    /// 반영되지 않는다(이 프로젝트에서 실제로 두 번 헛돌았다). 애셋이면 플레이 모드 중에
    /// 고친 값이 종료 후에도 남으므로, 재생을 끊지 않고 연출을 만질 수 있다.
    ///
    /// 세션에 이 애셋이 없으면 코드 기본값으로 동작하므로 비워 둬도 깨지지 않는다.
    /// </summary>
    [CreateAssetMenu(
        fileName = "FishingV2WaterPresentation",
        menuName = "Fishing V2/Water Presentation Asset",
        order = 20)]
    public sealed class FishingV2WaterPresentationAsset : ScriptableObject
    {
        [Tooltip("클릭부터 게임이 시작되기까지의 전체 시간표입니다. 찌 비행 → 접근 → 수면 통과 → 안착 순서로 이어집니다.")]
        public FishingV2DivePresentationTiming Timing = new FishingV2DivePresentationTiming();

        [Tooltip("찌가 물에 닿는 순간 함께 일어나는 것들 — 파문, 물방울, 기포, 카메라 충격의 시간표입니다.")]
        public FishingV2SplashTiming Splash = new FishingV2SplashTiming();

        [Header("⚠ 게임플레이 기준값 — 여기를 만지면 실제 플레이 화면이 바뀝니다")]
        public FishingV2WaterProfileSettings Gameplay = new FishingV2WaterProfileSettings();

        [Header("물 밖에서 수면을 내려다볼 때")]
        public FishingV2WaterProfileSettings AboveWater = new FishingV2WaterProfileSettings();

        [Header("수면 바로 위 — 카메라가 물에 닿기 직전")]
        public FishingV2WaterProfileSettings NearSurface = new FishingV2WaterProfileSettings();

        /// <summary>
        /// 코드에 박혀 있는 기본값으로 세 프로파일을 채운다. 애셋을 처음 만들 때와
        /// "값을 망쳤으니 되돌리자" 할 때 쓴다.
        /// </summary>
        public void PopulateFromCodeDefaults(FishingV2PresentationSettings settings)
        {
            Gameplay.CopyFrom(FishingV2WaterProfile.Gameplay(settings));
            AboveWater.CopyFrom(FishingV2WaterProfile.PresentationAboveWater(settings));
            NearSurface.CopyFrom(FishingV2WaterProfile.DiveTransition(settings));
            Timing = new FishingV2DivePresentationTiming();
            Splash = new FishingV2SplashTiming();
        }
    }
}
