using UnityEngine;

namespace Fishing.V2
{
    /// <summary>
    /// 세션 연출 상태. FishState와는 별개다 — 물고기 게임플레이 상태 머신은 이 값을 보지 않는다.
    /// 이 enum은 "지금 어떤 물 프로파일이 화면에 있는가"와 "인트로 연출이 아직 입력을 쥐고 있는가"만 정한다.
    /// </summary>
    public enum FishingV2SessionPresentationPhase
    {
        AboveWater,
        Diving,
        Gameplay
    }

    /// <summary>
    /// 물 표현 한 상태. "화면에 물이 얼마나 나와 있는가"를 정하는 값이 전부 여기 모여 있어서
    /// 상태 전환이 머티리얼 프로퍼티 열두 번이 아니라 블렌드 한 번이 된다.
    ///
    /// 승인된 gameplay 튜닝의 단일 원본은 여전히 FishingV2PresentationSettings다. Gameplay()가
    /// 그 값을 읽어오기만 하므로 이 구조체가 baseline의 두 번째 원본이 되지 않는다.
    /// </summary>
    public struct FishingV2WaterProfile
    {
        public string DisplayName;

        // 굴절. 하나의 surface height field에서 나오며, 개별 오브젝트가 아니라
        // Underwater RT 전체에 한 번 적용된다.
        public float RefractionStrength;
        public float RefractionCoefficient;
        public float RefractionScale;
        public float RefractionSpeed;

        // 보이는 수면.
        public float SurfaceRippleStrength;
        public float SurfaceShapeStrength;
        public float SurfaceHighlightStrength;
        public float SurfaceSpecularStrength;
        public float SurfaceReflectionStrength;
        public Color SurfaceReflectionColor;
        public Color SurfaceSpecularColor;

        // 바닥에 투과된 빛.
        public float LargeCausticStrength;
        public float MidCausticStrength;
        public float MicroSurfaceStrength;

        // 물 부피.
        public float WaterAbsorptionStrength;
        public float UnderwaterClarity;

        public float FishLightInfluence;
        public float PhysicalRippleStrength;

        // 카메라 연출. 탑다운 직교 카메라에서 "카메라 높이"는 orthographicSize다 —
        // 직교 투영은 z를 밀어도 화면이 변하지 않으므로 높이를 z로 흉내내지 않는다.
        // 1.0 = 현재 gameplay framing.
        public float CameraHeight;
        public Vector2 CameraFramingShift;

        /// <summary>
        /// 승인된 현재 상태. 값을 새로 정하지 않고 FishingV2PresentationSettings에서 읽어온다.
        /// 새로 추가된 항목(shape/specular/reflection/clarity)은 전부 중립값이라
        /// 이 프로파일이 걸린 화면은 이번 작업 이전과 같다.
        /// </summary>
        public static FishingV2WaterProfile Gameplay(FishingV2PresentationSettings settings)
        {
            return new FishingV2WaterProfile
            {
                DisplayName = "Gameplay",
                RefractionStrength = settings.WaterOpticalDistortionStrength,
                // WaterOpticsField.hlsl이 상수로 갖고 있던 UV 계수. 프로파일로 올려서
                // Presentation에서만 키운다.
                RefractionCoefficient = 0.0035f,
                RefractionScale = settings.WaterOpticalDistortionScale,
                RefractionSpeed = settings.WaterOpticalDistortionSpeed,
                // WaterSurfaceV2의 셰이더 기본값. 세션이 따로 덮어쓰지 않던 값이라 여기서 명시한다.
                SurfaceRippleStrength = 0.85f,
                SurfaceShapeStrength = 0f,
                SurfaceHighlightStrength = 1f,
                SurfaceSpecularStrength = 0f,
                SurfaceReflectionStrength = 0f,
                SurfaceReflectionColor = new Color(0.38f, 0.64f, 0.76f, 1f),
                SurfaceSpecularColor = new Color(0.72f, 0.88f, 0.94f, 1f),
                LargeCausticStrength = settings.WaterLargeCausticStrength,
                MidCausticStrength = settings.WaterMidCausticStrength,
                MicroSurfaceStrength = settings.WaterMicroSurfaceStrength,
                WaterAbsorptionStrength = 1f,
                UnderwaterClarity = 1f,
                FishLightInfluence = 1f,
                PhysicalRippleStrength = 1f,
                CameraHeight = 1f,
                CameraFramingShift = Vector2.zero
            };
        }

