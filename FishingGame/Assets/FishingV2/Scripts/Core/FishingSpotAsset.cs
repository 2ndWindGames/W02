using UnityEngine;

namespace Fishing.V2
{
    [CreateAssetMenu(menuName = "Fishing V2/Fishing Spot", fileName = "Spot_")]
    public sealed class FishingSpotAsset : ScriptableObject
    {
        public string SpotId = "beach";
        public string DisplayName = "해변";
        public float SessionSeconds = 90f;
        public FishSpeciesAsset[] Species;
    }
}
