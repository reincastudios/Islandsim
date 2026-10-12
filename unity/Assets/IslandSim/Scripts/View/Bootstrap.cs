using UnityEngine;

namespace IslandSim.View
{
    /// <summary>Starts the game in whatever scene is open, so pressing Play always works, even in an empty scene.</summary>
    static class Bootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (Object.FindAnyObjectByType<IslandGame>() != null) return;
            new GameObject("IslandGame").AddComponent<IslandGame>();
        }
    }
}