        /// <summary>
        /// 물 밖에서 수면을 내려다보는 상태.
        ///
        /// 레퍼런스가 말해주는 것은 "수면은 물결 능선으로 보이지 않는다"는 것이다. 맑은 물의
        /// 수면이 존재한다는 단서는 (1) 표면에 걸린 하늘빛 반사/광택, (2) 그 아래가 조금 뿌옇고
        /// 대비가 낮다는 것, (3) 바닥 코스틱이 아직 또렷하지 않다는 것이다. 그래서 이 프로파일의
        /// 주력은 reflection/highlight/clarity이고, 물결 형태는 아주 넓고 얕게만 남긴다.
        /// </summary>
        public static FishingV2WaterProfile PresentationAboveWater(FishingV2PresentationSettings settings)
        {
            FishingV2WaterProfile profile = Gameplay(settings);
            profile.DisplayName = "Presentation / AboveWater";
            profile.RefractionStrength = 1.00f;
            // Gameplay 0.0035의 약 2배. 수면 너머라 아래가 흔들리긴 하되, 굴절 자체가
            // 눈에 띄는 연출이 되지는 않는 정도.
            profile.RefractionCoefficient = 0.0075f;
            profile.RefractionScale = 0.95f;
            profile.RefractionSpeed = 0.30f;
            profile.SurfaceRippleStrength = 1.05f;
            // 능선은 거의 죽인다. 여기를 올리면 물이 아니라 천 주름이 된다.
            profile.SurfaceShapeStrength = 0.55f;
            // 대신 넓은 base field에 걸리는 광택을 올린다. 이것이 "수면 위에 빛이 얹혀 있다"다.
            profile.SurfaceHighlightStrength = 1.95f;
            profile.SurfaceSpecularStrength = 0.55f;
            // 물 밖 상태의 주된 단서. 하늘빛이 표면에 얹히고 그만큼 아래를 가린다.
            profile.SurfaceReflectionStrength = 0.95f;
            // 바닥 빛은 아직 덜 보인다. 잠수하면서 또렷해지는 것이 "들어간다"의 절반이다.
            profile.LargeCausticStrength = settings.WaterLargeCausticStrength * 0.70f;
            profile.MidCausticStrength = settings.WaterMidCausticStrength * 0.62f;
            profile.MicroSurfaceStrength = settings.WaterMicroSurfaceStrength * 1.85f;
            profile.WaterAbsorptionStrength = 1.50f;
            profile.UnderwaterClarity = 0.60f;
            profile.FishLightInfluence = 1.40f;
            profile.PhysicalRippleStrength = 1.25f;
            profile.CameraHeight = 1.20f;
            profile.CameraFramingShift = new Vector2(0f, 0.45f);
            return profile;
        }

