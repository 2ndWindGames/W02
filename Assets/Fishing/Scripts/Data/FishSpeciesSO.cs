using UnityEngine;

namespace Fishing
{
    /// <summary>
    /// 어종 하나 = 이 애셋 하나. 기획서 §3, §7.
    ///
    /// 습성 6스탯(궤도·구역·경계심·호기심·접근속도·식탐)이 난이도를 만들고,
    /// 선회율이 이동 물리를, 유인 확률이 착수 직후 반응을 담당한다.
    /// 별도의 난이도 로직 없이 이 값들만으로 "잡어"와 "대물"이 갈린다.
    /// </summary>
    [CreateAssetMenu(menuName = "Fishing/Fish Species", fileName = "Fish_")]
    public class FishSpeciesSO : ScriptableObject
    {
        [Header("식별")]
        public string speciesId = "perch";
        public string displayName = "초록 잡어";
        [Tooltip("점수이자 재료 환산값. 둘을 같은 비율로 유지해야 §8-4가 성립한다.")]
        public int score = 1;

        [Header("궤도 — §4")]
        public PathType pathType = PathType.Lane;
        [Tooltip("Lane/School 순항 속도 (유닛/초)")] public float cruiseSpeed = 1.2f;
        [Tooltip("Lane 사인 진폭")] public float laneAmp = 0.6f;
        [Tooltip("Lane 사인 주기(초)")] public float lanePeriod = 4f;
        [Tooltip("Loop 가로 반경")] public float loopA = 4f;
        [Tooltip("Loop 세로 반경")] public float loopB = 2.5f;
        [Tooltip("Loop 한 바퀴 주기(초). 8~12초라야 눈으로 읽힌다.")] public float loopPeriod = 10f;
        [Tooltip("HoverDash 부유 시간 범위(초)")] public Vector2 hoverTime = new Vector2(2f, 4f);
        [Tooltip("HoverDash 돌진 지속(초)")] public float dashTime = 0.4f;
        [Tooltip("HoverDash 돌진 거리 범위(유닛)")] public Vector2 dashDistance = new Vector2(1.5f, 2.5f);
        [Tooltip("HoverDash 가 집에서 벗어날 수 있는 반경")] public float homeRadius = 3f;

        [Header("구역 — §4-3 (수면 높이 대비 0~1)")]
        public Vector2 band = new Vector2(0.05f, 0.95f);

        [Header("개체 수")]
        public Vector2Int aliveCount = new Vector2Int(4, 6);
        [Tooltip("School 한 무리의 마리 수")] public Vector2Int schoolSize = new Vector2Int(5, 8);
        [Tooltip("School 포메이션 반경")] public float schoolRadius = 1.2f;
        [Tooltip("세션 시작 시 스폰 확률 (1 = 항상)")] [Range(0f, 1f)] public float spawnChance = 1f;
        [Tooltip("잡힌 뒤 재스폰까지(초). 0이면 즉시 리필")] public float respawnDelay = 0f;

        [Header("습성 — §3-1")]
        [Tooltip("놀람 반경(유닛). 인지 반경은 이 값 x 1.9")]
        public float startleRadius = 1.5f;
        [Tooltip("놀람 지속(초)")] public float startleTime = 0.4f;
        [Tooltip("인지 반경 안에서 초당 관심 전환 확률. ⚠️ '개체당'이라 동시 개체 수만큼 실효 발동률이 곱해진다.")]
        [Range(0f, 1f)] public float curiosity = 0.05f;
        [Tooltip("이 거리보다 가까우면 관심이 안 생긴다. 대물 전용(3.2). 0이면 제한 없음.")]
        public float minNoticeRadius = 0f;
        [Tooltip("관심 상태 이동 속도(유닛/초)")] public float approachSpeed = 2.5f;
        [Tooltip("입질 지속(초). 만료되면 자동 회수된다.")] public float biteWindow = 1.2f;

        [Header("유인 — §5-0")]
        [Tooltip("착수 순간 관심으로 전환될 확률. 대물은 0 — 물장구에 오지 않는다.")]
        [Range(0f, 1f)] public float lureChance = 0.45f;

        [Header("이동 물리 — §5-3")]
        [Tooltip("초당 최대 선회 각도. 대물 85(무겁게), 소어 280(민첩하게).")]
        public float turnRateDeg = 200f;

        [Header("사이즈 — §3-3 (MVP 미표시, 값만 굴린다)")]
        public Vector2 sizeCm = new Vector2(12f, 20f);

        [Header("표현")]
        public Sprite bodySprite;
        [Tooltip("대물만 사용. 비어 있으면 통짜 1파트.")] public Sprite tailSprite;
        [Tooltip("스프라이트 몸길이에 해당하는 월드 유닛")] public float bodyLength = 0.92f;
        [Tooltip("꼬리 파트 지연(초). 선회할 때 꼬리가 끌려오는 정도.")] public float tailLag = 0.05f;
        [Tooltip("파동 진폭 배율. 복어는 0.1 — 몸통이 뻣뻣해야 한다.")] public float waveAmp = 1f;
        [Tooltip("파동 주파수 배율. 작은 물고기일수록 잘게 빠르게.")] public float waveFreq = 1f;

        public float NoticeRadius => startleRadius * FishingTuning.NoticeMultiplier;
    }
}
