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
        public const string JellyRunScene = "JellyRun";
        public const string ProgressKey = "WaterTurn.ChapterProgress.v1";
        public const string HiddenUnlockedKey = "WaterTurn.HiddenUnlocked.v1";
        public const string HiddenClearedKey = "WaterTurn.HiddenCleared.v1";
        public const string HubPearlKey = "WaterTurn.HubPearl.v1";
        public const string JellyRunClearedKey = "WaterTurn.JellyRun.Cleared.v1";

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
            if (FromJellyRun)
            {
                FromJellyRun = false;
                InJellyRunChain = false;
                ChainChapter = -1;
                ChainLevel = -1;
                SceneManager.LoadScene(JellyRunScene);
                return;
            }
            InJellyRunChain = false;
            ChainChapter = -1;
            ChainLevel = -1;
            SceneManager.LoadScene(HubScene);
        }

        // ------------------------------------------------------- jelly run chain

        /// <summary>True while the player is in a level started from a chapter entrance.</summary>
        public static bool FromJellyRun;

        /// <summary>True inside the four-level chain of the current chapter.</summary>
        public static bool InJellyRunChain;

        public static int ChainChapter = -1;
        public static int ChainLevel = -1;

        /// <summary>Chapter entrance to spawn beside when the run scene loads again.</summary>
        public static int ReturnEntrance = -1;

        public static int JellyRunCleared => PlayerPrefs.GetInt(JellyRunClearedKey, 0) & 0xFFFFF;

        public static bool JellyRunLevelCleared(int level) =>
            level >= 0 && level < Campaign.VisibleLevels && (JellyRunCleared & (1 << level)) != 0;

        /// <summary>Open the run scene directly (hub button or code).</summary>
        public static void EnterJellyRun(int entrance = -1)
        {
            ReturnEntrance = entrance;
            FromJellyRun = false;
            InJellyRunChain = false;
            ChainChapter = -1;
            ChainLevel = -1;
            SceneManager.LoadScene(JellyRunScene);
        }

        /// <summary>
        /// Prepare a chapter chain and return the absolute level to load. The
        /// chain resumes at the first uncleared level; a completed chapter is
        /// replayed from its first level.
        /// </summary>
        public static int BeginJellyRunChapter(int chapter)
        {
            chapter = Mathf.Clamp(chapter, 0, 4);
            int first = chapter * 4;
            int start = first;
            int mask = JellyRunCleared;
            for (int i = 0; i < 4; i++)
            {
                if ((mask & (1 << (first + i))) == 0) { start = first + i; break; }
            }
            FromJellyRun = true;
            InJellyRunChain = true;
            ChainChapter = chapter;
            ChainLevel = start;
            ReturnEntrance = chapter;
            return start;
        }

        /// <summary>Persist a chain clear (only meaningful while inside a chain).</summary>
        public static void RecordRunLevelCleared(int level)
        {
            if (!InJellyRunChain || level < 0 || level >= Campaign.VisibleLevels) return;
            int mask = JellyRunCleared | (1 << level);
            PlayerPrefs.SetInt(JellyRunClearedKey, mask & 0xFFFFF);
            PlayerPrefs.Save();
        }

        /// <summary>Next level in the current chapter, or -1 if the chain is done.</summary>
        public static int NextRunLevel(int level)
        {
            if (!InJellyRunChain || ChainChapter < 0) return -1;
            int first = ChainChapter * 4;
            if (level < first || level > first + 3) return -1;
            if (level >= Campaign.VisibleLevels - 1) return -1;
            if (level % 4 == 3) return -1;
            return level + 1;
        }

        public static void EndRun()
        {
            InJellyRunChain = false;
            ChainChapter = -1;
            ChainLevel = -1;
        }
    }
}
