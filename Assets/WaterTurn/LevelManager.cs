using UnityEngine;
using UnityEngine.SceneManagement;

namespace WaterTurn
{
    /// <summary>
    /// Loads one scene per level in campaign order. Every level scene carries one
    /// of these on its GameRoot, so it always knows which level it is.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public sealed class LevelManager : MonoBehaviour
    {
        public static LevelManager Instance { get; private set; }

        /// <summary>Index into <see cref="Campaign.Levels"/> for this scene.</summary>
        public int levelIndex;

        public WaterGame game;

        public static int LevelCount => Campaign.Levels.Length;

        public static string SceneName(int level) =>
            "Level_" + (Mathf.Clamp(level, 0, LevelCount - 1) + 1).ToString("00");

        /// <summary>True while there is another numbered level to go to.</summary>
        public bool HasNext => levelIndex + 1 < Campaign.VisibleLevels;

        void Awake()
        {
            Instance = this;
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public void LoadLevel(int level)
        {
            if (level < 0 || level >= LevelCount) return;
            GameSession.StartLevel = level;
            SceneManager.LoadScene(SceneName(level));
        }

        /// <summary>Next numbered level, or back to the hub after the last one.</summary>
        public void NextLevel()
        {
            if (HasNext) LoadLevel(levelIndex + 1);
            else ReturnToHub();
        }

        /// <summary>Reload this level from its saved scene, discarding progress.</summary>
        public void RestartLevel()
        {
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        }

        public void ReturnToHub()
        {
            GameSession.EnterHub(levelIndex);
        }
    }
}
