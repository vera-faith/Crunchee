using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Crunchee.Game
{
    /// <summary>
    /// The map catalog: what maps exist, whether they're playable yet, and
    /// which scene they load. The settings menu lists these as cards and the
    /// iOS build takes its scene list from the available ones.
    /// </summary>
    [System.Serializable]
    public class MapInfo
    {
        public string id;
        public string displayName;
        public string sceneName;   // scene asset name (no extension)
        public bool available;

        public string ScenePath => "Assets/Scenes/" + sceneName + ".unity";
    }

    public static class MapCatalog
    {
        public static readonly List<MapInfo> Maps = new List<MapInfo>
        {
            new MapInfo { id = "butterpath",  displayName = "Butter Path",  sceneName = "ButterPath",  available = true },
            new MapInfo { id = "candyclouds", displayName = "Candy Clouds", sceneName = "CandyClouds", available = false },
            new MapInfo { id = "mochigarden", displayName = "Mochi Garden", sceneName = "MochiGarden", available = false },
        };

        /// <summary>Scene paths of every playable map (for BuildIOS).</summary>
        public static string[] AvailableScenePaths()
        {
            var paths = new List<string>();
            foreach (var m in Maps)
                if (m.available) paths.Add(m.ScenePath);
            return paths.ToArray();
        }

        public static bool IsCurrent(MapInfo m)
            => SceneManager.GetActiveScene().name == m.sceneName;

        /// <summary>Load an available map that isn't already running.</summary>
        public static bool Select(string id)
        {
            var m = Maps.Find(x => x.id == id);
            if (m == null || !m.available || IsCurrent(m)) return false;
            SceneManager.LoadScene(m.sceneName);
            return true;
        }
    }
}
