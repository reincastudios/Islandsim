using UnityEngine;

namespace IslandSim.View
{
    /// <summary>Starts the game when Play is pressed in a scene that has no IslandGame yet (an empty or new scene).</summary>
    static class Bootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Start()
        {
            if (Object.FindAnyObjectByType<IslandGame>() != null) return;
            new GameObject("IslandGame").AddComponent<IslandGame>();
        }
    }
}
