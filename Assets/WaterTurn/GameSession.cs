using UnityEngine;
using UnityEngine.SceneManagement;

namespace WaterTurn
{
    /// <summary>
    /// Progress that has to survive a scene load. Each level is now its own
    /// scene, so this only carries PlayerPrefs progress and the scene routes.
    /// </summary>
    public static class GameSession
    {
        public const string HubScene = "WaterTurnHub";
        public const string ProgressKey = "WaterTurn.ChapterProgress.v1";
        public const string HiddenUnlockedKey = "WaterTurn.HiddenUnlocked.v1";
        public const string HiddenClearedKey = "WaterTurn.HiddenCleared.v1";
        public const string HubPearlKey = "WaterTurn.HubPearl.v1";

        /// <summary>Last level requested, for anything that needs to log or resume it.</summary>
        public static int StartLevel = -1;

        /// <summary>
        /// Level that was just cleared, read once by the hub when it loads so it
        /// can play the right reveal. Reset to -1 as soon as the hub consumes it.
        /// </summary>
        public static int LastCompletedLevel = -1;

        public static int LevelsCleared => PlayerPrefs.GetInt(ProgressKey, 0);

        public static bool HiddenUnlocked =>
            PlayerPrefs.GetInt(HiddenUnlockedKey, 0) == 1 || LevelsCleared >= 1;

        public static bool HiddenCleared => PlayerPrefs.GetInt(HiddenClearedKey, 0) == 1;

        public static bool HubPearlTaken =>
            PlayerPrefs.GetInt(HubPearlKey, 0) == 1 || LevelsCleared >= 1;

        public static void EnterLevel(int level)
        {
            StartLevel = Mathf.Clamp(level, 0, Campaign.Levels.Length - 1);
            SceneManager.LoadScene(LevelManager.SceneName(StartLevel));
        }

        /// <summary>
        /// Level the fish just left. The hub reads this once and places the fish
        /// beside that level's shrine instead of the central start pool. -1 means
        /// "no level context" (fresh boot), which uses the start pool.
        /// </summary>
        public static int ReturnLevel = -1;

        public static void EnterHub(int fromLevel = -1)
        {
            ReturnLevel = fromLevel;
            SceneManager.LoadScene(HubScene);
        }
    }
}
