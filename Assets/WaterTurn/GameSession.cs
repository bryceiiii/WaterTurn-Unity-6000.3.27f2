using UnityEngine;
using UnityEngine.SceneManagement;

namespace WaterTurn
{
    /// <summary>
    /// Small bridge between the hub cave scene and the puzzle scenes.
    /// Unity reloads every scene from scratch, so the chosen level and the
    /// result of the last chamber live here for exactly one scene transition.
    /// </summary>
    public static class GameSession
    {
        public const string HubScene = "WaterTurnHub";
        public const string LevelScene = "WaterTurn";
        public const string ProgressKey = "WaterTurn.ChapterProgress.v1";
        public const string HiddenUnlockedKey = "WaterTurn.HiddenUnlocked.v1";
        public const string HiddenClearedKey = "WaterTurn.HiddenCleared.v1";
        public const string HubPearlKey = "WaterTurn.HubPearl.v1";

        public static int StartLevel = -1;
        public static int LastCompletedLevel = -1;

        public static int LevelsCleared => PlayerPrefs.GetInt(ProgressKey, 0);

        public static bool HiddenUnlocked =>
            PlayerPrefs.GetInt(HiddenUnlockedKey, 0) == 1 || LevelsCleared >= 1;

        public static bool HiddenCleared => PlayerPrefs.GetInt(HiddenClearedKey, 0) == 1;

        public static bool HubPearlTaken =>
            PlayerPrefs.GetInt(HubPearlKey, 0) == 1 || LevelsCleared >= 1;

        public static void EnterLevel(int level)
        {
            StartLevel = level;
            SceneManager.LoadScene(LevelScene);
        }

        public static void EnterHub()
        {
            SceneManager.LoadScene(HubScene);
        }
    }
}