        /// <summary>
        /// 수면을 지나는 중간 상태. 예전처럼 한 번 크게 튀는 peak가 아니라, 2초 넘게 이어지는
        /// 하강의 중간 지점이다. 반사가 빠지고 맑기가 돌아오는 구간이라 값도 그 사이에 있다.
        /// 굴절만 잠깐 위로 부풀어서 "경계면을 지난다"를 표시한다.
        /// </summary>
        public static FishingV2WaterProfile DiveTransition(FishingV2PresentationSettings settings)
        {
            FishingV2WaterProfile profile = PresentationAboveWater(settings);
            profile.DisplayName = "Dive";
            profile.RefractionCoefficient = 0.0125f;
            profile.RefractionScale = 0.88f;
            profile.RefractionSpeed = 0.42f;
            profile.SurfaceRippleStrength = 1.25f;
            profile.SurfaceShapeStrength = 0.78f;
            profile.SurfaceHighlightStrength = 1.55f;
            profile.SurfaceSpecularStrength = 0.72f;
            // 수면을 통과하는 중이므로 반사는 빠진다. 이것이 "경계면을 넘었다"의 주된 단서다.
            profile.SurfaceReflectionStrength = 0.26f;
            profile.LargeCausticStrength = settings.WaterLargeCausticStrength * 0.92f;
            profile.MidCausticStrength = settings.WaterMidCausticStrength * 0.88f;
            profile.MicroSurfaceStrength = settings.WaterMicroSurfaceStrength * 1.40f;
            profile.WaterAbsorptionStrength = 1.22f;
            profile.UnderwaterClarity = 0.80f;
            profile.FishLightInfluence = 1.75f;
            profile.PhysicalRippleStrength = 1.12f;
            profile.CameraHeight = 1.07f;
            profile.CameraFramingShift = new Vector2(0f, 0.16f);
            return profile;
        }

        /// <summary>
        /// 프로파일 전체를 한 번에 보간한다. 값을 snap하지 않는 것이 요점이라
        /// 상태 전환은 항상 이 함수를 지난다.
        /// </summary>
        public static FishingV2WaterProfile Blend(FishingV2WaterProfile from, FishingV2WaterProfile to, float t)
        {
            t = Mathf.Clamp01(t);
            return new FishingV2WaterProfile
            {
                DisplayName = t < 0.5f ? from.DisplayName : to.DisplayName,
                RefractionStrength = Mathf.Lerp(from.RefractionStrength, to.RefractionStrength, t),
                RefractionCoefficient = Mathf.Lerp(from.RefractionCoefficient, to.RefractionCoefficient, t),
                RefractionScale = Mathf.Lerp(from.RefractionScale, to.RefractionScale, t),
                RefractionSpeed = Mathf.Lerp(from.RefractionSpeed, to.RefractionSpeed, t),
                SurfaceRippleStrength = Mathf.Lerp(from.SurfaceRippleStrength, to.SurfaceRippleStrength, t),
                SurfaceShapeStrength = Mathf.Lerp(from.SurfaceShapeStrength, to.SurfaceShapeStrength, t),
                SurfaceHighlightStrength = Mathf.Lerp(from.SurfaceHighlightStrength, to.SurfaceHighlightStrength, t),
                SurfaceSpecularStrength = Mathf.Lerp(from.SurfaceSpecularStrength, to.SurfaceSpecularStrength, t),
                SurfaceReflectionStrength = Mathf.Lerp(from.SurfaceReflectionStrength, to.SurfaceReflectionStrength, t),
                SurfaceReflectionColor = Color.Lerp(from.SurfaceReflectionColor, to.SurfaceReflectionColor, t),
                SurfaceSpecularColor = Color.Lerp(from.SurfaceSpecularColor, to.SurfaceSpecularColor, t),
                LargeCausticStrength = Mathf.Lerp(from.LargeCausticStrength, to.LargeCausticStrength, t),
                MidCausticStrength = Mathf.Lerp(from.MidCausticStrength, to.MidCausticStrength, t),
                MicroSurfaceStrength = Mathf.Lerp(from.MicroSurfaceStrength, to.MicroSurfaceStrength, t),
                WaterAbsorptionStrength = Mathf.Lerp(from.WaterAbsorptionStrength, to.WaterAbsorptionStrength, t),
                UnderwaterClarity = Mathf.Lerp(from.UnderwaterClarity, to.UnderwaterClarity, t),
                FishLightInfluence = Mathf.Lerp(from.FishLightInfluence, to.FishLightInfluence, t),
                PhysicalRippleStrength = Mathf.Lerp(from.PhysicalRippleStrength, to.PhysicalRippleStrength, t),
                CameraHeight = Mathf.Lerp(from.CameraHeight, to.CameraHeight, t),
                CameraFramingShift = Vector2.Lerp(from.CameraFramingShift, to.CameraFramingShift, t)
            };
        }
    }
}
