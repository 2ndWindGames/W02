using UnityEngine;

namespace Fishing
{
    /// <summary>낚시터 하나 = 이 애셋 하나. 어종 구성이 곧 그 물의 성격이다.</summary>
    [CreateAssetMenu(menuName = "Fishing/Fishing Spot", fileName = "Spot_")]
    public class FishingSpotSO : ScriptableObject
    {
        [System.Serializable]
        public struct Entry
        {
            public FishSpeciesSO species;
            [Tooltip("스폰 가중치 (현재는 개체 수 규칙이 우선이라 참고용)")]
            public float weight;
        }

        public string displayName = "해변";
        [Tooltip("세션 길이(초). 이 값이 첫 번째 성장 축이다 — §2-4")]
        public float sessionSeconds = 90f;
        public Entry[] species;
    }
}
