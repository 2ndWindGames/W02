using UnityEngine;

namespace Fishing.V2
{
    [CreateAssetMenu(menuName = "Fishing V2/Fish Species", fileName = "Fish_")]
    public sealed class FishSpeciesAsset : ScriptableObject
    {
        public FishSpeciesConfig Data = new FishSpeciesConfig();
    }
}
