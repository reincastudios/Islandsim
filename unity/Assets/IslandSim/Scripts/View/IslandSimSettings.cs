using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace IslandSim.View
{
    /// <summary>Project assets the game needs at run time (made by Island Sim > Set Up Project).</summary>
    [CreateAssetMenu(menuName = "Island Sim/Settings")]
    public sealed class IslandSimSettings : ScriptableObject
    {
        [Tooltip("The URP renderer's Screen Space Ambient Occlusion feature, switched on for Ultra.")]
        public ScriptableRendererFeature Ssao;
        public static IslandSimSettings Load() => Resources.Load<IslandSimSettings>("IslandSim/Settings");
    }
}
